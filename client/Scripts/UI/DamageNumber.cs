using Godot;

namespace Game.UI;

/// <summary>
/// Floating damage number that rises and fades out.
/// Add as child of a CanvasLayer or Control, then call Show().
/// </summary>
public partial class DamageNumber : Label
{
    private float _lifetime = 0.8f;
    private float _elapsed;
    private Vector2 _velocity = new(0, -60); // drift upward

    public void Show(Vector2 screenPos, int damage, bool isCrit = false)
    {
        Text = damage.ToString();
        Position = screenPos;
        AddThemeFontSizeOverride("font_size", isCrit ? 24 : 18);
        AddThemeColorOverride("font_color", isCrit ? Colors.Red : Colors.White);
        _elapsed = 0;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        float t = _elapsed / _lifetime;

        if (t >= 1f)
        {
            QueueFree();
            return;
        }

        Position += _velocity * (float)delta;
        Modulate = new Color(1, 1, 1, 1f - t); // fade out
    }
}
