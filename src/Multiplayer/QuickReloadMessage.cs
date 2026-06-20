using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace QuickReload.Multiplayer;

public struct QuickReloadMessage :
    INetMessage,
    IPacketSerializable
{
    public ulong playerId;

    public readonly bool ShouldBroadcast => true;

    public readonly NetTransferMode Mode => NetTransferMode.Reliable;

    public readonly LogLevel LogLevel => LogLevel.VeryDebug;

    public readonly bool ShouldBuffer => true;

    public readonly void Serialize(PacketWriter writer)
    {
        writer.WriteULong(playerId);
    }

    public void Deserialize(PacketReader reader)
    {
        this.playerId = reader.ReadULong();
    }

    public override readonly string ToString()
    {
        return $"[QUICKRELOAD]: QuickReloadMessage: playerId={playerId}";
    }
}
