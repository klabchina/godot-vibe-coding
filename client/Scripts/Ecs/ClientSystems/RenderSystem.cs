using Godot;
using System.Collections.Generic;
using Game.Ecs;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.ClientSystems;

/// <summary>
/// Client-only: syncs ECS state to Godot visual nodes.
/// Runs at render frequency for smooth animation.
/// </summary>
public class RenderSystem : GameSystem
{
    public override bool IsRenderSystem => true;

    public Node2D RenderRoot { get; set; }

    // 插值平滑：存储每个实体的渲染当前位置（独立于逻辑位置）
    private readonly Dictionary<int, Vector2> _renderPositions = new();
    private readonly Dictionary<int, float> _renderRotations = new();

    // 上帧位置/旋转检测（用于检测逻辑帧更新）
    private readonly Dictionary<int, Vector2> _prevLogicPositions = new();
    private readonly Dictionary<int, float> _prevLogicRotations = new();

    private readonly Dictionary<int, Node2D> _entityNodes = new();
    private readonly Dictionary<int, List<Node2D>> _orbitNodes = new();

    // 玩家动画状态跟踪
    private readonly Dictionary<int, float> _prevBowTimers = new();   // 上帧弓箭冷却计时器
    private readonly Dictionary<int, float> _attackAnimTimers = new(); // 攻击动画剩余时间
    private readonly Dictionary<int, float> _damageFlashTimers = new(); // 受伤红色闪烁计时器

    private const float AttackAnimDuration = 0.33f;
    private const float AnimFps = 10f;

    // 当前帧累计时间进度（每帧 Update 开始后累加 delta）
    private float _currentDelta = 0f;

    // 怪物动画状态跟踪
    private readonly Dictionary<int, string> _monsterAnims = new(); // entityId -> current anim

    /// <summary>Helper: convert Vec2 → Godot.Vector2</summary>
    private static Vector2 ToGodot(Vec2 v) => new(v.X, v.Y);

    /// <summary>Helper: float equality with epsilon</summary>
    private static bool IsFloatEqual(float a, float b, float epsilon = 0.001f) => Mathf.Abs(a - b) < epsilon;

