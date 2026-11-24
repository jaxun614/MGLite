using ZeEngine.Networking;

namespace MGLite.Net;

// The messages this game sends. Add your own here: a small class that writes its fields in
// Write() and reads them back, in the same order, in Read(). Then give it an id in Register().

/// <summary>
/// Where a player is, as a fraction of the play area (0 to 1). Using fractions instead of pixels
/// means windows of different sizes agree on where everyone is.
/// </summary>
public sealed class PositionMessage : INetMessage
{
    public int PlayerId;
    public float X, Y;

    public void Write(NetWriter w) { w.Write(PlayerId); w.Write(X); w.Write(Y); }
    public void Read(NetReader r) { PlayerId = r.ReadInt32(); X = r.ReadSingle(); Y = r.ReadSingle(); }
}

/// <summary>The host tells clients who is in the game (Present = true) or who just left.</summary>
public sealed class PlayerInfoMessage : INetMessage
{
    public int PlayerId;
    public string Name;
    public bool Present;

    public void Write(NetWriter w) { w.Write(PlayerId); w.Write(Name); w.Write(Present); }
    public void Read(NetReader r) { PlayerId = r.ReadInt32(); Name = r.ReadString(); Present = r.ReadBoolean(); }
}

public sealed class ChatMessage : INetMessage
{
    public int PlayerId;
    public string Text;

    public void Write(NetWriter w) { w.Write(PlayerId); w.Write(Text); }
    public void Read(NetReader r) { PlayerId = r.ReadInt32(); Text = r.ReadString(); }
}

/// <summary>Sent out with IsReply = false; the other side bounces it back with IsReply = true.</summary>
public sealed class PingMessage : INetMessage
{
    public long Timestamp;
    public bool IsReply;

    public void Write(NetWriter w) { w.Write(Timestamp); w.Write(IsReply); }
    public void Read(NetReader r) { Timestamp = r.ReadInt64(); IsReply = r.ReadBoolean(); }
}

public static class GameMessages
{
    /// <summary>Hosts and clients with different ids refuse to connect. Change it for your own game.</summary>
    public const string GameId = "mglite";

    /// <summary>Call once at startup, after Core.Network exists (see MGLiteGame.Initialize).</summary>
    public static void Register(NetworkManager net)
    {
        net.GameId = GameId;

        // Ids 0-15 belong to the engine; yours start at 16. Both sides must agree on them.
        net.Register<PositionMessage>(16);
        net.Register<PlayerInfoMessage>(17);
        net.Register<ChatMessage>(18);
        net.Register<PingMessage>(19);
    }
}
