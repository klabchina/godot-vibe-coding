using System.Collections.Generic;
using Game.Data;

namespace Game.Ecs.Components;

public class WaveComponent
{
    public int CurrentWave;                         // 1-based wave number
    public WaveData.SpawnEntry[] SpawnList;          // Monsters to spawn this wave
    public int SpawnIndex;                           // Next spawn entry index
    public int SpawnCountInEntry;                    // Spawned count within current entry
    public float SpawnTimer;                         // Timer for next spawn
    public float SpawnInterval;                      // Current random interval
    public int TotalSpawned;                         // Total monsters spawned this wave
    public int TotalToSpawn;                         // Total monsters to spawn this wave
    public int AliveMonsters;                        // Currently alive monsters
    public float WaveIntervalTimer;                  // Countdown between waves
    public bool IsSpawning;                          // Currently spawning monsters
    public bool IsWaveInterval;                      // Between waves countdown
    public bool AllWavesComplete;                    // All 8 waves done
}
