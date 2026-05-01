using System;
using System.Collections.Generic;
using Game.Data;
using Game.Ecs.Components;

namespace Game.Ecs.Systems;

/// <summary>
/// Applies upgrade choices to player ECS components.
/// In multiplayer, requests are fed by server-authoritative SkillChoice messages.
/// </summary>
public class UpgradeApplySystem : GameSystem
{
    private readonly Queue<(int Slot, string SkillId, int Tick)> _pending = new();
    private readonly Dictionary<int, int> _lastAppliedTickBySlot = new();

    public void EnqueueChoice(int slot, string skillId, int tick)
    {
        _pending.Enqueue((slot, skillId, tick));
    }

    public override void Update(float delta)
    {
        while (_pending.Count > 0)
        {
            var req = _pending.Dequeue();

            if (_lastAppliedTickBySlot.TryGetValue(req.Slot, out var lastTick) && req.Tick <= lastTick)
                continue;

            if (!Enum.TryParse<UpgradeId>(req.SkillId, out var upgradeId))
                continue;

            var player = FindPlayerBySlot(req.Slot);
            if (player == null)
                continue;

            var upgrade = player.Get<UpgradeComponent>();
            if (upgrade == null)
                continue;

            upgrade.Apply(upgradeId);
            ApplyImmediateEffects(player, upgrade, upgradeId);

            _lastAppliedTickBySlot[req.Slot] = req.Tick;
        }
    }

    private Entity FindPlayerBySlot(int slot)
    {
        var players = World.GetEntitiesWith<PlayerComponent, UpgradeComponent>();
        foreach (var player in players)
        {
            var playerComp = player.Get<PlayerComponent>();
            if (playerComp.PlayerIndex == slot)
                return player;
        }

        return null;
    }

    private static void ApplyImmediateEffects(Entity player, UpgradeComponent upgrade, UpgradeId chosen)
    {
        switch (chosen)
        {
            case UpgradeId.MaxHpUp:
            {
                var health = player.Get<HealthComponent>();
                if (health != null)
                {
                    health.MaxHp = UpgradeData.GetMaxHp(upgrade.MaxHpLevel);
                    health.Hp = Math.Min(health.Hp + UpgradeData.HpHealPerUpgrade, health.MaxHp);
                }
                break;
            }
            case UpgradeId.MoveSpeedUp:
            {
                var velocity = player.Get<VelocityComponent>();
                if (velocity != null)
                {
                    velocity.Speed = UpgradeData.GetMoveSpeed(upgrade.MoveSpeedLevel);
                }
                break;
            }
            case UpgradeId.Shield:
            {
                var buff = player.Get<BuffComponent>();
                if (buff != null)
                {
                    buff.ShieldActive = true;
                    buff.ShieldCooldown = UpgradeData.ShieldRegenInterval;
                }
                break;
            }
            case UpgradeId.Regen:
            {
                var buff = player.Get<BuffComponent>();
                if (buff != null)
                {
                    buff.RegenActive = true;
                }
                break;
            }
            case UpgradeId.OrbitGuard:
            {
                var orbit = player.Get<OrbitComponent>();
                if (orbit != null)
                {
                    orbit.Count = upgrade.OrbitCount;
                }
                break;
            }
        }
    }
}
