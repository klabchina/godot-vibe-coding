using Godot;
using Game.Data;
using Game.Ecs.Components;

namespace Game.UI;

/// <summary>
/// Displays active timed buff icon and remaining time in the HUD.
/// </summary>
public partial class BuffBar : HBoxContainer
{
    private Label _buffLabel;

    public override void _Ready()
    {
        _buffLabel = new Label();
        _buffLabel.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_buffLabel);
    }

    public void UpdateBuff(BuffComponent buff)
    {
        if (buff == null || !buff.ActiveTimedBuff.HasValue)
        {
            _buffLabel.Text = "";
            return;
        }

        string name = buff.ActiveTimedBuff.Value switch
        {
            BuffType.Frenzy => "FRENZY",
            BuffType.Invincible => "INVINCIBLE",
            BuffType.Shield => "SHIELD",
            _ => ""
        };

        _buffLabel.Text = $"[{name} {buff.TimedBuffRemaining:F1}s]";

        _buffLabel.AddThemeColorOverride("font_color", buff.ActiveTimedBuff.Value switch
        {
            BuffType.Frenzy => new Color(1f, 0.8f, 0f),
            BuffType.Invincible => Colors.White,
            _ => Colors.Cyan
        });
    }

    public void UpdateShield(bool shieldActive)
    {
        // Append shield status if active
        if (shieldActive)
        {
            _buffLabel.Text += " [SHIELD]";
        }
    }
}
