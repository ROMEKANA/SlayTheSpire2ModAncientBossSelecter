using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
namespace SelectMOD;
public static class ProbeRules
{
    public static void ValidateSelection(IEnumerable<ModifierModel> modifiers, bool isCustom, GameMode mode, int players)
    {
        var models = modifiers.ToArray();
        if (models.OfType<SelectAscensionSettings>().Any()) throw new InvalidOperationException("SelectMOD: 0.2の個別アセンションは停止中です。この保存ランは変更せず、新規ランでボス選択を試してください。");
        var probes = models.OfType<SelectSettingsProbe>().ToArray();
        var encounters = models.OfType<SelectEncounterSettings>().ToArray();
        if (probes.Length == 0 && encounters.Length == 0) return;
        if (!ModEntry.IsActive) throw new InvalidOperationException("SelectMODは初期化されていません。");
        if (probes.Length > 1 || encounters.Length > 1) throw new InvalidOperationException("SelectMOD: 設定が重複しています。");
        if (!isCustom || mode != GameMode.Standard || players != 1) throw new InvalidOperationException("SelectMOD: 選択ランはソロの標準モード専用です。");
        if (probes.Length == 1) probes[0].Snapshot.Validate();
        if (encounters.Length == 1) encounters[0].ReadRules();
    }
    public static void ValidateRun(RunState state) => ValidateSelection(state.Modifiers, state.IsCustomGame, state.GameMode, state.Players.Count);
}
