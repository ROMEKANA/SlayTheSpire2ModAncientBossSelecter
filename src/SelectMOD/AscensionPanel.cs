using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

internal static class AscensionPanel
{
    internal static void Add(Control screen, VBoxContainer list, List<Action> refresh)
    {
        var flames = new Label();
        var check = new CheckBox { Name = "SelectMODAscensionCheck" };
        var box = new VBoxContainer { Name = "SelectMODAscensionControls" };
        box.AddChild(flames);
        box.AddChild(check);
        var native = FindPanel(screen);
        if (native != null && native is not Container && !native.ClipContents)
        {
            // Use native local coordinates so layout follows the game's UI scale.
            box.AnchorLeft = box.AnchorRight = 1;
            box.OffsetLeft = 8;
            native.AddChild(box);
        }
        else list.AddChild(box);

        void Sync()
        {
            var lobby = MultiplayerSelection.GetLobby(screen);
            var network = MultiplayerSelection.IsNetwork(lobby);
            var level = lobby?.Ascension ?? 0;
            if (network && !MultiplayerSelection.Editable(lobby!) && MultiplayerSelection.PreviewLevel(lobby!) is int preview)
                level = preview;
            var mask = network ? MultiplayerSelection.Mask(lobby!) : AscensionPreference.Read();
            var available = lobby?.GameMode == GameMode.Standard && level is >= 1 and <= 10;
            check.Disabled = !available || (network && !MultiplayerSelection.Editable(lobby!));
            check.Text = available ? $"A{level} の効果を使う" : "レベルを選ぶと効果を切り替えられます";
            check.SetPressedNoSignal(available && (mask & (1 << (level - 1))) != 0);
            var maximum = ReadMaximum(native) ?? ReadMaximum(lobby) ?? level;
            flames.Text = "アセンション  " + string.Join(" ", Enumerable.Range(1, Math.Clamp(Math.Max(level, maximum), 0, 10))
                .Select(n => n > level ? "·" : (mask & (1 << (n - 1))) != 0 ? "🔥" : "○"));
        }
        check.Pressed += () => {
            try
            {
                var lobby = MultiplayerSelection.GetLobby(screen);
                if (lobby == null || check.Disabled || lobby.GameMode != GameMode.Standard || lobby.Ascension is < 1 or > 10) return;
                if (MultiplayerSelection.IsNetwork(lobby) && !MultiplayerSelection.Editable(lobby)) return;
                var bit = 1 << (lobby.Ascension - 1);
                var mask = AscensionPreference.Read();
                AscensionPreference.Write(check.ButtonPressed ? mask | bit : mask & ~bit);
                if (MultiplayerSelection.Editable(lobby)) lobby.SetModifiers(lobby.Modifiers.ToArray());
                Sync();
            }
            catch (Exception e) { ModEntry.Reject(e.Message); }
        };
        refresh.Add(Sync);
    }

    private static int? ReadMaximum(object? source)
    {
        if (source == null) return null;
        var type = source.GetType();
        return (HarmonyLib.AccessTools.Property(type, "MaxAscension")?.GetValue(source)
            ?? HarmonyLib.AccessTools.Field(type, "_maxAscension")?.GetValue(source)) as int?;
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
