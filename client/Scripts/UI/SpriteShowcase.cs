using Godot;

namespace Game.UI;

/// <summary>
/// Test scene that displays all character animations in a grid:
/// Max 5 rows per group; overflow wraps into additional column groups to the right.
/// </summary>
public partial class SpriteShowcase : Node2D
{
    private const float CellW = 280f;
    private const float CellH = 220f;
    private const float OriginX = 100f;
    private const float OriginY = 80f;
    private const float FrameInterval = 0.15f; // seconds per frame
    private const int FrameCount = 5;
    private const float SpriteScale = 2f;
    private const int MaxRows = 5;
    private const float GroupGap = 60f; // horizontal gap between column groups

    // Row definitions: name, sprite path pattern, actions
    private static readonly RowDef[] Rows =
    {
        new("Archer",   "res://Assets/Sprites/Roles/archer_{0}_{1}.png",   new[] { "idle", "walk", "attack" }),
        new("Slime",    "res://Assets/Sprites/Enemies/slime_{0}_{1}.png",  new[] { "walk", "attack", "death" }),
        new("Skeleton", "res://Assets/Sprites/Enemies/skeleton_{0}_{1}.png", new[] { "walk", "attack", "death" }),
        new("Orc",      "res://Assets/Sprites/Enemies/orc_{0}_{1}.png",    new[] { "walk", "attack", "death" }),
        new("Elite",    "res://Assets/Sprites/Enemies/elite_{0}_{1}.png",  new[] { "walk", "attack", "death" }),
        new("Boss",     "res://Assets/Sprites/Enemies/boss_{0}_{1}.png",   new[] { "walk", "attack", "death" }),
    };

    private record RowDef(string Name, string PathPattern, string[] Actions);

    private record AnimCell(Sprite2D Sprite, Texture2D[] Frames, int CurrentFrame);

    private readonly System.Collections.Generic.List<AnimCell> _cells = new();
    private float _timer;

    public override void _Ready()
    {
        int actionCols = Rows.Length > 0 ? Rows[0].Actions.Length : 3;
        int groupCount = (Rows.Length + MaxRows - 1) / MaxRows;
        int displayRows = Rows.Length < MaxRows ? Rows.Length : MaxRows;
        float groupWidth = CellW * actionCols + OriginX + GroupGap;

        // Dark background
        var bg = new ColorRect
        {
            Color = new Color(0.12f, 0.12f, 0.15f),
            Position = new Vector2(-20, -20),
            Size = new Vector2(groupWidth * groupCount + 40, CellH * displayRows + OriginY + 60)
        };
        AddChild(bg);

        // Title
        var title = new Label { Text = "Sprite Showcase — All Animations" };
        title.AddThemeColorOverride("font_color", Colors.White);
        title.AddThemeFontSizeOverride("font_size", 20);
        title.Position = new Vector2(OriginX, 10);
        AddChild(title);

        for (int i = 0; i < Rows.Length; i++)
        {
            var def = Rows[i];
            int group = i / MaxRows;
            int row = i % MaxRows;
            float groupOffsetX = group * groupWidth;

            // Row label
            var rowLabel = new Label { Text = def.Name };
            rowLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            rowLabel.AddThemeFontSizeOverride("font_size", 14);
            rowLabel.Position = new Vector2(groupOffsetX + 5, OriginY + row * CellH + CellH * 0.4f);
            AddChild(rowLabel);

            for (int col = 0; col < def.Actions.Length; col++)
            {
                string action = def.Actions[col];
                float x = groupOffsetX + OriginX + col * CellW;
                float y = OriginY + row * CellH;

                // Column header (only on first row of each group)
                if (row == 0)
                {
                    var colLabel = new Label { Text = action };
                    colLabel.AddThemeColorOverride("font_color", Colors.LightGray);
                    colLabel.AddThemeFontSizeOverride("font_size", 12);
                    colLabel.Position = new Vector2(x + CellW * 0.35f, OriginY - 20);
                    AddChild(colLabel);
                }

                // Load frames
                var frames = new Texture2D[FrameCount];
                bool allLoaded = true;
                for (int f = 0; f < FrameCount; f++)
                {
                    string path = string.Format(def.PathPattern, action, f + 1);
                    var tex = GD.Load<Texture2D>(path);
                    if (tex == null)
                    {
                        allLoaded = false;
                        GD.PrintErr($"SpriteShowcase: missing {path}");
                        break;
                    }
                    frames[f] = tex;
                }

                if (!allLoaded) continue;

                // Sprite
                var sprite = new Sprite2D
                {
                    Texture = frames[0],
                    Position = new Vector2(x + CellW * 0.5f, y + CellH * 0.55f),
                    Scale = new Vector2(SpriteScale, SpriteScale)
                };
                AddChild(sprite);

                _cells.Add(new AnimCell(sprite, frames, 0));

                // Cell border
                var border = new ColorRect
                {
                    Color = new Color(0.3f, 0.3f, 0.35f, 0.5f),
                    Position = new Vector2(x, y),
                    Size = new Vector2(CellW - 4, CellH - 4)
                };
                AddChild(border);
                MoveChild(border, bg.GetIndex() + 1); // behind sprites
            }
        }
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer < FrameInterval) return;
        _timer -= FrameInterval;

        for (int i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            int next = (cell.CurrentFrame + 1) % cell.Frames.Length;
            cell.Sprite.Texture = cell.Frames[next];
            _cells[i] = cell with { CurrentFrame = next };
        }
    }
}