    public override void Update(float delta)
    {
        if (RenderRoot == null)
            return;

        // 每帧累计时间进度
        _currentDelta += delta;

        // 判断当前帧是否已完成移动（时间进度超过服务器帧率）
        bool frameCompleted = _currentDelta >= ServerConfig.ServerFrameTime;

        foreach (var (id, entity) in World.Entities)
        {
            if (!entity.IsAlive)
                continue;

            var transform = entity.Get<TransformComponent>();
            if (transform == null)
                continue;

            if (!_entityNodes.TryGetValue(id, out var node))
            {
                node = CreateVisualNode(entity);
                if (node == null)
                    continue;

                RenderRoot.AddChild(node);
                _entityNodes[id] = node;

                // 新实体直接设置位置，不做插值
                _renderPositions[id] = ToGodot(transform.Position);
                _renderRotations[id] = transform.Rotation;
                _prevLogicPositions[id] = _renderPositions[id];
                _prevLogicRotations[id] = _renderRotations[id];
            }

            Vector2 targetPos = ToGodot(transform.Position);
            float targetRot = transform.Rotation;

            if (frameCompleted)
            {
                // 帧已完成移动：起点终点都设为目标位置（节点已在目标位置，无位移）
                _prevLogicPositions[id] = targetPos;
                _prevLogicRotations[id] = targetRot;
                _renderPositions[id] = targetPos;
                _renderRotations[id] = targetRot;

                // 应用渲染插值结果
                node.Position = _renderPositions[id];

                if (entity.Has<ArrowComponent>() || entity.Has<MonsterProjectileComponent>())
                    node.Rotation = _renderRotations[id];
            }
            else
            {
                // 非逻辑帧：按时间进度比例插值
                // 公式：lerp(_prevLogicPositions, _renderPositions, currentDelta / ServerFrameTime)
                float t = Mathf.Clamp(_currentDelta / ServerConfig.ServerFrameTime, 0f, 1f);
                node.Position = _prevLogicPositions[id].Lerp(_renderPositions[id], t);

                // 旋转插值（处理角度跨越 -π/π 的情况）
                float startRot = _prevLogicRotations[id];
                float endRot = _renderRotations[id];
                float diff = endRot - startRot;
                // 归一化到 [-π, π]
                while (diff > Mathf.Pi) diff -= Mathf.Tau;
                while (diff < -Mathf.Pi) diff += Mathf.Tau;
                node.Rotation = startRot + diff * t;
            }

            if (entity.Has<PlayerComponent>())
                UpdatePlayerAnimation(entity, node, delta);
            else if (entity.Has<MonsterComponent>())
                UpdateMonsterAnimation(entity, node, delta);

            UpdateEffectVisuals(entity, node);
        }

        // 遍历完成后，如果帧已完成，重置时间进度
        if (frameCompleted)
        {
            _currentDelta = 0f;
        }

        var toRemove = new List<int>();
        foreach (var (id, node) in _entityNodes)
        {
            var entity = World.GetEntity(id);
            if (entity == null || !entity.IsAlive)
            {
                node.QueueFree();
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
        {
            _entityNodes.Remove(id);
            _renderPositions.Remove(id);
            _renderRotations.Remove(id);
            _prevLogicPositions.Remove(id);
            _prevLogicRotations.Remove(id);
        }

        RenderOrbitArrows();
    }

    private void UpdateEffectVisuals(Entity entity, Node2D node)
    {
        if (!entity.Has<MonsterComponent>()) return;

        var effect = entity.Get<EffectComponent>();
        if (effect == null) return;

        var rect = node.GetChildOrNull<ColorRect>(0);
        if (rect == null) return;

        var monster = entity.Get<MonsterComponent>();
        Color baseColor = GetMonsterColor(monster.Type);

        if (effect.IsFrozen && effect.IsBurning)
            rect.Color = new Color(0.5f, 0.3f, 0.8f);
        else if (effect.IsFrozen)
            rect.Color = new Color(0.4f, 0.7f, 1.0f);
        else if (effect.IsBurning)
            rect.Color = new Color(1.0f, 0.5f, 0.1f);
        else
            rect.Color = baseColor;
    }

    private void RenderOrbitArrows()
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, OrbitComponent>();
        var activePlayerIds = new HashSet<int>();

        foreach (var player in players)
        {
            activePlayerIds.Add(player.Id);
            var orbit = player.Get<OrbitComponent>();
            var playerTransform = player.Get<TransformComponent>();

            if (!_orbitNodes.ContainsKey(player.Id))
                _orbitNodes[player.Id] = new List<Node2D>();

            var nodes = _orbitNodes[player.Id];

            while (nodes.Count < orbit.Count)
            {
                var orbitVisual = new Node2D();
                var sprite = new Sprite2D();
                sprite.Texture = GD.Load<Texture2D>("res://Assets/Sprites/Props/orbit_orb.png");
                sprite.Offset = new Vector2(-16, -16); // Center 32x32 sprite
                orbitVisual.AddChild(sprite);
                RenderRoot.AddChild(orbitVisual);
                nodes.Add(orbitVisual);
            }

            while (nodes.Count > orbit.Count)
            {
                var last = nodes[^1];
                last.QueueFree();
                nodes.RemoveAt(nodes.Count - 1);
            }

            if (orbit.Count > 0)
            {
                float angleStep = 360f / orbit.Count;
                for (int i = 0; i < orbit.Count; i++)
                {
                    float angle = GMath.DegToRad(orbit.CurrentAngle + angleStep * i);
                    var pos = playerTransform.Position + new Vec2(
                        GMath.Cos(angle) * UpgradeData.OrbitRadius,
                        GMath.Sin(angle) * UpgradeData.OrbitRadius
                    );
                    nodes[i].Position = ToGodot(pos);
                    nodes[i].Rotation = angle;
                }
            }
        }

        var toRemove = new List<int>();
        foreach (var (playerId, nodes) in _orbitNodes)
        {
            if (!activePlayerIds.Contains(playerId))
            {
                foreach (var n in nodes) n.QueueFree();
                toRemove.Add(playerId);
            }
        }
        foreach (var id in toRemove)
            _orbitNodes.Remove(id);
    }

    private Node2D CreateVisualNode(Entity entity)
    {
        var wrapper = new Node2D();

        if (entity.Has<PlayerComponent>())
        {
            var player = entity.Get<PlayerComponent>();

            var animSprite = new AnimatedSprite2D();
            animSprite.SpriteFrames = CreateArcherSpriteFrames();
            animSprite.Play(AnimNames.Idle);
            wrapper.AddChild(animSprite);

            var nameLabel = new Label();
            nameLabel.Text = player.IsLocal ? "me" : $"player{player.PlayerIndex}";
            nameLabel.Position = new Vector2(-50, -100);
            nameLabel.Size = new Vector2(100, 20);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 24);
            nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            nameLabel.AddThemeConstantOverride("outline_size", 10);
            wrapper.AddChild(nameLabel);

            return wrapper;
        }
        else if (entity.Has<MonsterComponent>())
        {
            var animSprite = new AnimatedSprite2D();
            var monster = entity.Get<MonsterComponent>();
            animSprite.SpriteFrames = CreateMonsterSpriteFrames(monster.Type);
            animSprite.Play(AnimNames.Walk);
            wrapper.AddChild(animSprite);
            return wrapper;
        }
        else if (entity.Has<ArrowComponent>())
        {
            // 玩家箭矢使用 arrow_normal.png 图片
            var sprite = new Sprite2D();
            sprite.Texture = GD.Load<Texture2D>(SpriteFramesConstant.ArrowNormal);
            wrapper.AddChild(sprite);
            return wrapper;
        }
        else if (entity.Has<MonsterProjectileComponent>())
        {
            var proj = entity.Get<MonsterProjectileComponent>();
            var sprite = new Sprite2D();
            // 骷髅（直线）→ fire，精英（追踪）→ star
            string texPath = proj.IsHoming
                ? SpriteFramesConstant.ArrowStar
                : SpriteFramesConstant.ArrowFire;
            sprite.Texture = GD.Load<Texture2D>(texPath);
            wrapper.AddChild(sprite);
            return wrapper;
        }
        else if (entity.Has<PickupComponent>())
        {
            var pickup = entity.Get<PickupComponent>();
            var baseColor = GetPickupColor(pickup.Type);
            float size = pickup.Type == PickupType.ExpOrb ? 10 : 14;
            // 深色底层（2x暗）
            var backRect = new ColorRect();
            backRect.Color = new Color(baseColor.R * 0.5f, baseColor.G * 0.5f, baseColor.B * 0.5f);
            backRect.Size = new Vector2(size + 4, size + 4);
            backRect.Position = new Vector2(-(size + 4) / 2, -(size + 4) / 2);
            wrapper.AddChild(backRect);
            // 顶层原色
            var frontRect = new ColorRect();
            frontRect.Color = baseColor;
            frontRect.Size = new Vector2(size, size);
            frontRect.Position = new Vector2(-size / 2, -size / 2);
            wrapper.AddChild(frontRect);
            return wrapper;
        }
        else if (entity.Has<ObstacleComponent>())
        {
            var obstacle = entity.Get<ObstacleComponent>();
            var collider = entity.Get<ColliderComponent>();
            float w = collider.HalfWidth * 2;
            float h = collider.HalfHeight * 2;

            if (!string.IsNullOrEmpty(obstacle.SpritePath))
            {
                var tex = GD.Load<Texture2D>(obstacle.SpritePath);
                if (tex != null)
                {
                    var sprite = new Sprite2D();
                    sprite.Texture = tex;
                    sprite.Scale = new Vector2(w / tex.GetWidth(), h / tex.GetHeight());
                    wrapper.AddChild(sprite);
                    return wrapper;
                }
            }
            // Fallback: brown ColorRect
            var rect = new ColorRect();
            rect.Color = new Color(0.3f, 0.25f, 0.2f, 0.8f);
            rect.Size = new Vector2(w, h);
            rect.Position = new Vector2(-collider.HalfWidth, -collider.HalfHeight);
            wrapper.AddChild(rect);
            return wrapper;
        }

        return null;
    }

