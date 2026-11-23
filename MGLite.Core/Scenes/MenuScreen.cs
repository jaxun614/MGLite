using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Gum.Forms.Controls;
using MonoGameGum;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using ZeEngine;
using ZeEngine.Networking;
using MGLite.UI;

namespace MGLite.Scenes;

/// <summary>
/// Base class for Gum-based menu screens.
/// Controls are positioned in a 270x180 design space (see UiLayout). That space stays centered as
/// the window is resized, and a 9-slice panel stretches behind it to fill the window.
/// </summary>
public abstract class MenuScreen : GameScreen
{
    private sealed class Placed
    {
        public FrameworkElement Control;
        public float X;   // design-space position
        public float Y;
    }

    private readonly List<Placed> _controls = new();
    private Label _errorLabel;
    private bool _leaving;

    protected MenuScreen(Game game) : base(game) { }

    /// <summary>Draw the stretching 9-slice panel behind the controls. The main menu turns this off.</summary>
    protected virtual bool ShowPanel => true;

    // ---------------------------------------------------------------- Control helpers
    // x / y are design-space coordinates (0..270, 0..180); the screen centers them for you.
    protected Button AddButton(string text, float x, float y, float width, Action onClick)
        => AddButton(text, x, y, width, 16, onClick);

    protected Button AddButton(string text, float x, float y, float width, float height, Action onClick)
    {
        var button = new Button();
        button.AddToRoot();
        button.Width = width;
        button.Height = height;
        button.Text = text;
        button.Click += (_, _) => RunSafely(onClick);
        Track(button, x, y);
        return button;
    }

    protected Label AddLabel(string text, float x, float y)
    {
        var label = new Label();
        label.AddToRoot();
        label.Text = text;
        Track(label, x, y);
        return label;
    }

    protected TextBox AddTextBox(string text, float x, float y, float width, float height)
    {
        var box = new TextBox();
        box.AddToRoot();
        box.Width = width;
        box.Height = height;
        box.Text = text;
        Track(box, x, y);
        return box;
    }

    protected ListBox AddListBox(float x, float y, float width, float height)
    {
        var list = new ListBox();
        list.AddToRoot();
        list.Width = width;
        list.Height = height;
        Track(list, x, y);
        return list;
    }

    private void Track(FrameworkElement control, float x, float y)
    {
        var placed = new Placed { Control = control, X = x, Y = y };
        _controls.Add(placed);
        Place(placed, UiLayout.Offset);
    }

    private static void Place(Placed placed, Vector2 offset)
    {
        placed.Control.X = placed.X + offset.X;
        placed.Control.Y = placed.Y + offset.Y;
    }

    /// <summary>Re-centers every control after the window (and so the UI zoom) changed.</summary>
    private void LayoutControls()
    {
        Vector2 offset = UiLayout.Offset;
        foreach (Placed placed in _controls)
            Place(placed, offset);
    }

    // A click handler that throws shows the error on screen (and in the console) instead of crashing.
    private void RunSafely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            ShowError($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    protected void ShowError(string message)
    {
        _errorLabel ??= AddLabel("", 30, 168);
        _errorLabel.Text = message;
    }

    // ---------------------------------------------------------------- Navigation
    /// <summary>Fade to another screen. Ignored after the first call so a double-click can't navigate twice.</summary>
    protected void Go(GameScreen next, FadeTransition transition)
    {
        if (_leaving) return;
        _leaving = true;
        ScreenManager.ReplaceScreen(next, transition);
    }

    /// <summary>Called when Escape is pressed. Override to go back one screen.</summary>
    protected virtual void OnBack() { }

    // ---------------------------------------------------------------- Lifecycle
    public override void Initialize()
    {
        UiLayout.Changed += LayoutControls;
        base.Initialize();
    }

    public override void UnloadContent()
    {
        UiLayout.Changed -= LayoutControls;

        foreach (Placed placed in _controls)
            placed.Control.RemoveFromRoot();
        _controls.Clear();

        base.UnloadContent();
    }

    public override void Update(GameTime gameTime)
    {
        GumService.Default.Update(gameTime);

        KeyboardExtended.Update();
        if (KeyboardExtended.GetState().WasKeyPressed(Keys.Escape))
            OnBack();
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        DrawBackground(gameTime);

        if (ShowPanel)
            DrawPanel();

        GumService.Default.Draw();
    }

    /// <summary>Draw sprites behind the panel and the Gum UI.</summary>
    protected virtual void DrawBackground(GameTime gameTime) { }

    private void DrawPanel()
    {
        int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int height = GraphicsDevice.PresentationParameters.BackBufferHeight;

        int margin = 4 * UiLayout.Zoom; // 4 canvas units of space around the panel
        var bounds = new Rectangle(margin, margin, width - margin * 2, height - margin * 2);

        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Core.PanelSlice.Draw(Core.SpriteBatch, bounds, Color.White, UiLayout.Zoom);
        Core.SpriteBatch.End();
    }
}
