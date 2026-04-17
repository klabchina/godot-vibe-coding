## Context
Monsters oscillate in front of obstacles because AdjustForObstacles recalculates each frame. Add detour memory so they commit to a direction until the path is clear.

## Approach
Add detour state fields to MonsterAIState. Add `IsPathBlockedByObstacles` (AABB line-segment intersection) and `GetDetourDirection` (smart side selection via cross product). Modify Slime/Boss/Orc chase to use detour memory before falling through to `AdjustForObstacles`.

## Steps
1. Add detour fields to MonsterAIState — `client/Scripts/Ecs/Components/MonsterAIState.cs`
   - `bool IsDetouring`, `Vec2 DetourDir`, `float DetourTimer`

2. Add `IsPathBlockedByObstacles(start, end, entityRadius)` to MonsterAISystem — `client/Scripts/Ecs/Systems/MonsterAISystem.cs:339+`
   - For each obstacle: expand AABB by entityRadius (Minkowski), test line-segment vs AABB intersection

3. Add `GetDetourDirection(toPlayer, monsterPos, blockingObstaclePos)` — same file
   - Cross product of toPlayer with (obstacleCenter - monsterPos) determines side
   - Return toPlayer rotated ±90°; if both blocked try ±135°; if all blocked return Vec2.Zero

4. Add `ApplyDetourMemory(ai, monsterPos, nearestPos, toPlayer, entityRadius, delta)` — same file
   - If path clear → clear IsDetouring, return toPlayer
   - If blocked & not detouring → enter detour, pick direction, set DetourTimer=2s
   - If blocked & detouring → decrement timer, if expired re-pick direction, else return DetourDir

5. Call `ApplyDetourMemory` in Slime chase (line 98), Boss direction (line 94), Orc chase (line 298) and Orc charge (line 285)
   - Replace raw `toPlayer` with detour-aware direction before passing to `AdjustForObstacles`

## Key Files
- `client/Scripts/Ecs/Components/MonsterAIState.cs` — add 3 fields after line 29
- `client/Scripts/Ecs/Systems/MonsterAISystem.cs` — add 3 methods, modify 4 call sites
- Existing: `AdjustForObstacles` (line 339), `IsBlockedByObstacle` (line 378), obstacle query pattern (line 343)

## Verification
```bash
cd client && dotnet build
```
