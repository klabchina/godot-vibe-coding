using Godot;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Updates timed buffs (Frenzy, Invincible), shield regen, and HP regen.
/// Runs after EffectSystem, before DeathSystem.
/// </summary>
public class BuffSystem : GameSystem
{
    public override void Update(float delta)
    {
        var players = World.GetEntitiesWith<PlayerComponent, BuffComponent, HealthComponent>();

        foreach (var player in players)
        {
            if (!player.IsAlive) continue;

            var buff = player.Get<BuffComponent>();
            var health = player.Get<HealthComponent>();
            var bow = player.Get<BowComponent>();
            var upgrade = player.Get<UpgradeComponent>();

            // --- Timed buff countdown ---
            if (buff.ActiveTimedBuff.HasValue)
            {
                buff.TimedBuffRemaining -= delta;
                if (buff.TimedBuffRemaining <= 0)
                {
                    buff.ActiveTimedBuff = null;
                    buff.TimedBuffRemaining = 0;
                }
            }

            // --- Frenzy: halve cooldown while active ---
            // Applied in AutoAimSystem by checking BuffComponent

            // --- Shield regen ---
            if (upgrade != null && upgrade.HasShield)
            {
                if (!buff.ShieldActive)
                {
                    buff.ShieldCooldown -= delta;
                    if (buff.ShieldCooldown <= 0)
                    {
                        buff.ShieldActive = true;
                        buff.ShieldCooldown = UpgradeData.ShieldRegenInterval;
                    }
                }
            }

            // --- HP Regen ---
            if (upgrade != null && upgrade.HasRegen && health.Hp > 0)
            {
                float regenAmount = health.MaxHp * UpgradeData.RegenPercentPerSec * delta;
                buff.RegenAccumulator += regenAmount;

                if (buff.RegenAccumulator >= 1f)
                {
                    int heal = (int)buff.RegenAccumulator;
                    buff.RegenAccumulator -= heal;
                    health.Hp = Mathf.Min(health.Hp + heal, health.MaxHp);
                }
            }
        }
    }
}
