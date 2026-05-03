using Godot;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Data;
using Game.Ecs.Components;
using Game.Ecs.Systems;
using Game.Ecs.ClientSystems;
using Game;
using Game.Net;
using Game.Utils;

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
	private bool _isPaused;
	private MapConfig _currentMap;
	private int _tickCount;
	private float _accumulator;
	private SyncClient _syncClient;

	// Pending level-ups queue (supports consecutive level-ups)
	private readonly System.Collections.Generic.Queue<(Entity player, int level)> _pendingLevelUps = new();

	public override void _Ready()
	{
		// 设置随机种子，必须在任何使用 GameRandom 的代码之前调用
		// 必须放在最前面，因为 ResourceLoader.Load 可能触发其他代码
		GameRandom.SetSeed(GameManager.Instance.CurrentRandomSeed);
		GameLogger.Enabled = true; // 关闭日志打印
		GameLogger.Print($"[BattleScene] Random seed set to {GameManager.Instance.CurrentRandomSeed} for deterministic simulation.");

		_renderRoot = GetNode<Node2D>("RenderRoot");
		_canvasLayer = GetNode<CanvasLayer>("CanvasLayer");
		_hud = GetNode<BattleHud>("CanvasLayer/BattleHud");

		_upgradePanel = ResourceLoader.Load<PackedScene>("res://Scenes/UpgradePanel.tscn").Instantiate() as UpgradePanel
			?? throw new System.InvalidOperationException("Failed to load UpgradePanel scene");
		_canvasLayer.AddChild(_upgradePanel);
		_upgradePanel.OnUpgradeSelected += (entity, id) => _isPaused = false;
		_upgradePanel.SetSyncClient(null);
		_upgradePanel.SetUpgradeApplySystem(null);

		MapLoader.LoadAll();
		StageLoader.Load("stage_2");
		_currentMap = MapLoader.PickRandom();
		MapLoader.ApplyBackground(_currentMap, this);
		GameLogger.Print($"Map: {_currentMap.Id}");

		InitializeWorld();
	}

	private void InitializeWorld()
	{
		_tickCount = 0;
		_accumulator = 0f;
		_world = new World();

		// Create player entities
		var localSlot = GameManager.Instance.CurrentMode == GameMode.MultiPlayer
			? GameManager.Instance.CurrentPlayerSlot
			: 0;

		if (GameManager.Instance.CurrentMode == GameMode.MultiPlayer)
		{
			var slots = GameManager.Instance.CurrentMatchPlayerSlots;
			if (slots != null && slots.Length > 0)
			{
				foreach (var slot in slots)
				{
					CreatePlayerEntity(slot, slot == localSlot, ResolveSpawnPositionBySlot(slot));
				}
			}
			else
			{
				CreatePlayerEntity(localSlot, true, ResolveSpawnPositionBySlot(localSlot));
			}
		}
		else
		{
			CreatePlayerEntity(0, true, ArenaData.Size / 2);
		}

		// Create game settings entity
		var settingEntity = _world.CreateEntity();
		settingEntity.Add(new GameSettingComponent
		{
			Mode = GameManager.Instance.CurrentMode
		});

		// Create wave spawner entity
		var spawner = _world.CreateEntity();
		var wave = spawner.Add(new WaveComponent());

		// Spawn map obstacles as ECS entities
		MapLoader.SpawnObstacles(_currentMap, _world);

		// Register systems in execution order
		_world.AddSystem(new InputSystem());
		_world.AddSystem(new NetworkInputSystem());
		if (GameManager.Instance.CurrentMode == GameMode.MultiPlayer)
		{
			_syncClient = new SyncClient
			{
				LocalPlayerSlot = localSlot
			};
			_upgradePanel.SetSyncClient(_syncClient);
			_world.AddSystem(new NetworkRecvSystem { Sync = _syncClient });
			_world.AddSystem(new NetworkSendSystem { Sync = _syncClient });
		}
		var upgradeApplySystem = new UpgradeApplySystem();
		_world.AddSystem(upgradeApplySystem);
		_upgradePanel.SetUpgradeApplySystem(upgradeApplySystem);

		var waveSpawnSystem = new WaveSpawnSystem();
		_world.AddSystem(waveSpawnSystem);

		var bossAISystem = new BossAISystem();
		bossAISystem.OnBossPhaseChange = OnBossPhaseChange;
		_world.AddSystem(bossAISystem);

		_world.AddSystem(new MonsterAISystem());
		_world.AddSystem(new MeleeAttackSystem());
		_world.AddSystem(new AutoAimSystem());
		_world.AddSystem(new MovementSystem());
		_world.AddSystem(new CollisionSystem());
		_world.AddSystem(new OrbitSystem());
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

	private void CreatePlayerEntity(int slot, bool isLocal, Vec2 spawnPos)
	{
		var player = _world.CreateEntity();
		player.Add(new PlayerComponent { PlayerIndex = slot, IsLocal = isLocal });
		player.Add(new NetworkSyncComponent
		{
			NetId = slot,
			Owner = slot,
			IsLocal = isLocal
		});
		player.Add(new TransformComponent { Position = spawnPos });
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
		if (isLocal)
			player.Add(new ClientInputComponent());
		player.Add(new ColliderComponent
		{
			Shape = ColliderShape.Box,
			Radius = PlayerData.PlayerRadius,
			HalfWidth = PlayerData.HalfWidth,
			HalfHeight = PlayerData.HalfHeight,
			Layer = CollisionLayers.Player,
			Mask = CollisionLayers.Monster | CollisionLayers.Pickup
		});
		player.Add(new UpgradeComponent());
		player.Add(new BuffComponent());
		player.Add(new OrbitComponent());
	}

	private Vec2 ResolveSpawnPositionBySlot(int slot)
	{
		var center = ArenaData.Size / 2;
		const float spacingX = 120f;
		const float spacingY = 80f;

		if (slot == 0) return new Vec2(center.X - spacingX, center.Y);
		if (slot == 1) return new Vec2(center.X + spacingX, center.Y);

		int ring = slot / 2;
		float dirX = slot % 2 == 0 ? -1f : 1f;
		float dirY = ring % 2 == 0 ? -1f : 1f;
		return new Vec2(center.X + dirX * spacingX * (ring + 1), center.Y + dirY * spacingY);
	}

	private const float FixedDelta = ServerConfig.ServerFrameTime;  // 固定步长，与服务器一致

	public override void _Process(double delta)
	{
		if (_gameOver) return;

		float deltaSec = (float)delta;

		// 累积实际时间，使用固定步长更新逻辑系统（服务器帧率）
		_accumulator += deltaSec;

		while (_accumulator >= FixedDelta)
		{
			if (!_isPaused)
			{
				if (GameManager.Instance.CurrentMode == GameMode.MultiPlayer && _syncClient != null)
				{
					if (_syncClient.CanAdvanceOneTick())
					{
						_world.UpdateLogic(FixedDelta);
						_tickCount++;
					}
				}
				else
				{
					_world.UpdateLogic(FixedDelta);
					_tickCount++;
				}
			}
			_accumulator -= FixedDelta;
		}

		// 每帧更新渲染系统（客户端帧率）
		_world.UpdateRender(deltaSec);

		UpdateHud();
		ProcessPendingLevelUps();
		CheckGameOver();
		SpawnDamageNumbers();

		// 每 10 tick 打印一次玩家 HP 状态
		if (_tickCount > 0 && _tickCount % 10 == 0)
		{
			var waveEntities = _world.GetEntitiesWith<WaveComponent>();
			int currentWave = 0, aliveMonsters = 0;
			foreach (var w in waveEntities)
			{
				var wave = w.Get<WaveComponent>();
				currentWave = wave.CurrentWave;
				aliveMonsters = wave.AliveMonsters;
				break;
			}
			var players = _world.GetEntitiesWith<PlayerComponent>();
			string playerHpInfo = "";
			foreach (var p in players)
			{
				var pc = p.Get<PlayerComponent>();
				var hc = p.Get<HealthComponent>();
				playerHpInfo += $" P{pc.PlayerIndex}: HP={hc.Hp}/{hc.MaxHp}";
			}
			var elapsed = _tickCount * FixedDelta;
			GameLogger.Print($"[Tick {_tickCount,6} | {elapsed,6:F1}s] Wave {currentWave}{playerHpInfo}");
		}
	}

	private void OnPlayerLevelUp(Entity playerEntity, int newLevel)
	{
		// 以下代码保留以备将来启用升级面板
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

		var playerComp = player.Get<PlayerComponent>();
		if (GameManager.Instance.CurrentMode == GameMode.MultiPlayer && (playerComp == null || !playerComp.IsLocal))
			return;

		var upgrade = player.Get<UpgradeComponent>();
		if (upgrade == null) return;

		if (level > LevelData.MaxLevel) return;

		var options = UpgradeRoller.Roll(upgrade, level);
		if (options.Count > 0)
		{
			if (GameManager.Instance.CurrentMode != GameMode.MultiPlayer)
				_isPaused = true;
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

		// [ECS一致性日志] 输出战斗结算数据，用于对比客户端/服务端运行结果
		GameLogger.Print("========== ECS 战斗结算 ==========");
		GameLogger.Print($"结果: {(victory ? "胜利" : "失败")}");
		GameLogger.Print($"完成波数: {wavesCompleted}");
		GameLogger.Print($"存活玩家数: {players.Count}");
		foreach (var p in players)
		{
			var pc = p.Get<PlayerComponent>();
			var hc = p.Get<HealthComponent>();
			var up = p.Get<UpgradeComponent>();
			GameLogger.Print($"  玩家: KillCount={pc.KillCount}, TotalDamage={pc.TotalDamageDealt}, " +
				$"Level={pc.CurrentLevel}, Xp={pc.TotalXp}, Hp={hc.Hp}/{hc.MaxHp}");
			if (up != null)
			{
				GameLogger.Print($"  升级: OrbitCount={up.OrbitCount}");
			}
		}
		GameLogger.Print($"存活怪物数: {_world.GetEntitiesWith<MonsterComponent>().Count}");
		GameLogger.Print($"Tick数: {_tickCount}");
		GameLogger.Print($"[Debug] GameRandom calls: {GameRandom.CallCount}");
		GameLogger.Print("=================================");

		if (GameManager.Instance.CurrentMode == GameMode.MultiPlayer && _syncClient != null)
		{
			_syncClient.SendGameEndSubmit(victory ? "Win" : "Lose");
			NetManager.Instance.Disconnect();
		}

		// Delay transition slightly so player sees the final state
		GetTree().CreateTimer(2.0f).Timeout += () =>
		{
			SceneManager.Instance.GoToResult();
		};
	}

	private void SpawnDamageNumbers()
	{
		// Handle orbit hits via OrbitHitComponent (pure ECS approach)
		var orbitHitEntities = _world.GetEntitiesWith<OrbitHitComponent, TransformComponent>();
		foreach (var entity in orbitHitEntities)
		{
			var hit = entity.Get<OrbitHitComponent>();
			var transform = entity.Get<TransformComponent>();

			var dmgNum = new DamageNumber();
			_canvasLayer.AddChild(dmgNum);
			dmgNum.Show(new Vector2(transform.Position.X - 10, transform.Position.Y - 30), hit.Damage);

			entity.Remove<OrbitHitComponent>();
		}

		// Handle collision system hits (arrows, monster projectiles)
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

	public override void _ExitTree()
	{
		_syncClient?.Dispose();
	}

	private void UpdateHud()
	{
		var waveEntities = _world.GetEntitiesWith<WaveComponent>();
		if (waveEntities.Count > 0)
		{
			var wave = waveEntities[0].Get<WaveComponent>();
			_hud?.UpdateWave(wave.CurrentWave, StageLoader.GetTotalWaves());
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
