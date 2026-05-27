using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace QuickReload.Multiplayer;

public struct QuickReloadMessage :
    INetMessage,
    IPacketSerializable
{
    public ulong playerId;
    public string hostIp;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(playerId);
        writer.WriteString(hostIp);
    }

    public void Deserialize(PacketReader reader)
    {
        this.playerId = reader.ReadULong();
        this.hostIp = reader.ReadString();
    }

    public override string ToString()
    {
        return $"[QUICKRELOAD]: QuickReloadMessage: playerId={playerId}, hostIp={hostIp}";
    }
}
