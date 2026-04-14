using Godot;

namespace Game.UI;

public partial class MainMenuUI : Control
{

    public override void _Ready()
    {
        var startBtn = GetNode<Button>("ContentBox/StartButton");
        startBtn.Pressed += OnStartPressed;
    }

    public void OnStartPressed()
    {
        SceneManager.Instance.GoToMatching();
    }
}
