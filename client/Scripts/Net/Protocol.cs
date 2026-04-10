using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Data;

namespace Game.Net;

/// <summary>
/// Network message types exchanged between client and server.
/// All messages are JSON-encoded with a "type" discriminator.
/// </summary>
public enum MessageType
{
    // --- Matching phase ---
    MatchRequest,       // C → S : request to join matchmaking queue
    MatchUpdate,        // S → C : queue status update (waiting)
    MatchSuccess,       // S → C : match found (room id, player list)
    MatchCancel,        // C → S : cancel matchmaking
    PlayerReady,        // C → S : player is ready to start
    BattleStart,        // S → C : both players ready, begin battle

    // --- Battle phase ---
    PlayerInput,        // C → S : movement direction
    GameState,          // S → C : authoritative game state snapshot
    SpawnArrow,         // S → C : arrow creation event
    SpawnWave,          // S → C : new wave of monsters
    EntityDeath,        // S → C : entity died (monster / pickup)
    UpgradeOptions,     // S → C : 3 upgrade choices for the player
    UpgradeChoice,      // C → S : player's upgrade selection
    PickupSpawn,        // S → C : pickup (XP orb / item) spawned
    PickupCollect,      // S → C : pickup collected by player
    BuffApply,          // S → C : buff applied / removed
    BossPhaseChange,    // S → C : boss phase transition
    GameOver,           // S → C : game ended (victory / defeat)

    // --- Common ---
    Heartbeat,          // bidirectional keep-alive
    Disconnect,         // graceful disconnect
}

// ──────────────────── Envelope ────────────────────

/// <summary>
/// Top-level message envelope. Every WebSocket frame is a JSON object
/// with a "Type" field and a "Data" payload (also JSON-encoded string).
/// </summary>
public class NetMessage
{
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    /// <summary>Serialize to JSON string for sending.</summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, SerializeOptions);
    }

    /// <summary>Deserialize an incoming JSON string.</summary>
    public static NetMessage Deserialize(string json)
    {
        return JsonSerializer.Deserialize<NetMessage>(json, SerializeOptions);
    }

    /// <summary>Create a NetMessage from a typed payload.</summary>
    public static NetMessage Create<T>(MessageType type, T payload)
    {
        return new NetMessage
        {
            Type = type,
            Data = JsonSerializer.Serialize(payload, SerializeOptions),
        };
    }

    /// <summary>Extract typed payload from Data field.</summary>
    public T GetPayload<T>()
    {
        return JsonSerializer.Deserialize<T>(Data, SerializeOptions);
    }

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

// ──────────────────── Matching Messages ────────────────────

public class MatchRequestMsg { }

public class MatchUpdateMsg
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "waiting";

    [JsonPropertyName("playersInQueue")]
    public int PlayersInQueue { get; set; }
}

public class MatchSuccessMsg
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("players")]
    public List<PlayerInfo> Players { get; set; } = new();

    [JsonPropertyName("seed")]
    public int Seed { get; set; }
}

public class PlayerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

public class PlayerReadyMsg { }

public class BattleStartMsg
{
    [JsonPropertyName("seed")]
    public int Seed { get; set; }
}

// ──────────────────── Battle Messages ────────────────────

public class PlayerInputMsg
{
    [JsonPropertyName("dx")]
    public float Dx { get; set; }

    [JsonPropertyName("dy")]
    public float Dy { get; set; }

    [JsonPropertyName("seq")]
    public int Seq { get; set; }
}

public class GameStateMsg
{
    [JsonPropertyName("tick")]
    public int Tick { get; set; }

    [JsonPropertyName("players")]
    public List<PlayerStateData> Players { get; set; } = new();

    [JsonPropertyName("monsters")]
    public List<EntityStateData> Monsters { get; set; } = new();

    [JsonPropertyName("arrows")]
    public List<EntityStateData> Arrows { get; set; } = new();

    [JsonPropertyName("pickups")]
    public List<EntityStateData> Pickups { get; set; } = new();
}

public class PlayerStateData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("index")]
    public int PlayerIndex { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("xp")]
    public int Xp { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("damage")]
    public int TotalDamage { get; set; }

    [JsonPropertyName("buffs")]
    public List<BuffStateData> Buffs { get; set; } = new();

    [JsonPropertyName("upgrades")]
    public List<UpgradeStateData> Upgrades { get; set; } = new();
}

public class BuffStateData
{
    [JsonPropertyName("type")]
    public BuffType Type { get; set; }

    [JsonPropertyName("remaining")]
    public float Remaining { get; set; }
}

public class UpgradeStateData
{
    [JsonPropertyName("id")]
    public UpgradeId Id { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public class EntityStateData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("type")]
    public int TypeId { get; set; }
}

public class SpawnArrowMsg
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("ownerId")]
    public int OwnerId { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("vx")]
    public float Vx { get; set; }

    [JsonPropertyName("vy")]
    public float Vy { get; set; }

    [JsonPropertyName("damage")]
    public int Damage { get; set; }

    [JsonPropertyName("pierce")]
    public int Pierce { get; set; }

    [JsonPropertyName("bouncing")]
    public bool Bouncing { get; set; }

    [JsonPropertyName("explosive")]
    public bool Explosive { get; set; }

    [JsonPropertyName("freezing")]
    public bool Freezing { get; set; }

    [JsonPropertyName("burning")]
    public bool Burning { get; set; }
}

public class SpawnWaveMsg
{
    [JsonPropertyName("wave")]
    public int WaveNumber { get; set; }
}

public class EntityDeathMsg
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("killerId")]
    public int KillerId { get; set; }
}

public class UpgradeOptionsMsg
{
    [JsonPropertyName("options")]
    public List<UpgradeId> Options { get; set; } = new();
}

public class UpgradeChoiceMsg
{
    [JsonPropertyName("upgradeId")]
    public UpgradeId UpgradeId { get; set; }
}

public class PickupSpawnMsg
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("pickupType")]
    public PickupType Type { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }
}

public class PickupCollectMsg
{
    [JsonPropertyName("pickupId")]
    public int PickupId { get; set; }

    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }
}

public class BuffApplyMsg
{
    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("buffType")]
    public BuffType BuffType { get; set; }

    [JsonPropertyName("duration")]
    public float Duration { get; set; }

    [JsonPropertyName("remove")]
    public bool Remove { get; set; }
}

public class BossPhaseChangeMsg
{
    [JsonPropertyName("phase")]
    public BossPhase Phase { get; set; }

    [JsonPropertyName("xpReward")]
    public int XpReward { get; set; }
}

public class GameOverMsg
{
    [JsonPropertyName("victory")]
    public bool Victory { get; set; }

    [JsonPropertyName("wavesCompleted")]
    public int WavesCompleted { get; set; }
}

public class HeartbeatMsg
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}
