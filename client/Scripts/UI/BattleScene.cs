using Godot;
using Game.Ecs;
using Game.Ecs.Components;
using Game.Ecs.Systems;
using Game.Data;

namespace Game.UI;

/// <summary>
/// Battle scene controller. Initializes ECS world, player entity,
/// wave spawner, and runs the game loop.
/// </summary>
public partial class BattleScene : Node2D
{
	private World _world;
	private RenderSystem _renderSystem;
	private Node2D _renderRoot;
	private BattleHud _hud;
	private UpgradePanel _upgradePanel;

	// Pending level-ups queue (supports consecutive level-ups)
	private readonly System.Collections.Generic.Queue<(Entity player, int level)> _pendingLevelUps = new();

	public override void _Ready()
	{
		_renderRoot = GetNode<Node2D>("RenderRoot");
		_hud = GetNode<BattleHud>("CanvasLayer/BattleHud");

		// Create upgrade panel dynamically
		_upgradePanel = new UpgradePanel();
		_upgradePanel.Name = "UpgradePanel";
		_upgradePanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		GetNode<CanvasLayer>("CanvasLayer").AddChild(_upgradePanel);

		InitializeWorld();
	}

	private void InitializeWorld()
	{
		_world = new World();

		// Create player entity
		var player = _world.CreateEntity();
		player.Add(new PlayerComponent { PlayerIndex = 0, IsLocal = true });
		player.Add(new TransformComponent { Position = ArenaData.Size / 2 });
		player.Add(new VelocityComponent { Speed = PlayerData.BaseMoveSpeed });
		player.Add(new HealthComponent { Hp = PlayerData.BaseHp, MaxHp = PlayerData.BaseHp });
		player.Add(new BowComponent
		{
			Damage = PlayerData.BaseArrowDamage,
			Cooldown = PlayerData.BaseCooldown,
			CooldownTimer = 0,
			ArrowCount = PlayerData.BaseArrowCount,
			SpreadAngle = 0
		});
		player.Add(new AutoAimComponent { TargetId = -1, SearchRadius = 0 });
		player.Add(new ColliderComponent
		{
			Radius = PlayerData.PlayerRadius,
			Layer = CollisionLayers.Player,
			Mask = CollisionLayers.Monster | CollisionLayers.Pickup
		});
		player.Add(new UpgradeComponent());
		player.Add(new BuffComponent());
		player.Add(new OrbitComponent());

		// Create wave spawner entity
		var spawner = _world.CreateEntity();
		var wave = spawner.Add(new WaveComponent());

		// Register systems in execution order (see battle-gameplay-design.md §6.4)
		_world.AddSystem(new InputSystem());                 // 1. Movement input
		// NetworkRecvSystem — Phase 6                       // 2.
		var waveSpawnSystem = new WaveSpawnSystem();
		_world.AddSystem(waveSpawnSystem);                   // 3. Wave spawning

		var bossAISystem = new BossAISystem();
		bossAISystem.OnBossPhaseChange = OnBossPhaseChange;
		_world.AddSystem(bossAISystem);                      // 4. Boss AI

		_world.AddSystem(new MonsterAISystem());             // 4b. Monster AI (chase/dodge/charge)
		_world.AddSystem(new AutoAimSystem());               // 5. Auto aim + fire
		_world.AddSystem(new MovementSystem());              // 6. Movement
		_world.AddSystem(new OrbitSystem());                 // 7. Orbit guard
		_world.AddSystem(new CollisionSystem());             // 8. Collision detection

		var pickupSystem = new PickupSystem();
		pickupSystem.OnLevelUp = OnPlayerLevelUp;
		_world.AddSystem(pickupSystem);                      // 9. Pickup processing

		_world.AddSystem(new DamageSystem());                // 10. Damage + revive
		_world.AddSystem(new EffectSystem());                // 11. Arrow effects (bounce/explode/freeze/burn)
		_world.AddSystem(new BuffSystem());                  // 12. Buff tick (frenzy/shield/regen)
		_world.AddSystem(new DeathSystem());                 // 13. Death + drops
		// NetworkSendSystem — Phase 6                       // 14.

		_renderSystem = new RenderSystem { RenderRoot = _renderRoot };
		_world.AddSystem(_renderSystem);                     // 15. Render

		// Start wave 1
		waveSpawnSystem.StartNextWave(wave);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_world.Update(dt);
		UpdateHud();
		ProcessPendingLevelUps();
	}

	private void OnPlayerLevelUp(Entity playerEntity, int newLevel)
	{
		_pendingLevelUps.Enqueue((playerEntity, newLevel));
	}

	private void OnBossPhaseChange(int xpReward)
	{
		// Award XP to all players when Boss changes phase
		var players = _world.GetEntitiesWith<PlayerComponent>();
		foreach (var player in players)
		{
			var playerComp = player.Get<PlayerComponent>();
			playerComp.TotalXp += xpReward;

			int newLevel = LevelData.GetLevel(playerComp.TotalXp);
			if (newLevel > playerComp.CurrentLevel)
			{
				playerComp.CurrentLevel = newLevel;
				OnPlayerLevelUp(player, newLevel);
			}
		}
	}

	private void ProcessPendingLevelUps()
	{
		// Only show one upgrade panel at a time
		if (_upgradePanel.IsActive) return;
		if (_pendingLevelUps.Count == 0) return;

		var (player, level) = _pendingLevelUps.Dequeue();
		if (!player.IsAlive) return;

		var upgrade = player.Get<UpgradeComponent>();
		if (upgrade == null) return;

		// Don't show panel if already at max level
		if (level > LevelData.MaxLevel) return;

		var options = UpgradeRoller.Roll(upgrade, level);
		if (options.Count > 0)
		{
			_upgradePanel.Show(player, options);
		}
	}

	private void UpdateHud()
	{
		var waveEntities = _world.GetEntitiesWith<WaveComponent>();
		if (waveEntities.Count > 0)
		{
			var wave = waveEntities[0].Get<WaveComponent>();
			_hud?.UpdateWave(wave.CurrentWave, WaveData.TotalWaves);
		}

		var playerEntities = _world.GetEntitiesWith<PlayerComponent, HealthComponent>();
		if (playerEntities.Count > 0)
		{
			var health = playerEntities[0].Get<HealthComponent>();
			var player = playerEntities[0].Get<PlayerComponent>();
			_hud?.UpdateHp(health.Hp, health.MaxHp);
			_hud?.UpdateLevel(player.CurrentLevel);
			_hud?.UpdateXp(player.TotalXp);
		}
	}
}
