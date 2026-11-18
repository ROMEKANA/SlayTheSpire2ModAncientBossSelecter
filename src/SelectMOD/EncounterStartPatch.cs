using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun), new[] {
    typeof(Player), typeof(bool), typeof(IReadOnlyList<ActModel>), typeof(IReadOnlyList<ModifierModel>),
    typeof(string), typeof(GameMode), typeof(bool), typeof(int), typeof(DateTimeOffset?) })]
internal static class EncounterStartPatch
{
    private static void Prefix(Player __0, bool __1, IReadOnlyList<ActModel> __2, ref IReadOnlyList<ModifierModel> __3,
        GameMode __5, ref bool __6, DateTimeOffset? __8)
    {
        if (!ModEntry.IsActive || !__1 || __5 != GameMode.Standard || __8 != null) return;
        try
        {
            ModEntry.WriteLog("START_ENTER solo standard; character-screen draft");
            if (__3.Any(m => m is SelectAscensionSettings or SelectEncounterSettings or SelectLobbySettings))
                throw new InvalidOperationException("SelectMOD: 新規開始に既存の選択設定が含まれています。");
            if (ModEntry.ReadSelectionDialog())
            {
                if (EncounterSelectionPanel.Failure != null) throw new InvalidOperationException("SelectMOD設定画面にエラーがあります: " + EncounterSelectionPanel.Failure);
                var catalog = EncounterCatalog.Build(__2, __0.UnlockState);
                // Bind index-based UI choices to the ACTUAL randomly selected Act IDs.
                var rules = EncounterDraft.Snapshot(catalog);
                var changed = EncounterRules.HasChanges(rules);
                ModEntry.WriteLog($"SELECTION_CAPTURED changed={changed} rules={EncounterRules.Encode(rules)}");
                if (changed)
                {
                    var settings = (SelectEncounterSettings)ModelDb.Modifier<SelectEncounterSettings>().ToMutable();
                    settings.SelectEncounterSchema = EncounterRules.Schema;
                    settings.SelectEncounterRules = EncounterRules.Encode(rules);
                    __3 = __3.Append(settings).ToArray();
                    __6 = true;
                }
            }
        }
        catch (Exception e) { ModEntry.WriteLog("START_FAILED " + e); throw; }
    }
    private static void Postfix(ref Task<RunState> __result) => __result = Observe(__result);
    private static async Task<RunState> Observe(Task<RunState> original)
    {
        try { var state = await original; ModEntry.WriteLog("START_COMPLETED"); return state; }
        catch (Exception e) { ModEntry.WriteLog("START_FAILED " + e); throw; }
    }
}
