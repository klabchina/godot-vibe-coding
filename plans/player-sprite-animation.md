# PlayerComponent 序列帧动画实现计划

## Context
为 `RenderSystem.cs` 中的 PlayerComponent 添加序列帧动画渲染支持。弓箭手角色需要在待机/移动/攻击三种状态间切换播放对应帧动画，优先级：攻击 > 移动 > 待机。

## Approach
在 RenderSystem 中使用 Godot `AnimatedSprite2D` 替代现有的 ColorRect。动画状态由 ECS 组件数据驱动：
- 攻击状态：`BowComponent.CooldownTimer` 接近冷却值（刚射击后）→ attack 动画
- 移动状态：`VelocityComponent.Velocity` 非零 → walk 动画
- 否则：idle 动画

## Steps
1. **修改 RenderSystem.cs** — `CreateVisualNode()` 中将 PlayerComponent 的 ColorRect 替换为 `AnimatedSprite2D`，预设三个 AnimationLibrary（idle/walk/attack），加载对应帧图
2. **新增 PlayerSpriteState 内部状态类** — 跟踪当前动画状态、帧索引、播放时间（用于帧切换速度控制）
3. **实现动画更新逻辑** — `Update()` 中为玩家实体更新帧动画：优先级判断 → 状态切换 → 帧前进

## Key Files
- `client/Scripts/Ecs/ClientSystems/RenderSystem.cs` — 主要修改文件（CreateVisualNode + Update）
- `client/Assets/Sprites/Roles/archer_idle_{1-5}.png` — idle 帧
- `client/Assets/Sprites/Roles/archer_walk_{1-5}.png` — walk 帧
- `client/Assets/Sprites/Roles/archer_attack_{1-5}.png` — attack 帧

## Sprite Details
- 帧尺寸：126×54 px（从 sprite_list.md 约束）
- 帧率：10 FPS（100ms/帧）
- SpriteFrames 通过代码构建，直接 AddFrame() 逐帧加载

## Verification
在 Godot 编辑器中运行游戏，观察玩家角色是否正确播放待机/行走/攻击动画，切换状态时动画是否正确切换。
