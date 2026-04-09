namespace Game.Ecs.Components;

public class AutoAimComponent
{
    public int TargetId = -1;       // Current locked target entity ID (-1 = no target)
    public float SearchRadius;       // Search radius (0 = unlimited / screen)
}
