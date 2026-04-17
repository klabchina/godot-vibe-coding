namespace Game.Ecs.Components;

/// <summary>Marker: entity is a static obstacle (impassable AABB).</summary>
public class ObstacleComponent
{
    /// <summary>Sprite texture path (res://...). Empty = use fallback ColorRect.</summary>
    public string SpritePath { get; set; } = "";
}
