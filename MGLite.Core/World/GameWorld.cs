// GameWorld.cs
// Data-driven World for MonoGame + MonoGame.Extended.
//
// NuGet packages required:
//   MonoGame.Extended
//   MonoGame.Extended.Entities
//   MonoGame.Extended.Tiled
//   MonoGame.Extended.Tiled.Renderers
//
// The world's layout comes from a Tiled map (.tmx, content-pipeline built)
// and its entity roster comes from a plain JSON file (see world.example.json).
// Nothing about entities, textures, or starting stats is hard-coded here —
// add/remove/rebalance entities by editing the JSON, not the code.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using MonoGame.Extended.ViewportAdapters;

namespace MGLite.Worlds
{
    #region Data contracts (what world.json deserializes into)

    public class WorldDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Untitled";

        [JsonPropertyName("tiledMap")]
        public string? TiledMapPath { get; set; }

        [JsonPropertyName("entities")]
        public List<EntityDefinition> Entities { get; set; } = new();
    }

    public class EntityDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("texture")]
        public string? TextureKey { get; set; }

        [JsonPropertyName("layerDepth")]
        public float LayerDepth { get; set; } = 0f;

        [JsonPropertyName("velocity")]
        public float[]? Velocity { get; set; }

        [JsonPropertyName("health")]
        public int? Health { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();
    }

    #endregion

    #region Components (plain structs — cheap to store/iterate in the ECS)

    public class Transform2
    {
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;

        public Transform2(Vector2 position)
        {
            Position = position;
            Rotation = 0f;
            Scale = Vector2.One;
        }
    }

    public class Velocity
    {
        public Vector2 Value;
        public Velocity(Vector2 value) => Value = value;
    }

    public class Sprite
    {
        public Texture2D Texture;
        public Color Color;
        public Vector2 Origin;
        public float LayerDepth;

        public Sprite(Texture2D texture, float layerDepth = 0f)
        {
            Texture = texture;
            Color = Color.White;
            Origin = texture != null ? new Vector2(texture.Width / 2f, texture.Height / 2f) : Vector2.Zero;
            LayerDepth = layerDepth;
        }
    }

    public class Health
    {
        public int Current;
        public int Max;
        public Health(int max)
        {
            Max = max;
            Current = max;
        }
    }

    public class Tag
    {
        public string Name;
        public Tag(string name) => Name = name;
    }

    #endregion

    #region Systems

    /// <summary>Integrates Velocity into Transform2.Position every frame.</summary>
    public class MovementSystem : EntityUpdateSystem
    {
        private ComponentMapper<Transform2> _transformMapper = null!;
        private ComponentMapper<Velocity> _velocityMapper = null!;

        public MovementSystem() : base(Aspect.All(typeof(Transform2), typeof(Velocity))) { }

        public override void Initialize(IComponentMapperService mapperService)
        {
            _transformMapper = mapperService.GetMapper<Transform2>();
            _velocityMapper = mapperService.GetMapper<Velocity>();
        }

        public override void Update(GameTime gameTime)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var entityId in ActiveEntities)
            {
                var transform = _transformMapper.Get(entityId);
                var velocity = _velocityMapper.Get(entityId);
                transform.Position += velocity.Value * dt;
            }
        }
    }

    /// <summary>Draws every entity that has both a Transform2 and a Sprite.</summary>
    public class RenderSystem : EntityDrawSystem
    {
        private readonly SpriteBatch _spriteBatch;
        private readonly OrthographicCamera _camera;

        private ComponentMapper<Transform2> _transformMapper = null!;
        private ComponentMapper<Sprite> _spriteMapper = null!;

        public RenderSystem(GraphicsDevice graphicsDevice, OrthographicCamera camera)
            : base(Aspect.All(typeof(Transform2), typeof(Sprite)))
        {
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _camera = camera;
        }

        public override void Initialize(IComponentMapperService mapperService)
        {
            _transformMapper = mapperService.GetMapper<Transform2>();
            _spriteMapper = mapperService.GetMapper<Sprite>();
        }

        public override void Draw(GameTime gameTime)
        {
            var viewMatrix = _camera.GetViewMatrix();

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: viewMatrix);

            foreach (var entityId in ActiveEntities)
            {
                var transform = _transformMapper.Get(entityId);
                var sprite = _spriteMapper.Get(entityId);

                if (sprite.Texture == null) continue;

                _spriteBatch.Draw(
                    sprite.Texture,
                    transform.Position,
                    null,
                    sprite.Color,
                    transform.Rotation,
                    sprite.Origin,
                    transform.Scale,
                    SpriteEffects.None,
                    sprite.LayerDepth);
            }

            _spriteBatch.End();
        }
    }

    #endregion

    /// <summary>
    /// A data-driven game world. Its layout comes from a Tiled map and its
    /// entity roster comes from a JSON file — this class just wires both
    /// into a MonoGame.Extended ECS World and drives Update/Draw each frame.
    /// </summary>
    public class GameWorld : IDisposable
    {
        public string Name { get; private set; } = "Untitled";
        public MonoGame.Extended.ECS.World EcsWorld { get; private set; } = null!;
        public Tilemap? Map { get; private set; }
        public TilemapRenderer? MapRenderer { get; private set; }
        public OrthographicCamera Camera { get; }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _content;
        private readonly Dictionary<string, Texture2D> _textureCache = new();

        public GameWorld(GameWindow gameWindow, GraphicsDevice graphicsDevice, ContentManager content, Viewport viewport)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;

            var viewportAdapter = new BoxingViewportAdapter(
                gameWindow,
                _graphicsDevice,
                viewport.Width,
                viewport.Height,
                viewport.Width,
                viewport.Height);
            Camera = new OrthographicCamera(viewportAdapter);
        }

        /// <summary>
        /// Loads a WorldDefinition JSON file, builds the Tiled map (if any),
        /// constructs the ECS world/systems, and spawns every entity
        /// described in the data file.
        /// </summary>
        public void LoadFromJson(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var definition = JsonSerializer.Deserialize<WorldDefinition>(json)
                ?? throw new InvalidDataException($"Could not parse world definition at '{jsonPath}'");

            Name = definition.Name;

            if (!string.IsNullOrWhiteSpace(definition.TiledMapPath))
            {
                Map = _content.Load<Tilemap>(definition.TiledMapPath);
                MapRenderer = new TilemapRenderer(_graphicsDevice);
            }

            EcsWorld = new WorldBuilder()
                .AddSystem(new MovementSystem())
                .AddSystem(new RenderSystem(_graphicsDevice, Camera))
                .Build();

            foreach (var entityDefinition in definition.Entities)
                SpawnEntity(entityDefinition);
        }

        /// <summary>Creates a live ECS entity from a single data definition.</summary>
        public Entity SpawnEntity(EntityDefinition data)
        {
            var entity = EcsWorld.CreateEntity();

            entity.Attach(new Transform2(new Vector2(data.X, data.Y)));

            if (!string.IsNullOrWhiteSpace(data.TextureKey))
                entity.Attach(new Sprite(GetTexture(data.TextureKey), data.LayerDepth));

            if (data.Velocity is { Length: 2 } velocity)
                entity.Attach(new Velocity(new Vector2(velocity[0], velocity[1])));

            if (data.Health is int health)
                entity.Attach(new Health(health));

            foreach (var tag in data.Tags)
                entity.Attach(new Tag(tag));

            return entity;
        }

        private Texture2D GetTexture(string key)
        {
            if (!_textureCache.TryGetValue(key, out var texture))
            {
                texture = _content.Load<Texture2D>(key);
                _textureCache[key] = texture;
            }
            return texture;
        }

        public void Update(GameTime gameTime)
        {
            MapRenderer?.Update(gameTime);
            EcsWorld?.Update(gameTime);
        }

        public void Draw(GameTime gameTime)
        {
            if (MapRenderer != null)
                foreach (var layer in Map!.Layers)
                    MapRenderer.DrawLayers(Camera, layer.Name);

            EcsWorld?.Draw(gameTime);
        }

        public void Dispose()
        {
            EcsWorld?.Dispose();
        }
    }
}