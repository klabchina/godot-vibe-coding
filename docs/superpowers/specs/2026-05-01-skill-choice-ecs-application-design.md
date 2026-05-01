# SkillChoice 网络接收与 ECS 升级应用设计

## 背景

当前客户端升级流程存在职责混杂：

- `UpgradePanel` 在 UI 层直接执行 `UpgradeComponent.Apply(...)` 与立即效果（HP/速度/护盾/环绕物）
- `SyncClient` 仅发送 `SkillChoice`，未接收 `MsgIds.SkillChoice`
- 多人模式下升级生效未与服务端权威确认对齐

目标是将“升级效果落地”下沉到 ECS 系统，并在多人模式下以服务端下行消息为准。

## 目标与范围

### 目标

1. 客户端支持 `MsgIds.SkillChoice` 的接收。
2. `UpgradePanel.OnChoiceSelected` 只负责发送选择，不再直接修改组件数据。
3. 升级效果（`UpgradeComponent` 与相关组件联动）统一由 ECS 逻辑系统执行。
4. 多人模式按服务端下行消息生效，按 `slot -> PlayerComponent.PlayerIndex` 映射玩家。

### 非目标

- 不改动匹配流程。
- 不引入回滚预测机制（本地立即生效后校正）。
- 不扩展到本次需求外的战斗协议重构。

## 协议设计

当前 `SkillChoice` 缺少映射字段，无法按 slot 精确应用。采用以下扩展：

```proto
message SkillChoice {
    int32 tick = 1;
    string skill_id = 2;
    int32 slot = 3;
}
```

说明：

- 客户端上行可传本地 slot（或保持兼容由服务端补齐）。
- 服务端广播时必须带权威 `slot`，客户端按 slot 应用。

## 架构与职责

### 1) UI 层（`UpgradePanel`）

职责：仅处理展示与用户交互。

改动：

- `OnChoiceSelected` 中移除：
  - `upgrade.Apply(chosen)`
  - `ApplyImmediateEffects(chosen)`
- 多人模式下改为：
  - 调用 `SyncClient.SendSkillChoice(skillId)`
  - 关闭面板并触发 `OnUpgradeSelected`（用于流程推进/解暂停）

### 2) 网络层（`SyncClient`）

职责：收发消息与缓存，不改 ECS 组件。

改动：

- 新增 `SkillChoiceQueue`（与 `LockstepFrameQueue` 类似）。
- `HandleMessage` 增加 `case MsgIds.SkillChoice`：解析后入队。
- 保留 `SendSkillChoice` 上行发送。

### 3) ECS 层

#### `NetworkRecvSystem`

- 继续处理 `LockstepFrameQueue`（写 `VelocityComponent.LogicVelocity`）。
- 新增消费 `SkillChoiceQueue` 的入口（转交升级应用系统或队列）。

#### `UpgradeApplySystem`（新增逻辑系统）

职责：统一处理升级落地。

- 按 `slot -> PlayerComponent.PlayerIndex` 定位玩家实体。
- 执行 `UpgradeComponent.Apply(chosen)`。
- 执行立即效果（从 UI 下沉）：
  - `MaxHpUp`：更新 `HealthComponent.MaxHp` 并治疗
  - `MoveSpeedUp`：更新 `VelocityComponent.Speed`
  - `Shield`：更新 `BuffComponent` 护盾状态/冷却
  - `OrbitGuard`：更新 `OrbitComponent.Count`

## 数据流与时序

### 多人模式（权威生效）

1. 玩家在 `UpgradePanel` 做出选择。
2. 客户端上行 `SkillChoice`。
3. 服务端处理并广播权威 `SkillChoice{tick, skill_id, slot}`。
4. `SyncClient` 接收并入 `SkillChoiceQueue`。
5. ECS 逻辑帧中消费消息，`UpgradeApplySystem` 按 slot 应用升级。

### 单机模式

- 保持本地直接生效路径（不依赖网络）。

## 一致性与去重

为避免重复包导致重复升级：

- 维护 `lastAppliedTickBySlot`（或 `(slot,tick)` 去重集合）。
- 若同 slot 收到重复/过旧 tick，则忽略。

## 错误处理

1. **解析失败**：沿用网络层保护逻辑，忽略该消息。
2. **非法 skill_id**：无法映射 `UpgradeId` 时忽略并记录日志。
3. **slot 无对应实体**：忽略并记录开发日志。
4. **组件缺失**：仅跳过该效果分支，不中断系统。

## 验收标准

1. `UpgradePanel` 不再直接改 `Upgrade/Health/Velocity/Buff/Orbit`。
2. `SyncClient.HandleMessage` 支持 `MsgIds.SkillChoice` 接收并入队。
3. 多人模式升级仅在收到服务端下行后生效。
4. 升级应用按 `slot -> PlayerIndex` 正确命中目标玩家。
5. 重复消息不会重复叠加升级。
6. `client` 项目构建通过。

## 影响文件（计划）

- `server/proto/battle.proto`（扩展 `SkillChoice.slot`）
- `client/Scripts/Net/SyncClient.cs`
- `client/Scripts/UI/UpgradePanel.cs`
- `client/Scripts/Ecs/ClientSystems/NetworkRecvSystem.cs`
- `client/Scripts/Ecs/Systems/UpgradeApplySystem.cs`（新增）
- `client/Scripts/UI/BattleScene.cs`（系统注册与依赖注入）

## 风险与回滚

### 风险

- 协议改动需客户端/服务端同时更新，存在短暂不兼容窗口。
- 若服务端未广播 `SkillChoice`，多人升级将不生效。

### 回滚策略

- 保留旧逻辑分支开关（开发期可临时启用本地直接生效）。
- 协议与实现变更保持小步提交，便于回退。