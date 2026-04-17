namespace Game.Ecs.Components;

/// <summary>Collider shape type.</summary>
public enum ColliderShape { Circle, Box }

/// <summary>
/// Collider for all entities. Supports circle and OBB (oriented bounding box).
/// For Circle: uses Radius. For Box: uses HalfWidth/HalfHeight + TransformComponent.Rotation.
/// </summary>
public class ColliderComponent
{
    public ColliderShape Shape = ColliderShape.Circle;
    public float Radius;
    public float HalfWidth;   // Box half-extent along local X
    public float HalfHeight;  // Box half-extent along local Y
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
