using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

internal static class AscensionPanel
{
    internal static void Add(Control screen, VBoxContainer list, List<Action> refresh)
    {
        var flames = new Label();
        var check = new CheckBox { Name = "SelectMODAscensionCheck" };
        list.AddChild(flames);
        list.AddChild(check);

        void Sync()
        {
            var lobby = MultiplayerSelection.GetLobby(screen);
            var network = MultiplayerSelection.IsNetwork(lobby);
            var level = lobby?.Ascension ?? 0;
            var mask = network ? MultiplayerSelection.Mask(lobby!) : AscensionPreference.Read();
            var available = lobby?.GameMode == GameMode.Standard && level is >= 1 and <= 10;
            check.Disabled = !available || (network && !MultiplayerSelection.Editable(lobby!));
            check.Text = available ? $"A{level} の効果を使う" : "レベルを選ぶと効果を切り替えられます";
            check.SetPressedNoSignal(available && (mask & (1 << (level - 1))) != 0);
            flames.Text = "アセンション  " + string.Join(" ", Enumerable.Range(1, Math.Clamp(level, 0, 10))
                .Select(n => (mask & (1 << (n - 1))) != 0 ? "🔥" : "○"));
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
}