    private static SpriteFrames CreateArcherSpriteFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        foreach (var anim in AnimNames.PlayerAnims)
        {
            frames.AddAnimation(anim);
            for (int i = 1; i <= SpriteFramesConstant.ArcherFrameCount; i++)
            {
                var path = anim switch
                {
                    AnimNames.Idle => $"{SpriteFramesConstant.ArcherIdlePrefix}{i}{SpriteFramesConstant.ArcherExt}",
                    AnimNames.Walk => $"{SpriteFramesConstant.ArcherWalkPrefix}{i}{SpriteFramesConstant.ArcherExt}",
                    AnimNames.Attack => $"{SpriteFramesConstant.ArcherAttackPrefix}{i}{SpriteFramesConstant.ArcherExt}",
                    _ => $"res://Assets/Sprites/Roles/archer_{anim}_{i}.png"
                };
                var tex = GD.Load<Texture2D>(path);
                frames.AddFrame(anim, tex);
            }
            frames.SetAnimationSpeed(anim, AnimFps);
            frames.SetAnimationLoop(anim, anim != AnimNames.Attack);
        }

        return frames;
    }

    private static Godot.SpriteFrames CreateMonsterSpriteFrames(MonsterType type)
    {
        var frames = new Godot.SpriteFrames();
        frames.RemoveAnimation("default");

        foreach (var anim in SpriteFramesConstant.MonsterAnims)
        {
            frames.AddAnimation(anim);
            for (int i = 1; i <= SpriteFramesConstant.MonsterFrameCount; i++)
            {
                var path = SpriteFramesConstant.GetMonsterPath(type, anim, i);
                var tex = GD.Load<Texture2D>(path);
                frames.AddFrame(anim, tex);
            }
            bool loop = anim != AnimNames.Death && anim != AnimNames.Attack; // death 和 attack 不循环
            frames.SetAnimationLoop(anim, loop);
            frames.SetAnimationSpeed(anim, AnimFps);
        }

        return frames;
    }

    private void UpdatePlayerAnimation(Entity entity, Node2D node, float delta)
    {
        var animSprite = node.GetChildOrNull<AnimatedSprite2D>(0);
        if (animSprite == null)
            return;

        int id = entity.Id;

        // 处理受伤红色闪烁
        if (entity.Has<DamageFlashComponent>())
        {
            var flash = entity.Get<DamageFlashComponent>();
            flash.Timer -= delta;
            if (flash.Timer <= 0)
            {
                entity.Remove<DamageFlashComponent>();
                animSprite.SelfModulate = Colors.White;
            }
            else
            {
                // 闪烁：从亮红渐变到白色 SelfModulate 直接覆盖颜色不过曝
                float t = flash.Timer / 0.2f;
                animSprite.SelfModulate = new Color(1f, 0.3f + 0.7f * t, 0.3f + 0.7f * t);
            }
        }

        // 检测攻击触发：BowComponent 的 CooldownTimer 重置（从小值跳大）时说明刚发射
        var bow = entity.Get<BowComponent>();
        if (bow != null)
        {
            _prevBowTimers.TryGetValue(id, out float prev);
            if (bow.CooldownTimer > prev + delta * 0.5f)
                _attackAnimTimers[id] = AttackAnimDuration;
            _prevBowTimers[id] = bow.CooldownTimer;
        }

        // 倒计攻击动画剩余时间
        _attackAnimTimers.TryGetValue(id, out float attackLeft);
        if (attackLeft > 0f)
            _attackAnimTimers[id] = attackLeft - delta;

        // 判断移动状态
        var velocity = entity.Get<VelocityComponent>();
        bool isMoving = velocity != null && velocity.LogicVelocity.LengthSquared() > 0.1f;

        // 根据水平速度翻转精灵
        if (velocity != null && Mathf.Abs(velocity.LogicVelocity.X) > 0.1f)
            animSprite.FlipH = velocity.LogicVelocity.X < 0;

        // 动画优先级：攻击 > 行走 > 待机
        string targetAnim;
        if (_attackAnimTimers.GetValueOrDefault(id) > 0f)
            targetAnim = AnimNames.Attack;
        else if (isMoving)
            targetAnim = AnimNames.Walk;
        else
            targetAnim = AnimNames.Idle;

        if (animSprite.Animation != targetAnim)
            animSprite.Play(targetAnim);
    }

    private void UpdateMonsterAnimation(Entity entity, Node2D node, float delta)
    {
        var animSprite = node.GetChildOrNull<AnimatedSprite2D>(0);
        if (animSprite == null)
            return;

        int id = entity.Id;
        var monster = entity.Get<MonsterComponent>();
        var transform = entity.Get<TransformComponent>();

        // 新增：检测 DeathPendingComponent → 强制播放死亡动画
        bool isDying = entity.Has<DeathPendingComponent>();

        if (isDying)
        {
            // 强制播放死亡动画
            if (_monsterAnims.GetValueOrDefault(id) != AnimNames.Death)
            {
                _monsterAnims[id] = AnimNames.Death;
                animSprite.Play(AnimNames.Death);
            }

            // 死亡动画播完后标记 entity 为 dead（World.Update 会自动清理）
            if (!animSprite.IsPlaying())
            {
                entity.IsAlive = false;
            }
        }
        else
        {
            // 根据朝向翻转（锁定目标在怪物左侧时翻转）
            bool flipH = false;
            if (transform != null && monster != null)
            {
                var ai = entity.Get<MonsterAIState>();
                if (ai != null && ai.TargetId >= 0)
                {
                    var targetEntity = World.GetEntity(ai.TargetId);
                    var targetTransform = targetEntity?.Get<TransformComponent>();
                    if (targetTransform != null)
                        flipH = targetTransform.Position.X < transform.Position.X;
                }
            }
            animSprite.FlipH = flipH;

            // 根据状态决定动画
            string targetAnim = GetMonsterTargetAnim(entity, monster);

            if (_monsterAnims.GetValueOrDefault(id) != targetAnim)
            {
                _monsterAnims[id] = targetAnim;
                animSprite.Play(targetAnim);
            }

            // 攻击动画播放完成后移除标记（用于近战攻击）
            if (targetAnim == AnimNames.Attack && entity.Has<AttackAnimationComponent>())
            {
                if (!animSprite.IsPlaying() || animSprite.Frame >= animSprite.SpriteFrames.GetFrameCount(AnimNames.Attack) - 1)
                {
                    entity.Remove<AttackAnimationComponent>();
                }
            }
        }
    }

    private static string GetMonsterTargetAnim(Entity entity, MonsterComponent monster)
    {
        // 优先死亡动画
        var health = entity.Get<HealthComponent>();
        if (health != null && health.Hp <= 0)
            return AnimNames.Death;

        // 近战攻击动画（由 MeleeAttackSystem 标记）
        if (entity.Has<AttackAnimationComponent>())
            return AnimNames.Attack;

        // 远程攻击动画（由 AI 状态机标记）
        var aiState = entity.Get<MonsterAIState>();
        if (aiState != null && aiState.FiredThisCycle)
            return AnimNames.Attack;

        return AnimNames.Walk;
    }

    private static Color GetMonsterColor(MonsterType type)
    {
        return type switch
        {
            MonsterType.Slime => Colors.Green,
            MonsterType.Skeleton => Colors.Gray,
            MonsterType.Orc => new Color(0.55f, 0.15f, 0.15f),
            MonsterType.Elite => Colors.Purple,
            MonsterType.Boss => Colors.Red,
            _ => Colors.White,
        };
    }

    private static Color GetPickupColor(PickupType type)
    {
        return type switch
        {
            PickupType.ExpOrb => Colors.LimeGreen,
            PickupType.HealthPotion => new Color(1.0f, 0.2f, 0.4f),
            PickupType.Frenzy => new Color(1.0f, 0.8f, 0.0f),
            PickupType.Invincible => Colors.White,
            PickupType.Bomb => new Color(1.0f, 0.0f, 0.0f),
            _ => Colors.White,
        };
    }
}

/// <summary>
/// 动画名称常量
/// </summary>
public static class AnimNames
{
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Attack = "attack";
    public const string Death = "death";

    public static string[] PlayerAnims => new[] { Idle, Walk, Attack };
    public static string[] MonsterAnims => new[] { Walk, Attack, Death };
}
