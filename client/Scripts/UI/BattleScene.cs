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

	public override void _Ready()
	{
		_renderRoot = GetNode<Node2D>("RenderRoot");
		_hud = GetNode<BattleHud>("CanvasLayer/BattleHud");

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

		// Create wave spawner entity
		var spawner = _world.CreateEntity();
		var wave = spawner.Add(new WaveComponent());

		// Register systems in execution order
		_world.AddSystem(new InputSystem());
		// NetworkRecvSystem — Phase 6
		var waveSpawnSystem = new WaveSpawnSystem();
		_world.AddSystem(waveSpawnSystem);
		// BossAISystem — Phase 4
		_world.AddSystem(new MonsterAISystem());
		_world.AddSystem(new AutoAimSystem());
		_world.AddSystem(new MovementSystem());
		// OrbitSystem — Phase 4
		_world.AddSystem(new CollisionSystem());
		// PickupSystem — Phase 3
		_world.AddSystem(new DamageSystem());
		// EffectSystem — Phase 4
		// BuffSystem — Phase 4
		_world.AddSystem(new DeathSystem());
		// NetworkSendSystem — Phase 6

		_renderSystem = new RenderSystem { RenderRoot = _renderRoot };
		_world.AddSystem(_renderSystem);

		// Start wave 1
		waveSpawnSystem.StartNextWave(wave);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_world.Update(dt);
		UpdateHud();
	}

	private void UpdateHud()
	{
		// Find wave component
		var waveEntities = _world.GetEntitiesWith<WaveComponent>();
		if (waveEntities.Count > 0)
		{
			var wave = waveEntities[0].Get<WaveComponent>();
			_hud?.UpdateWave(wave.CurrentWave, WaveData.TotalWaves);
		}

		// Find player health
		var playerEntities = _world.GetEntitiesWith<PlayerComponent, HealthComponent>();
		if (playerEntities.Count > 0)
		{
			var health = playerEntities[0].Get<HealthComponent>();
			_hud?.UpdateHp(health.Hp, health.MaxHp);
		}
	}
}
