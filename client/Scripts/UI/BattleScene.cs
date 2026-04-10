using Godot;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Ecs.Systems;
using Game.Ecs.ClientSystems;
using Game.Data;

namespace Game.UI;

/// <summary>
/// Battle scene controller. Initializes ECS world, player entity,
/// wave spawner, and runs the game loop.
/// Handles game-over detection and transition to Result scene.
/// </summary>
public partial class BattleScene : Node2D
{
	private World _world;
	private RenderSystem _renderSystem;
	private Node2D _renderRoot;
	private BattleHud _hud;
	private UpgradePanel _upgradePanel;
	private CanvasLayer _canvasLayer;
	private bool _gameOver;

	// Pending level-ups queue (supports consecutive level-ups)
	private readonly System.Collections.Generic.Queue<(Entity player, int level)> _pendingLevelUps = new();

	public override void _Ready()
	{
		_renderRoot = GetNode<Node2D>("RenderRoot");
		_canvasLayer = GetNode<CanvasLayer>("CanvasLayer");
		_hud = GetNode<BattleHud>("CanvasLayer/BattleHud");

		// Create upgrade panel dynamically
		_upgradePanel = new UpgradePanel();
		_upgradePanel.Name = "UpgradePanel";
		_upgradePanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_canvasLayer.AddChild(_upgradePanel);

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

		// Register systems in execution order
		_world.AddSystem(new InputSystem());
		var waveSpawnSystem = new WaveSpawnSystem();
		_world.AddSystem(waveSpawnSystem);

		var bossAISystem = new BossAISystem();
		bossAISystem.OnBossPhaseChange = OnBossPhaseChange;
		_world.AddSystem(bossAISystem);

		_world.AddSystem(new MonsterAISystem());
		_world.AddSystem(new AutoAimSystem());
		_world.AddSystem(new MovementSystem());
		_world.AddSystem(new OrbitSystem());
		_world.AddSystem(new CollisionSystem());

		var pickupSystem = new PickupSystem();
		pickupSystem.OnLevelUp = OnPlayerLevelUp;
		_world.AddSystem(pickupSystem);

		_world.AddSystem(new DamageSystem());
		_world.AddSystem(new EffectSystem());
		_world.AddSystem(new BuffSystem());
		_world.AddSystem(new DeathSystem());

		_renderSystem = new RenderSystem { RenderRoot = _renderRoot };
		_world.AddSystem(_renderSystem);

		// Start wave 1
		waveSpawnSystem.StartNextWave(wave);
	}

	public override void _Process(double delta)
	{
		if (_gameOver) return;

		float dt = (float)delta;
		_world.Update(dt);
		UpdateHud();
		ProcessPendingLevelUps();
		CheckGameOver();
		SpawnDamageNumbers();
	}

	private void OnPlayerLevelUp(Entity playerEntity, int newLevel)
	{
		_pendingLevelUps.Enqueue((playerEntity, newLevel));
	}

	private void OnBossPhaseChange(int xpReward)
	{
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
		if (_upgradePanel.IsActive) return;
		if (_pendingLevelUps.Count == 0) return;

		var (player, level) = _pendingLevelUps.Dequeue();
		if (!player.IsAlive) return;

		var upgrade = player.Get<UpgradeComponent>();
		if (upgrade == null) return;

		if (level > LevelData.MaxLevel) return;

		var options = UpgradeRoller.Roll(upgrade, level);
		if (options.Count > 0)
		{
			_upgradePanel.Show(player, options);
		}
	}

	private void CheckGameOver()
	{
		var waveEntities = _world.GetEntitiesWith<WaveComponent>();
		if (waveEntities.Count == 0) return;
		var wave = waveEntities[0].Get<WaveComponent>();

		// Victory: all waves cleared and no monsters alive
		if (wave.AllWavesComplete && wave.AliveMonsters <= 0)
		{
			EndBattle(true, wave.CurrentWave);
			return;
		}

		// Defeat: all players dead (HP <= 0 and already used revive)
		var players = _world.GetEntitiesWith<PlayerComponent, HealthComponent>();
		bool allDead = true;
		foreach (var player in players)
		{
			var health = player.Get<HealthComponent>();
			if (health.Hp > 0)
			{
				allDead = false;
				break;
			}
			var revive = player.Get<ReviveComponent>();
			if (revive == null || !revive.HasRevived)
			{
				allDead = false; // still has revive pending
				break;
			}
		}

		if (allDead && players.Count > 0)
		{
			EndBattle(false, wave.CurrentWave);
		}
	}

	private void EndBattle(bool victory, int wavesCompleted)
	{
		_gameOver = true;

		// Write stats to GameManager for Result screen
		var gm = GameManager.Instance;
		gm.WavesCompleted = wavesCompleted;

		var players = _world.GetEntitiesWith<PlayerComponent, HealthComponent>();
		if (players.Count > 0)
		{
			var player = players[0].Get<PlayerComponent>();
			var health = players[0].Get<HealthComponent>();
			gm.KillCount = player.KillCount;
			gm.TotalDamage = player.TotalDamageDealt;
			gm.TotalXpCollected = player.TotalXp;
			gm.RemainingHpPercent = health.MaxHp > 0 ? (float)health.Hp / health.MaxHp : 0;
		}

		// Delay transition slightly so player sees the final state
		GetTree().CreateTimer(2.0f).Timeout += () =>
		{
			SceneManager.Instance.GoToResult();
		};
	}

	private void SpawnDamageNumbers()
	{
		var collisionSystem = _world.GetSystem<CollisionSystem>();
		if (collisionSystem == null) return;

		foreach (var hit in collisionSystem.Hits)
		{
			// Show for player arrows hitting monsters, OR monster projectiles hitting players
			bool isMonsterProjectileHit = false;
			if (!hit.IsArrow)
			{
				var attacker = _world.GetEntity(hit.AttackerId);
				isMonsterProjectileHit = attacker != null && attacker.Has<MonsterProjectileComponent>();
			}
			if (!hit.IsArrow && !isMonsterProjectileHit) continue;

			var defender = _world.GetEntity(hit.DefenderId);
			if (defender == null) continue;

			var transform = defender.Get<TransformComponent>();
			if (transform == null) continue;

			var dmgNum = new DamageNumber();
			_canvasLayer.AddChild(dmgNum);
			var pos = transform.Position;
			dmgNum.Show(new Vector2(pos.X - 10, pos.Y - 30), hit.Damage);
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
			var buff = playerEntities[0].Get<BuffComponent>();
			var upgrade = playerEntities[0].Get<UpgradeComponent>();

			_hud?.UpdateHp(health.Hp, health.MaxHp);
			_hud?.UpdateLevel(player.CurrentLevel);
			_hud?.UpdateXp(player.TotalXp);
			_hud?.UpdateKills(player.KillCount);
			_hud?.UpdateBuffs(buff);
			_hud?.UpdateUpgradeIcons(upgrade);
		}
	}
}
