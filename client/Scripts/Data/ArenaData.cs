using Godot;

namespace Game.Data;

/// <summary>
/// Arena size derived from project viewport settings.
/// </summary>
public static class ArenaData
{
    public static Vector2 Size { get; private set; }

    static ArenaData()
    {
        int w = (int)ProjectSettings.GetSetting("display/window/size/viewport_width");
        int h = (int)ProjectSettings.GetSetting("display/window/size/viewport_height");
        Size = new Vector2(w, h);
    }
}
