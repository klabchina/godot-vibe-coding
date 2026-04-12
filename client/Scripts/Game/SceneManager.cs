using Godot;

namespace Game;

/// <summary>
/// Scene transition manager (Autoload).
/// Handles transitions: MainMenu → Matching → Battle → Result → MainMenu.
/// </summary>
public partial class SceneManager : Node
{
	public static SceneManager Instance { get; private set; }

	private const string MainMenuPath  = "res://Scenes/MainMenu.tscn";
	private const string MatchingPath  = "res://Scenes/Matching.tscn";
	private const string BattlePath    = "res://Scenes/Battle.tscn";
	private const string ResultPath    = "res://Scenes/Result.tscn";

	public override void _Ready()
	{
		Instance = this;
	}

	public void GoToMainMenu()
	{
		GameManager.Instance.CurrentPhase = GameManager.GamePhase.MainMenu;
		GetTree().ChangeSceneToFile(MainMenuPath);
	}

	public void GoToMatching()
	{
		GameManager.Instance.CurrentPhase = GameManager.GamePhase.Matching;
		GetTree().ChangeSceneToFile(MatchingPath);
	}

	public void GoToBattle()
	{
		GameManager.Instance.CurrentPhase = GameManager.GamePhase.Battle;
		GameManager.Instance.ResetBattleStats();
		GetTree().ChangeSceneToFile(BattlePath);
	}

	public void GoToResult()
	{
		GameManager.Instance.CurrentPhase = GameManager.GamePhase.Result;
		GetTree().ChangeSceneToFile(ResultPath);
	}
}
