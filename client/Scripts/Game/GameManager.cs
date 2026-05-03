using Godot;

namespace Game;

/// <summary>
/// Global game state manager (Autoload).
/// Tracks current game phase and player session data.
/// </summary>
public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	public enum GamePhase { MainMenu, Matching, Battle, Result }

	public GamePhase CurrentPhase { get; set; } = GamePhase.MainMenu;
	public Ecs.Components.GameMode CurrentMode { get; set; } = Ecs.Components.GameMode.SinglePlayer;
	public int CurrentRandomSeed { get; set; } = 42;
	public int CurrentPlayerSlot { get; set; } = 0;
	public int[] CurrentMatchPlayerSlots { get; set; } = System.Array.Empty<int>();

	// Per-session battle stats
	public int KillCount { get; set; }
	public int TotalDamage { get; set; }
	public int TotalXpCollected { get; set; }
	public int WavesCompleted { get; set; }
	public float RemainingHpPercent { get; set; }

	public override void _Ready()
	{
		Instance = this;
	}

	public void ResetBattleStats()
	{
		KillCount = 0;
		TotalDamage = 0;
		TotalXpCollected = 0;
		WavesCompleted = 0;
		RemainingHpPercent = 1.0f;
	}
}
