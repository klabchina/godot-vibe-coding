using Godot;

namespace Game.UI;

/// <summary>
/// Main menu: title + Start button → Matching scene.
/// </summary>
public partial class MainMenuUI : Control
{
    public override void _Ready()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical = GrowDirection.Both;
        vbox.AddThemeConstantOverride("separation", 30);
        vbox.Position = new Vector2(0, 0);
        AddChild(vbox);

        var title = new Label();
        title.Text = "Bow Survivor";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 48);
        vbox.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "Co-op Arrow Defense";
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(subtitle);

        var startBtn = new Button();
        startBtn.Text = "Start Game";
        startBtn.CustomMinimumSize = new Vector2(400, 60);
        startBtn.Pressed += OnStartPressed;
        vbox.AddChild(startBtn);
    }

    private void OnStartPressed()
    {
        SceneManager.Instance.GoToMatching();
    }
}
