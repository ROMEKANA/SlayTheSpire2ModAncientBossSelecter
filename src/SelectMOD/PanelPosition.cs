using System.Text.Json;
using Godot;

namespace SelectMOD;

internal static class PanelPosition
{
    private sealed record Position(float X, float Y);

    internal static void Install(Control screen, PanelContainer panel, Button grip)
    {
        var path = Path.Combine(ModEntry.ConfigDirectory, "panel-position.json");
        const float width = 0.28f;
        void Place(Position p)
        {
            panel.AnchorLeft = Math.Clamp(p.X, 0, 1 - width);
            panel.AnchorRight = panel.AnchorLeft + width;
            var maxY = screen.Size.Y > 0 ? Math.Max(0, 1 - panel.Size.Y / screen.Size.Y) : 1;
            panel.AnchorTop = panel.AnchorBottom = Math.Clamp(p.Y, 0, maxY);
            panel.OffsetLeft = panel.OffsetRight = panel.OffsetTop = panel.OffsetBottom = 0;
        }
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Position>(File.ReadAllText(path)) is { } p &&
                float.IsFinite(p.X) && float.IsFinite(p.Y)) Place(p);
        }
        catch (Exception e) { ModEntry.WriteLog("PANEL_POSITION_READ " + e.Message); }

        bool dragging = false;
        grip.GuiInput += input => {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
            {
                if (dragging && !click.Pressed)
                {
                    var temp = path + "." + Guid.NewGuid().ToString("N");
                    try
                    {
                        Directory.CreateDirectory(ModEntry.ConfigDirectory);
                        File.WriteAllText(temp, JsonSerializer.Serialize(new Position(panel.AnchorLeft, panel.AnchorTop)));
                        File.Move(temp, path, true);
                    }
                    catch (Exception e) { ModEntry.WriteLog("PANEL_POSITION_WRITE " + e.Message); }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                dragging = click.Pressed;
            }
            else if (dragging && input is InputEventMouseMotion motion && screen.Size.X > 0 && screen.Size.Y > 0)
                Place(new(panel.AnchorLeft + motion.Relative.X / screen.Size.X, panel.AnchorTop + motion.Relative.Y / screen.Size.Y));
        };
    }
}
