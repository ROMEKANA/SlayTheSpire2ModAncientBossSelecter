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
                Name = "SelectMODSelectionPanel", AnchorLeft = 0.64f, AnchorRight = 0.985f,
                AnchorTop = 0.06f, AnchorBottom = 0.06f, OffsetBottom = 72, ZIndex = 50
            };
            var margin = new MarginContainer();
            foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 14);
            var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 10);
            var toggle = new Button { Name = "SelectMODToggle", CustomMinimumSize = new Vector2(0, 44) };
            var content = new VBoxContainer { Name = "SelectMODSelectionContent", Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 10);
            var expanded = false;
            body.AddChild(toggle); body.AddChild(content);
            var info = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            content.AddChild(info);
            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            list.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(list); content.AddChild(scroll);
            var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            var controls = new List<OptionButton>();
            var refresh = new List<Action>();
            void UpdateStatus()
            {
                var changed = catalog.Any(a => CurrentChoice(screen, a.Index) != (null, null));
                status.Text = changed ? "指定あり：この設定で開始します（カスタム扱い）" : "全てランダム：ゲーム標準の抽選で開始します";
                var lobby = MultiplayerSelection.GetLobby(screen);
                info.Text = (MultiplayerSelection.IsNetwork(lobby) ? MultiplayerSelection.Status(lobby!) : "ソロ：選ぶと即反映。指定したランはカスタム扱い。") + "\n全Actから選択可能。[別Act]は実験機能。";
                toggle.Text = (expanded ? "▼ " : "▶ ") + "SelectMOD設定（" + (changed ? "指定あり" : "ランダム") + "）";
            }
            void ApplyHeight()
            {
                panel.AnchorBottom = expanded ? 0.83f : panel.AnchorTop;
                panel.OffsetBottom = expanded ? 0 : 72;
                if (!expanded) panel.Size = new Vector2(panel.Size.X, 72);
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
                list.AddChild(new Label { Text = $"Act {act.Index + 1}" });
                AddChoice(screen, list, "主ボス", act.Index, true, act.Bosses, candidate => (EncounterCatalog.IsNative(act.Index, candidate) ? "" : "[別Act] ") + candidate.Title.GetFormattedText(), controls, refresh, UpdateStatus);
                AddChoice(screen, list, "エンシェント", act.Index, false, act.Ancients, candidate => (EncounterCatalog.IsNative(act.Index, candidate) ? "" : "[別Act] ") + candidate.Title.GetFormattedText(), controls, refresh, UpdateStatus);
            }
            var reset = new Button { Name = "SelectMODRandomReset", Text = "全てランダムに戻す", CustomMinimumSize = new Vector2(0, 44) };
            reset.Pressed += () => {
                try { var lobby = MultiplayerSelection.GetLobby(screen); if (MultiplayerSelection.IsNetwork(lobby)) MultiplayerSelection.Reset(lobby!); else EncounterDraft.Reset(); foreach (var control in controls) control.Select(0); UpdateStatus(); }
                catch (Exception e) { ModEntry.Reject(e.Message); }
            };
            content.AddChild(status); content.AddChild(reset); margin.AddChild(body); panel.AddChild(margin);
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
        if (node is Control control) control.AddThemeFontSizeOverride("font_size", 23);
        foreach (var child in node.GetChildren()) SetFonts(child);
    }
    private static (ModelKey? Boss, ModelKey? Ancient) CurrentChoice(Control screen, int index)
    {
        var lobby = MultiplayerSelection.GetLobby(screen);
        return MultiplayerSelection.IsNetwork(lobby) ? MultiplayerSelection.Choice(lobby!, index) : EncounterDraft.Get(index);
    }
    private static bool CanEdit(Control screen)
    {
        var lobby = MultiplayerSelection.GetLobby(screen);
        return !MultiplayerSelection.IsNetwork(lobby) || (lobby!.IsCustom && MultiplayerSelection.Editable(lobby));
    }
    private static void AddChoice<T>(Control screen, VBoxContainer list, string label, int index, bool boss, T[] candidates, Func<T, string> title,
        List<OptionButton> controls, List<Action> refresh, Action update) where T : AbstractModel
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(125, 0) });
        var choice = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
        choice.GetPopup().AddThemeFontSizeOverride("font_size", 23);
        choice.AddItem("ランダム");
        foreach (var candidate in candidates) choice.AddItem(title(candidate));
        void Sync()
        {
            choice.Disabled = !CanEdit(screen);
            var current = CurrentChoice(screen, index); var key = boss ? current.Boss : current.Ancient;
            var selected = key == null ? 0 : Array.FindIndex(candidates, c => EncounterCatalog.Key(c) == key) + 1;
            if (key != null && selected == 0) throw new InvalidOperationException("次回候補が見つかりません。ゲームを再起動してください。");
            choice.Select(selected);
        }
        Sync(); refresh.Add(Sync);
        choice.ItemSelected += value => {
            try {
                var key = value == 0 ? null : EncounterCatalog.Key(candidates[checked((int)value - 1)]);
                var lobby = MultiplayerSelection.GetLobby(screen);
                if (MultiplayerSelection.IsNetwork(lobby)) MultiplayerSelection.Edit(lobby!, index, boss, key); else EncounterDraft.Set(index, boss, key);
                update();
            } catch (Exception e) { Sync(); ModEntry.Reject(e.Message); }
        };
        controls.Add(choice); row.AddChild(choice); list.AddChild(row);
    }
}
