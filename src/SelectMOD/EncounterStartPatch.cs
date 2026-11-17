using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace SelectMOD;

[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun), new[] {
    typeof(Player), typeof(bool), typeof(IReadOnlyList<ActModel>), typeof(IReadOnlyList<ModifierModel>),
    typeof(string), typeof(GameMode), typeof(bool), typeof(int), typeof(DateTimeOffset?) })]
internal static class EncounterStartPatch
{
    private sealed class Attempt { public Task<RunState> Task = null!; }
    private static readonly ConditionalWeakTable<NGame, Attempt> Pending = new();
    [ThreadStatic] private static NGame? _continuing;

    private static bool Prefix(NGame __instance, Player __0, bool __1, IReadOnlyList<ActModel> __2,
        IReadOnlyList<ModifierModel> __3, string __4, GameMode __5, bool __6, int __7, DateTimeOffset? __8, ref Task<RunState> __result)
    {
        if (ReferenceEquals(_continuing, __instance) || !ModEntry.IsActive || !__1 || __5 != GameMode.Standard || __8 != null) return true;
        if (Pending.TryGetValue(__instance, out var existing)) { __result = existing.Task; return false; }
        var attempt = new Attempt(); Pending.Add(__instance, attempt);
        attempt.Task = Start(__instance, __0, __1, __2, __3, __4, __5, __6, __7, __8);
        __result = attempt.Task;
        return false;
    }
    private static async Task<RunState> Start(NGame game, Player player, bool shouldSave, IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers, string seed, GameMode mode, bool isCustom, int ascension, DateTimeOffset? daily)
    {
        try
        {
            ModEntry.WriteLog("START_ENTER solo standard; ascension hooks disabled");
            if (modifiers.OfType<SelectAscensionSettings>().Any()) throw new InvalidOperationException("SelectMOD: 0.2の個別アセンションは停止中です。新規ランで試してください。");
            if (modifiers.OfType<SelectEncounterSettings>().Any()) throw new InvalidOperationException("SelectMOD: 選択処理の再入を停止しました。");
            var effective = modifiers;
            if (ModEntry.ReadSelectionDialog())
            {
                var catalog = EncounterCatalog.Build(acts, player.UnlockState);
                var rules = await EncounterSelectionDialog.Show(catalog);
                EncounterCatalog.ValidateChoice(rules, catalog);
                if (EncounterRules.HasChanges(rules))
                {
                    var settings = (SelectEncounterSettings)ModelDb.Modifier<SelectEncounterSettings>().ToMutable();
                    settings.SelectEncounterSchema = EncounterRules.Schema;
                    settings.SelectEncounterRules = EncounterRules.Encode(rules);
                    effective = modifiers.Append(settings).ToArray();
                    isCustom = true; // Preserve standard custom-run achievement/progression policy.
                }
                ModEntry.WriteLog($"SELECTION_CONFIRMED changed={EncounterRules.HasChanges(rules)}");
            }
            Task<RunState> original;
            var previous = _continuing;
            try
            {
                _continuing = game;
                ModEntry.WriteLog("START_ORIGINAL");
                original = game.StartNewSingleplayerRun(player, shouldSave, acts, effective, seed, mode, isCustom, ascension, daily);
            }
            finally { _continuing = previous; }
            var state = await original;
            ModEntry.WriteLog("START_COMPLETED");
            return state;
        }
        catch (Exception e) { ModEntry.WriteLog("START_FAILED " + e); throw; }
        finally { Pending.Remove(game); }
    }
}
