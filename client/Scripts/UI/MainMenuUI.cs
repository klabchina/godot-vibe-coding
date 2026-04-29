using Godot;
using Game.Ecs.Components;

namespace Game.UI;

public partial class MainMenuUI : Control
{

    public override void _Ready()
    {
        var startBtn = GetNode<Button>("ContentBox/HBox/StartButton");
        startBtn.Pressed += OnStartMultiPressed;

        var singlePlayerBtn = GetNode<Button>("ContentBox/HBox/SinglePlayerButton");
        singlePlayerBtn.Pressed += OnSinglePlayerPressed;
    }

    public void OnStartMultiPressed()
    {
        GameManager.Instance.CurrentMode = GameMode.MultiPlayer;
        SceneManager.Instance.GoToMatching();
    }

    public void OnSinglePlayerPressed()
    {
        GameManager.Instance.CurrentMode = GameMode.SinglePlayer;
        SceneManager.Instance.GoToBattle();
    }
}
