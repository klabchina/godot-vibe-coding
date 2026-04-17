using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Game.Data;

public class ObstacleConfig
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("w")]
    public float W { get; set; }

    [JsonPropertyName("h")]
    public float H { get; set; }

    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "";
}

public class MapConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("background")]
    public string Background { get; set; } = "";

    [JsonPropertyName("obstacles")]
    public List<ObstacleConfig> Obstacles { get; set; } = new();
}
