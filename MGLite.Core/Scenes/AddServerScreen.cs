using System.Linq;
using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using ZeEngine.Networking;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

/// <summary>Add a remote server by address ("host" or "host:port") to the saved list.</summary>
public class AddServerScreen : MenuScreen
{
    private readonly PlayerData _player;

    private TextBox _nameBox;
    private TextBox _addressBox;
    private Label _status;

    public AddServerScreen(Game game, PlayerData player) : base(game)
    {
        _player = player;
    }

    public override void Initialize()
    {
        AddLabel("Add Server", 30, 10);

        AddLabel("Name (optional):", 30, 40);
        _nameBox = AddTextBox("", 30, 55, 210, 5);

        AddLabel("Address (host or host:port):", 30, 80);
        _addressBox = AddTextBox("", 30, 95, 210, 5);

        AddButton("Add", 30, 125, 64, 5, AddServer);
        AddButton("Back", 100, 125, 64, 5, OnBack);
        _status = AddLabel("", 30, 150);

        base.Initialize();
    }

    protected override void OnBack() => Go(new ServerListScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));

    private void AddServer()
    {
        if (!ServerAddress.TryParse(_addressBox.Text, out string host, out int port, out string error))
        {
            _status.Text = error;
            return;
        }

        string name = (_nameBox.Text ?? string.Empty).Trim();
        if (name.Length == 0)
            name = host;
        else if (!SaveManager.TryValidateName(name, out name, out error))
        {
            _status.Text = error;
            return;
        }

        var servers = SaveManager.LoadServers();
        if (servers.Any(s => s.Host == host && s.Port == port))
        {
            _status.Text = "That server is already saved.";
            return;
        }

        servers.Add(new SavedServer { Name = name, Host = host, Port = port });
        SaveManager.SaveServers(servers);

        Go(new ServerListScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }
}
