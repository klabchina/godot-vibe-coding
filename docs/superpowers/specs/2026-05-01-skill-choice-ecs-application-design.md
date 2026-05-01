# SkillChoice 联机链路与升级应用（现状复核 + 最小修复设计）

## 背景

已复核代码现状（client + server）：

- `SyncClient` 已支持 `MsgIds.SkillChoice` 接收并入 `SkillChoiceQueue`。
- `UpgradePanel.OnChoiceSelected` 已实现：多人发送 `SkillChoice`；单机走本地 `UpgradeApplySystem.EnqueueChoice`。
- `NetworkRecvSystem` 已消费 `SkillChoiceQueue` 并转交 `UpgradeApplySystem`。
- `UpgradeApplySystem` 已按 `slot -> PlayerComponent.PlayerIndex` 应用升级与立即效果。
- 服务端已实现 `SkillChoice` 路由与广播（会用权威 slot 覆盖）。

因此，原“接入 SkillChoice 链路 + 升级下沉 ECS”的主体已具备。

当前核心缺陷是：

- `BattleScene.ProcessPendingLevelUps` 展示升级面板时无条件 `_isPaused = true`，导致多人模式本地逻辑停摆；服务端仍推进，进而产生客户端状态偏差。

## 已确认决策

1. 消息流向：`Client -> Server -> Client`（服务端权威）。
2. 升级应用位置：仅 ECS 系统应用，UI 只发送意图。
3. 暂停策略：多人模式不触发 `_isPaused` 全局暂停。

## 目标（本轮最小修复）

1. 保持现有 SkillChoice 联机链路不变（继续服务端权威生效）。
2. 修复多人升级面板导致的逻辑停摆问题。
3. 单机模式保留原暂停体验（升级面板期间暂停逻辑）。
4. 不扩大改动面到协议、匹配流程或额外功能。

## 非目标

- 不新增预测/回滚。
- 不重构 ECS 执行管线。
- 不改服务端 SkillChoice 协议字段。

## 设计方案

### A. BattleScene 暂停逻辑分流（唯一必要代码改动）

在 `BattleScene.ProcessPendingLevelUps` 中：

- 单机：继续 `_isPaused = true` 后显示面板。
- 多人：不设置 `_isPaused`，仅显示面板。

伪代码：

```csharp
if (options.Count > 0)
{
    if (GameManager.Instance.CurrentMode != GameMode.MultiPlayer)
        _isPaused = true;

    _upgradePanel.Show(player, options);
}
```

### B. 现有链路职责保持

- `UpgradePanel`：只做展示/选择/发送，不直接改组件。
- `SyncClient`：收发 SkillChoice，入队。
- `NetworkRecvSystem`：消费队列并转给 `UpgradeApplySystem`。
- `UpgradeApplySystem`：唯一升级落地点，按 slot 命中玩家并做去重。

## 验证策略（TDD 约束）

先做失败验证，再改代码：

1. **最小可执行失败验证**（若当前无测试基建，可先用可执行验证）：
   - 构造多人场景触发升级面板。
   - 观察升级面板显示后 `_world.UpdateLogic` 仍持续推进（tick 继续增长）。
   - 当前版本应失败（tick 停止），作为 red。

2. **实现最小修复**：仅改 `BattleScene.ProcessPendingLevelUps` 的暂停条件。

3. **回归验证**：
   - 多人：面板显示期间逻辑不断帧，SkillChoice 下行后升级生效。
   - 单机：面板显示期间仍暂停，选择后恢复。
   - `client` 构建通过。

## 验收标准

1. 多人模式升级面板出现时不再全局暂停逻辑帧。
2. 多人升级效果仍仅由服务端回传 SkillChoice 触发 ECS 生效。
3. 单机模式升级面板暂停行为不变。
4. 不引入与本需求无关文件改动。

## 影响文件（预期最小）

- `client/Scripts/UI/BattleScene.cs`（必要）
- 测试/验证代码文件（若项目已有对应测试入口，则按现有结构新增）
