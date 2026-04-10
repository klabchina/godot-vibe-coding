namespace Game.Ecs.Components;

/// <summary>
/// Marks an entity as network-synchronized.
/// NetId is the server-assigned entity ID, Owner is the controlling player index,
/// and IsLocal indicates if this entity is controlled by the local player.
/// </summary>
public class NetworkSyncComponent
{
    public int NetId;
    public int Owner;     // player index (0 or 1), -1 for server-owned (monsters, pickups)
    public bool IsLocal;
}
