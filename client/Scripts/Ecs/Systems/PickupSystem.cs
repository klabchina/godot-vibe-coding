using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Handles experience orb attraction, pickup collection, XP accumulation,
/// level-up triggering, and item pickup effects.
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

            // 1) 无目标：找到第一个进入吸附范围的玩家并锁定目标
            if (pickup.TargetPlayerId < 0)
            {
                foreach (var playerEntity in players)
                {
                    if (!playerEntity.IsAlive) continue;

                    var playerTransform = playerEntity.Get<TransformComponent>();
                    var upgrade = playerEntity.Get<UpgradeComponent>();

                    float pickupRadius = upgrade != null
                        ? UpgradeData.GetPickupRadius(upgrade.MagnetLevel)
                        : PlayerData.BasePickupRadius;

                    float dist = pickupTransform.Position.DistanceTo(playerTransform.Position);
                    if (dist <= pickupRadius)
                    {
                        pickup.TargetPlayerId = playerEntity.Id;
                        // 锁定目标后，延长生命周期
                        pickup.LifeTime = PickupData.ExpOrbLifeTime;
                        break;
                    }
                }
            }

            // 2) 有目标：直接朝目标移动并尝试拾取
            if (pickup.TargetPlayerId >= 0)
            {
                var target = World.GetEntity(pickup.TargetPlayerId);
                if (target == null || !target.IsAlive)
                {
                    pickup.TargetPlayerId = -1;
                    continue;
                }

                var targetTransform = target.Get<TransformComponent>();
                if (targetTransform == null)
                {
                    pickup.TargetPlayerId = -1;
                    continue;
                }

                var vel = pickupEntity.Get<VelocityComponent>();
                if (vel != null)
                {
                    Vec2 dir = (targetTransform.Position - pickupTransform.Position).Normalized();
                    float currentSpeed = vel.LogicVelocity.Length();
                    float speed = currentSpeed > 0f
                        ? GMath.Max(currentSpeed, PickupData.ExpOrbFlySpeed)
                        : PickupData.ExpOrbFlySpeed;
                    vel.LogicVelocity = dir * speed;
                }

                float targetDist = pickupTransform.Position.DistanceTo(targetTransform.Position);
                if (targetDist <= 15f)
                {
                    CollectPickup(target, pickup);
                    World.DestroyEntity(pickupEntity.Id);
                }
            }
        }
    }

    private void CollectPickup(Entity playerEntity, PickupComponent pickup)
    {
        switch (pickup.Type)
        {
            case PickupType.ExpOrb:
                CollectExpOrb(playerEntity, pickup.Value);
                break;
            case PickupType.HealthPotion:
                CollectHealthPotion(playerEntity);
                break;
            case PickupType.Frenzy:
                CollectFrenzy(playerEntity);
                break;
            case PickupType.Invincible:
                CollectInvincible(playerEntity);
                break;
            case PickupType.Bomb:
                CollectBomb(playerEntity);
                break;
        }
    }

    private void CollectExpOrb(Entity playerEntity, int xpValue)
    {
        var player = playerEntity.Get<PlayerComponent>();
        if (player == null) return;

        player.TotalXp += xpValue;

        // Check level up
        int newLevel = LevelData.GetLevel(player.TotalXp);
        if (newLevel > player.CurrentLevel)
        {
            player.CurrentLevel = newLevel;
            OnLevelUp?.Invoke(playerEntity, newLevel);
        }
    }

    private void CollectHealthPotion(Entity playerEntity)
    {
        var health = playerEntity.Get<HealthComponent>();
        if (health == null) return;

        int healAmount = (int)(health.MaxHp * PickupData.HealthPotionHealPercent);
        health.Hp = GMath.Min(health.Hp + healAmount, health.MaxHp);
    }

    private void CollectFrenzy(Entity playerEntity)
    {
        var buff = playerEntity.Get<BuffComponent>();
        if (buff == null) return;

        // Timed buffs overwrite each other
        buff.ActiveTimedBuff = BuffType.Frenzy;
        buff.TimedBuffRemaining = PickupData.FrenzyDuration;
    }

    private void CollectInvincible(Entity playerEntity)
    {
        var buff = playerEntity.Get<BuffComponent>();
        if (buff == null) return;

        buff.ActiveTimedBuff = BuffType.Invincible;
        buff.TimedBuffRemaining = PickupData.InvincibleDuration;
    }

    private void CollectBomb(Entity playerEntity)
    {
        // Deal damage to ALL monsters on screen
        var monsters = World.GetEntitiesWith<MonsterComponent, HealthComponent>();
        foreach (var monster in monsters)
        {
            if (!monster.IsAlive) continue;
            var health = monster.Get<HealthComponent>();
            health.Hp -= PickupData.BombDamage;
            if (health.Hp < 0) health.Hp = 0;
        }

        // Track damage dealt
        var player = playerEntity.Get<PlayerComponent>();
        if (player != null)
            player.TotalDamageDealt += PickupData.BombDamage * World.GetEntitiesWith<MonsterComponent>().Count;
    }
}
