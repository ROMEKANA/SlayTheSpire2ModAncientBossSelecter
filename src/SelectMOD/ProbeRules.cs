using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

public static class ProbeRules
{
    public static void ValidateSelection(IEnumerable<ModifierModel> modifiers, bool isCustom, GameMode mode, int players)
    {
        var probes = modifiers.OfType<SelectSettingsProbe>().ToArray();
        if (probes.Length == 0) return;
        if (!ModEntry.IsActive) throw new InvalidOperationException("SelectMODは初期化されていません。ゲームを終了してログを確認してください。");
        if (probes.Length != 1) throw new InvalidOperationException("SelectMODの設定が重複しています。");
        if (!isCustom || mode != GameMode.Standard || players != 1)
            throw new InvalidOperationException("SelectMODの試作はソロの標準カスタムラン専用です。");
        probes[0].Snapshot.Validate();
    }

    public static void ValidateRun(RunState state) =>
        ValidateSelection(state.Modifiers, state.IsCustomGame, state.GameMode, state.Players.Count);
}
