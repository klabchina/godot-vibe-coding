using Godot;

namespace Game.Utils;

/// <summary>
/// Centralized logging utility with runtime toggle.
/// </summary>
public static class GameLogger
{
    private static bool _enabled = true;

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public static void Print(string message)
    {
        if (_enabled)
            GD.Print(message);
    }

    public static void PrintRich(string message)
    {
        if (_enabled)
            GD.PrintRich(message);
    }

    public static void PrintErr(string message)
    {
        if (_enabled)
            GD.PrintErr(message);
    }
}
