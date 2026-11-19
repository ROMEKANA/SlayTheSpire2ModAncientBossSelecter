using Godot;
using MegaCrit.Sts2.Core.Models;

namespace SelectMOD;

public static class EncounterSelectionPanel
{
    private static SceneTree? _tree;
    public static string? Failure { get; private set; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Node, PanelContainer> Panels = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Node, Action> Refreshers = new();
    public static void Refresh(Control screen) { Attach(screen); if (Refreshers.TryGetValue(screen, out var refresh)) refresh(); }
    private static readonly string[] Screens = [
        "MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen"
    ];
    public static void Install()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) throw new InvalidOperationException("SelectMOD: ゲーム画面を取得できません。");
        if (_tree != null) return;
        _tree = tree;
        tree.NodeAdded += Watch;
        // Defer discovery until after the loader registers this assembly.
        Callable.From(() => { if (ReferenceEquals(_tree, tree)) Discover(tree.Root); }).CallDeferred();
    }
    public static void Uninstall() { if (_tree != null) _tree.NodeAdded -= Watch; _tree = null; }
    private static void Discover(Node node) { Watch(node); foreach (var child in node.GetChildren()) Discover(child); }
    private static void Watch(Node node)
    {
        if (node is not Control screen || !Screens.Contains(node.GetType().FullName)) return;
        if (screen.IsNodeReady()) Attach(screen);
        else screen.Ready += () => Attach(screen);
    }
    public static void Attach(Control screen)
    {
        if (Panels.TryGetValue(screen, out _)) return;
        try
        {
            if (!ModEntry.ReadSelectionDialog() && !MultiplayerSelection.IsNetwork(MultiplayerSelection.GetLobby(screen))) return;
            var catalog = EncounterCatalog.BuildOptions();
            var panel = new PanelContainer {
                Name = "SelectMODSelectionPanel", AnchorLeft = 0.075f, AnchorRight = 0.355f,
                AnchorTop = 0.06f, AnchorBottom = 0.06f, OffsetBottom = 0, ZIndex = 50
            };
            var margin = new MarginContainer();
            foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 5);
            var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 2);
            var toggle = new Button { Name = "SelectMODToggle", CustomMinimumSize = new Vector2(0, 30) };
            var content = new VBoxContainer { Name = "SelectMODSelectionContent", Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 2);
            var expanded = false;
            var header = new HBoxContainer();
            var grip = new Button { Name = "SelectMODDrag", Text = "⠿", TooltipText = "ドラッグで移動" };
            toggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            header.AddChild(grip); header.AddChild(toggle);
            body.AddChild(header); body.AddChild(content);
            PanelPosition.Install(screen, panel, grip);
            var list = new VBoxContainer();
            list.AddThemeConstantOverride("separation", 1);
            content.AddChild(list);
            var controls = new List<OptionButton>();
            var refresh = new List<Action>();
            void UpdateStatus()
            {
                var changed = catalog.Any(a => CurrentChoice(screen, a.Index) != (null, null));
                changed |= DoubleBossEnabled(screen) && CurrentSecond(screen) != null;
                toggle.Text = (expanded ? "▼ " : "▶ ") + "SelectMOD設定（" + (changed ? "指定あり" : "ランダム") + "）";
            }
            void ApplyHeight()
            {
                panel.Size = new Vector2(panel.Size.X, 0);
            }
            toggle.Pressed += () => {
                expanded = !expanded;
                content.Visible = expanded;
                ApplyHeight();
                // Containers queue minimum-size updates after hiding children.
                // Reapply after that update so the old content height cannot
                // leave an invisible panel over the game's Embark button.
                Callable.From(ApplyHeight).CallDeferred();
                UpdateStatus();
                ModEntry.WriteLog($"SELECTION_PANEL_TOGGLE expanded={expanded}");
            };
            AscensionPanel.Add(screen, list, refresh);
            foreach (var act in catalog)
            {
                string BossTitle(EncounterModel m) => m.Title.GetFormattedText() + EncounterCatalog.OriginSuffix(act.Index, m);
                AddChoice(screen, list, $"Act {act.Index + 1} エンシェント", act.Index, false, act.Ancients, m => m.Title.GetFormattedText() + EncounterCatalog.OriginSuffix(act.Index, m), controls, refresh, UpdateStatus);
                AddChoice(screen, list, $"Act {act.Index + 1} 主ボス", act.Index, true, act.Bosses, BossTitle, controls, refresh, UpdateStatus);
                if (act.Index == 2)
                    AddChoice(screen, list, "Act 3 副ボス", 2, true, act.Bosses, BossTitle, controls, refresh, UpdateStatus, second: true);
            }
            var reset = new Button { Name = "SelectMODRandomReset", Text = "全てランダムに戻す", CustomMinimumSize = new Vector2(0, 30) };
            reset.Pressed += () => {
                try { var lobby = MultiplayerSelection.GetLobby(screen); if (MultiplayerSelection.IsNetwork(lobby)) MultiplayerSelection.Reset(lobby!); else EncounterDraft.Reset(); foreach (var control in controls) control.Select(0); UpdateStatus(); }
                catch (Exception e) { ModEntry.Reject(e.Message); }
            };
            content.AddChild(reset); margin.AddChild(body); panel.AddChild(margin);
            // Theme overrides do not propagate from a Panel to child controls.
            SetFonts(panel);
            void RefreshView() { foreach (var sync in refresh) sync(); reset.Disabled = !CanEdit(screen); UpdateStatus(); }
            Refreshers.Add(screen, RefreshView);
            screen.VisibilityChanged += () => { if (screen.IsVisibleInTree()) RefreshView(); };
            void LobbyChanged(MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby lobby) { if (ReferenceEquals(lobby, MultiplayerSelection.GetLobby(screen))) RefreshView(); }
            MultiplayerSelection.Changed += LobbyChanged;
            screen.TreeExiting += () => MultiplayerSelection.Changed -= LobbyChanged;
            Panels.Add(screen, panel); screen.AddChild(panel); RefreshView();
            Failure = null;
            ModEntry.WriteLog($"SELECTION_PANEL_ATTACHED screen={screen.GetType().Name} acts={catalog.Length}");
        }
        catch (Exception e) { Failure = e.Message; ModEntry.WriteLog("SELECTION_PANEL_FAILED " + e); ModEntry.Reject("SelectMOD設定画面を作れませんでした。ログを送ってください。\n" + e.Message); }
    }
    private static void SetFonts(Node node)
    {
        if (node is Control control) control.AddThemeFontSizeOverride("font_size", 17);
        foreach (var child in node.GetChildren()) SetFonts(child);
    }
    private static (ModelKey? Boss, ModelKey? Ancient) CurrentChoice(Control screen, int index)
    {
        var lobby = MultiplayerSelection.GetLobby(screen);
        return MultiplayerSelection.IsNetwork(lobby) ? MultiplayerSelection.Choice(lobby!, index) : EncounterDraft.Get(index);
    }
    private static bool DoubleBossEnabled(Control screen)
    {
        var lobby = MultiplayerSelection.GetLobby(screen);
        if (lobby == null || lobby.GameMode != MegaCrit.Sts2.Core.Runs.GameMode.Standard) return false;
        var network = MultiplayerSelection.IsNetwork(lobby);
        var level = network ? MultiplayerSelection.PreviewLevel(lobby) ?? lobby.Ascension : lobby.Ascension;
        return level >= 10 && ((network ? MultiplayerSelection.Mask(lobby) : AscensionPreference.Read()) & 512) != 0;
    }

    private static ModelKey? CurrentSecond(Control screen)
    {
        var lobby = MultiplayerSelection.GetLobby(screen);
        return MultiplayerSelection.IsNetwork(lobby) ? MultiplayerSelection.SecondBoss(lobby!) : EncounterDraft.SecondBoss;
    }

    private static bool CanEdit(Control screen)
    {
        var lobby = MultiplayerSelection.GetLobby(screen);
        return !MultiplayerSelection.IsNetwork(lobby) || MultiplayerSelection.Editable(lobby!);
    }
    private static void AddChoice<T>(Control screen, VBoxContainer list, string label, int index, bool boss, T[] candidates, Func<T, string> title,
        List<OptionButton> controls, List<Action> refresh, Action update, bool second = false) where T : AbstractModel
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(155, 0) });
        var choice = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 30) };
        EncounterChoiceStyle.Apply(choice);
        choice.GetPopup().AddThemeFontSizeOverride("font_size", 17);
        choice.AddItem("ランダム");
        foreach (var candidate in candidates) choice.AddItem(title(candidate));
        void Sync()
        {
            row.Visible = !second || DoubleBossEnabled(screen);
            choice.Disabled = !row.Visible || !CanEdit(screen);
            var current = CurrentChoice(screen, index); var key = second ? CurrentSecond(screen) : boss ? current.Boss : current.Ancient;
            var selected = key == null ? 0 : Array.FindIndex(candidates, c => EncounterCatalog.Key(c) == key) + 1;
            if (key != null && selected == 0) throw new InvalidOperationException("次回候補が見つかりません。ゲームを再起動してください。");
            choice.Select(selected);
        }
        Sync(); refresh.Add(Sync);
        choice.ItemSelected += value => {
            try {
                if (choice.Disabled) return;
                var key = value == 0 ? null : EncounterCatalog.Key(candidates[checked((int)value - 1)]);
                var lobby = MultiplayerSelection.GetLobby(screen);
                if (MultiplayerSelection.IsNetwork(lobby)) MultiplayerSelection.Edit(lobby!, index, boss, key, second); else if (second) EncounterDraft.SecondBoss = key; else EncounterDraft.Set(index, boss, key);
                update();
            } catch (Exception e) { Sync(); ModEntry.Reject(e.Message); }
        };
        controls.Add(choice); row.AddChild(choice); list.AddChild(row);
    }
}
