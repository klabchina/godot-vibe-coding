namespace Game.Data;

public enum MonsterType { Slime, Skeleton, Orc, Elite, Boss }
public enum PickupType { ExpOrb, HealthPotion, Frenzy, Invincible, Bomb }
public enum BuffType { Frenzy, Invincible, Shield }

public enum UpgradeId
{
    // Attack (6)
    MultiShot, AttackSpeed, DamageUp, Pierce, Bounce, Explosion,
    // Defense (4)
    MaxHpUp, MoveSpeedUp, Shield, Regen,
    // Special (4)
    Magnet, FreezeArrow, BurnArrow, OrbitGuard
}

public enum UpgradeCategory { Attack, Defense, Special }
public enum BossPhase { Chase, Summon, Frenzy }
