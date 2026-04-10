namespace Game.Ecs.Components;

/// <summary>
/// Simplified circle collider for all entities.
/// </summary>
public class ColliderComponent
{
    public float Radius;
    public int Layer;  // What layer this entity is on
    public int Mask;   // What layers this entity collides with
}

/// <summary>Collision layer constants.</summary>
public static class CollisionLayers
{
    public const int Player       = 1;
    public const int Monster      = 2;
    public const int Arrow        = 4;
    public const int Pickup       = 8;
    public const int MonsterArrow = 16; // Monster projectiles; collide with Player only
}
