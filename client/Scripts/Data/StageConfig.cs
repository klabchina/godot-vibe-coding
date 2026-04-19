using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Game.Data;

public class MonsterSpawnEntry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class WaveEntry
{
    [JsonPropertyName("monsters")]
    public List<MonsterSpawnEntry> Monsters { get; set; } = new();
}

public class StageConfig
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("waves")]
    public List<WaveEntry> Waves { get; set; } = new();
}
