using Google.Protobuf;
using Server.Proto;

namespace Game.Net;

public static class MsgIds
{
    public const uint MatchRequest = 1001;
    public const uint MatchCancel = 1002;
    public const uint MatchSuccess = 1004;

    public const uint PlayerReady = 2001;
    public const uint GameStart = 2002;

    public const uint PlayerMove = 3001;
    public const uint SkillChoice = 3002;
    public const uint GameEndSubmit = 3003;
    public const uint LockstepFrame = 3008;

    public const uint Heartbeat = 9001;
}

public static class Protocol
{
    public static byte[] BuildEnvelope(uint msgId, IMessage payload)
    {
        return BuildEnvelope(msgId, payload.ToByteArray());
    }

    public static byte[] BuildEnvelope(uint msgId, byte[] payload)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(msgId);
        writer.Write(payload);
        return ms.ToArray();
    }

    public static (uint MsgId, byte[] Payload)? ParseEnvelope(byte[] packet)
    {
        if (packet == null || packet.Length < 4) return null;

        try
        {
            using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(packet));
            var msgId = reader.ReadUInt32();
            var payload = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            return (msgId, payload);
        }
        catch
        {
            return null;
        }
    }

    public static IMessage? ParsePayload(uint msgId, byte[] payload)
    {
        return msgId switch
        {
            MsgIds.MatchSuccess => MatchSuccess.Parser.ParseFrom(payload),
            MsgIds.GameStart => GameStart.Parser.ParseFrom(payload),
            MsgIds.LockstepFrame => LockstepFrame.Parser.ParseFrom(payload),
            MsgIds.Heartbeat => null,
            _ => null,
        };
    }
}
