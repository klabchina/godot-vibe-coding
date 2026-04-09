using Godot;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Handles experience orb attraction, pickup collection, XP accumulation,
/// and level-up triggering.
/// </summary>
public class PickupSystem : GameSystem
{
    /// <summary>Set by BattleScene; called when player levels up.</summary>
    public System.Action<Entity, int> OnLevelUp;

    public override void Update(float delta)
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, ColliderComponent>();
        var pickups = World.GetEntitiesWith<PickupComponent, TransformComponent>();

        foreach (var pickupEntity in pickups)
        {
            if (!pickupEntity.IsAlive) continue;

            var pickup = pickupEntity.Get<PickupComponent>();
            var pickupTransform = pickupEntity.Get<TransformComponent>();

            // Decrement lifetime
            pickup.LifeTime -= delta;
            if (pickup.LifeTime <= 0)
            {
                World.DestroyEntity(pickupEntity.Id);
                continue;
            }

            // Check against each player
            foreach (var playerEntity in players)
            {
                var playerTransform = playerEntity.Get<TransformComponent>();
                var upgrade = playerEntity.Get<UpgradeComponent>();

                float pickupRadius = upgrade != null
                    ? UpgradeData.GetPickupRadius(upgrade.MagnetLevel)
                    : PlayerData.BasePickupRadius;

                float dist = pickupTransform.Position.DistanceTo(playerTransform.Position);

                if (dist <= pickupRadius)
                {
                    // Attract: fly toward player
                    var vel = pickupEntity.Get<VelocityComponent>();
                    if (vel != null)
                    {
                        Vector2 dir = (playerTransform.Position - pickupTransform.Position).Normalized();
                        vel.Velocity = dir * PickupData.ExpOrbFlySpeed;
                    }

                    // Collect if very close
                    if (dist <= 15f)
                    {
                        CollectPickup(playerEntity, pickup);
                        World.DestroyEntity(pickupEntity.Id);
                        break;
                    }
                }
            }
        }
    }

    private void CollectPickup(Entity playerEntity, PickupComponent pickup)
    {
        if (pickup.Type == PickupType.ExpOrb)
        {
            var player = playerEntity.Get<PlayerComponent>();
            if (player == null) return;

            player.TotalXp += pickup.Value;

            // Check level up
            int newLevel = LevelData.GetLevel(player.TotalXp);
            if (newLevel > player.CurrentLevel)
            {
                player.CurrentLevel = newLevel;
                OnLevelUp?.Invoke(playerEntity, newLevel);
            }
        }
        // Other pickup types (HealthPotion, Frenzy, etc.) handled in Phase 4
    }
}
