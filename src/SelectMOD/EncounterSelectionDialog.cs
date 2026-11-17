using Godot;
using MegaCrit.Sts2.Core.Models;

namespace SelectMOD;

public static class EncounterSelectionDialog
{
    private sealed record Row(EncounterAct Act, OptionButton Boss, OptionButton Ancient);
    public static Task<EncounterRule[]> Show(EncounterAct[] catalog)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) throw new InvalidOperationException("SelectMOD: 選択画面を開けません。");
        var completion = new TaskCompletionSource<EncounterRule[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new AcceptDialog { Title = "SelectMOD — 開始前のボス・エンシェント選択", DialogHideOnOk = false };
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(740, 420) };
        var description = new Label {
            Text = "各Actの出現先を選んで開始します。標準の抽選も選べます。\n変更する場合はカスタムラン扱いになります。アセンションは通常の累積設定のままです。\n共通エンシェントは解放済み候補から選べます。閉じる場合は変更せず開始します。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        body.AddChild(description);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(740, 320) };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list); body.AddChild(scroll);
        var rows = new List<Row>();
        foreach (var act in catalog)
        {
            list.AddChild(new Label { Text = $"Act {act.Index + 1} — {act.Act.Title.GetFormattedText()}" });
            var boss = AddRow(list, "主ボス", act.Bosses.Select(b => b.Title.GetFormattedText()));
            var ancient = AddRow(list, "エンシェント", act.Ancients.Select(a => a.Title.GetFormattedText()));
            rows.Add(new Row(act, boss, ancient));
        }
        var status = new Label { Text = "A10の第2ボスは標準抽選を維持し、主ボスと重なる場合だけ元の主ボスへ入れ替えます。", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(status); dialog.AddChild(body);
        dialog.GetOkButton().Text = "この設定で開始";
        void Finish(bool useSelection)
        {
            if (completion.Task.IsCompleted) return;
            try
            {
                var rules = rows.Select(row => new EncounterRule(row.Act.Index, EncounterCatalog.Key(row.Act.Act),
                    useSelection && row.Boss.Selected > 0 ? EncounterCatalog.Key(row.Act.Bosses[row.Boss.Selected - 1]) : null,
                    useSelection && row.Ancient.Selected > 0 ? EncounterCatalog.Key(row.Act.Ancients[row.Ancient.Selected - 1]) : null)).ToArray();
                EncounterCatalog.ValidateChoice(rules, catalog);
                completion.TrySetResult(rules);
                dialog.QueueFree();
            }
            catch (Exception e) { status.Text = e.Message; ModEntry.WriteLog("SELECTION_INVALID " + e.Message); }
        }
        dialog.Confirmed += () => Finish(true);
        dialog.Canceled += () => Finish(false);
        dialog.CloseRequested += () => Finish(false);
        dialog.AddButton("変更せず開始", false, "vanilla").Pressed += () => Finish(false);
        // Scene destruction must not leave an unresolved await. If the menu is
        // torn down externally, abort this attempt rather than starting a late run.
        dialog.TreeExiting += () => completion.TrySetCanceled();
        tree.Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(800, 560));
        ModEntry.WriteLog($"SELECTION_SHOWN acts={catalog.Length}");
        return completion.Task;
    }
    private static OptionButton AddRow(VBoxContainer list, string title, IEnumerable<string> items)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(130, 0) });
        var choice = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        choice.AddItem("標準の抽選");
        foreach (var item in items) choice.AddItem(item);
        choice.Select(0); row.AddChild(choice); list.AddChild(row);
        return choice;
    }
}
