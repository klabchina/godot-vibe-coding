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
        var font = new FontVariation();
        font.VariationEmbolden = 1.0f;
        AddThemeFontOverride("font", font);
        AddThemeFontSizeOverride("font_size", isCrit ? 36 : 24);
        AddThemeColorOverride("font_color", isCrit ? Colors.Red : Colors.White);
        AddThemeConstantOverride("outline_size", 12);
        AddThemeColorOverride("font_outline_color", Colors.Black);
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
