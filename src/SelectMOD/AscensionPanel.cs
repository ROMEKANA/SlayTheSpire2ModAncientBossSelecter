using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

internal static class AscensionPanel
{
    internal static void Add(Control screen, VBoxContainer list, List<Action> refresh)
    {
        var box = new HBoxContainer { Name = "SelectMODAscensionControls" };
        box.AddThemeConstantOverride("separation", 0);
        var buttons = new Button[10];
        foreach (var n in Enumerable.Range(1, buttons.Length))
        {
            var button = new Button { Name = $"SelectMODFlame{n}", ClipText = true, CustomMinimumSize = new Vector2(32, 34) };
            button.AddThemeFontSizeOverride("font_size", 16);
            // The wrapper keeps the slot even when an unavailable button is hidden.
            var slot = new VBoxContainer { CustomMinimumSize = new Vector2(32, 34) };
            slot.AddChild(button);
            box.AddChild(slot);
            button.Pressed += () => Toggle(n);
            buttons[n - 1] = button;
        }
        var native = FindPanel(screen);
        var custom = screen.GetType().Name == "NCustomRunScreen";
        var besideNative = !custom && native != null && native is not Container && !native.ClipContents;
        if (besideNative)
        {
            // Use native local coordinates so layout follows the game's UI scale.
            box.AnchorLeft = box.AnchorRight = 1;
            box.OffsetLeft = 8;
        }
        if (custom)
        {
            box.AnchorLeft = box.AnchorRight = 0.32f;
            box.AnchorTop = box.AnchorBottom = 0.88f;
            box.ZIndex = 51;
        }
        var parent = custom ? screen : besideNative ? native! : (Control)list;
#if SELECTMOD_BASELIB
        BaseLib.Extensions.NodeExtensions.AddUnique(parent, box, "SelectMODAscensionControls");
#else
        parent.AddChild(box);
#endif

        if (custom)
        {
            // Both the stock row and BaseLib's optional scroll wrapper live under CharSelectButtons.
            void PlaceBelowCharacters()
            {
                var group = screen.GetNodeOrNull<Control>("LeftContainer/CharSelectButtons");
                var icons = group?.GetNodeOrNull<Control>("ButtonScrollContainer") ?? group?.GetNodeOrNull<Control>("ButtonContainer");
                if (icons == null) return;
                var bounds = icons.GetGlobalRect();
                if (bounds.Size.X > 0 && bounds.Size.Y > 0)
                    box.GlobalPosition = new Vector2(bounds.Position.X, bounds.End.Y + 4);
            }
            refresh.Add(PlaceBelowCharacters);
            screen.Resized += () => Callable.From(PlaceBelowCharacters).CallDeferred();
            Callable.From(PlaceBelowCharacters).CallDeferred();
        }

        void Sync()
        {
            var lobby = MultiplayerSelection.GetLobby(screen);
            var network = MultiplayerSelection.IsNetwork(lobby);
            var level = lobby?.Ascension ?? 0;
            if (network && !MultiplayerSelection.Editable(lobby!) && MultiplayerSelection.PreviewLevel(lobby!) is int preview)
                level = preview;
            var mask = network ? MultiplayerSelection.Mask(lobby!) : AscensionPreference.Read();
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var enabled = (mask & (1 << i)) != 0;
                button.Visible = lobby?.GameMode == GameMode.Standard && i < level;
                button.Disabled = !button.Visible || (network && !MultiplayerSelection.Editable(lobby!));
                button.Text = enabled && button.Visible ? "🔥" : "";
                button.TooltipText = $"A{i + 1}  {(enabled ? "ON" : "OFF")}";
            }
        }

        void Toggle(int n)
        {
            try
            {
                var lobby = MultiplayerSelection.GetLobby(screen);
                if (lobby == null || lobby.GameMode != GameMode.Standard || n > lobby.Ascension) return;
                if (MultiplayerSelection.IsNetwork(lobby) && !MultiplayerSelection.Editable(lobby)) return;
                AscensionPreference.Write(AscensionPreference.Read() ^ (1 << (n - 1)));
                if (MultiplayerSelection.Editable(lobby)) lobby.SetModifiers(lobby.Modifiers.ToArray());
                EncounterSelectionPanel.Refresh(screen);
            }
            catch (Exception e) { ModEntry.Reject(e.Message); }
        }
        refresh.Add(Sync);
    }

    private static Control? FindPanel(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control panel && panel.GetType().Name == "NAscensionPanel") return panel;
            if (FindPanel(child) is { } nested) return nested;
        }
        return null;
    }
}
