using System;
using System.Collections.Generic;
using Game.Data;
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
    public const uint GameOver = 3005;
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

    public static object? ParsePayload(uint msgId, byte[] payload)
    {
        return msgId switch
        {
            MsgIds.MatchSuccess => MatchSuccess.Parser.ParseFrom(payload),
            MsgIds.GameStart => GameStart.Parser.ParseFrom(payload),
            MsgIds.LockstepFrame => LockstepFrame.Parser.ParseFrom(payload),
            MsgIds.GameOver => GameOver.Parser.ParseFrom(payload),
            MsgIds.Heartbeat => null,
            _ => null,
        };
    }
}

// 兼容现有 ECS 消费结构的数据类型

public class MatchSuccessMsg
{
    public string RoomId { get; set; } = "";
    public List<PlayerInfoMsg> Players { get; set; } = new();
    public int Seed { get; set; }
}

public class PlayerInfoMsg
{
    public string Id { get; set; } = "";
    public int Index { get; set; }
}

public class BattleStartMsg
{
    public int Seed { get; set; }
}

public class PlayerInputMsg
{
    public float Dx { get; set; }
    public float Dy { get; set; }
    public int Seq { get; set; }
}

public class GameStateMsg
{
    public int Tick { get; set; }
    public List<PlayerStateData> Players { get; set; } = new();
    public List<EntityStateData> Monsters { get; set; } = new();
    public List<EntityStateData> Arrows { get; set; } = new();
    public List<EntityStateData> Pickups { get; set; } = new();
}

public class PlayerStateData
{
    public int Id { get; set; }
    public int PlayerIndex { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Xp { get; set; }
    public int Level { get; set; }
    public int Kills { get; set; }
    public int TotalDamage { get; set; }
    public List<BuffStateData> Buffs { get; set; } = new();
    public List<UpgradeStateData> Upgrades { get; set; } = new();
}

public class BuffStateData
{
    public BuffType Type { get; set; }
    public float Remaining { get; set; }
}

public class UpgradeStateData
{
    public UpgradeId Id { get; set; }
    public int Level { get; set; }
}

public class EntityStateData
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int TypeId { get; set; }
}

public class SpawnArrowMsg
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Vx { get; set; }
    public float Vy { get; set; }
    public int Damage { get; set; }
    public int Pierce { get; set; }
    public bool Bouncing { get; set; }
    public bool Explosive { get; set; }
    public bool Freezing { get; set; }
    public bool Burning { get; set; }
}

public class SpawnWaveMsg
{
    public int WaveNumber { get; set; }
}

public class EntityDeathMsg
{
    public int Id { get; set; }
    public int KillerId { get; set; }
}

public class UpgradeOptionsMsg
{
    public List<UpgradeId> Options { get; set; } = new();
}

public class UpgradeChoiceMsg
{
    public UpgradeId UpgradeId { get; set; }
}

public class PickupSpawnMsg
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public PickupType Type { get; set; }
    public int Value { get; set; }
}

public class PickupCollectMsg
{
    public int PickupId { get; set; }
    public int PlayerId { get; set; }
}

public class BuffApplyMsg
{
    public int PlayerId { get; set; }
    public BuffType BuffType { get; set; }
    public float Duration { get; set; }
    public bool Remove { get; set; }
}

public class BossPhaseChangeMsg
{
    public BossPhase Phase { get; set; }
    public int XpReward { get; set; }
}

public class GameOverMsg
{
    public bool Victory { get; set; }
    public int WavesCompleted { get; set; }
}