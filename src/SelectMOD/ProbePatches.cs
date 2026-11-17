using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SelectMOD;

[HarmonyPatch(typeof(NCustomRunModifiersList), "GetAllModifiers")]
internal static class ProbeListPatch
{
    private static void Postfix(ref IEnumerable<ModifierModel> __result)
    {
        if (!ModEntry.IsActive) return;
        var probe = (SelectSettingsProbe)ModelDb.Modifier<SelectSettingsProbe>().ToMutable();
        probe.SelectProbeSchema = ProbeSettings.CurrentSchema;
        probe.SelectProbeValue = ModEntry.ReadNextValue();
        ModEntry.WriteLog($"CHOICE_READY schema={probe.SelectProbeSchema} value={probe.SelectProbeValue}");
        __result = __result.Concat(new[] { probe });
    }
}

[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers", new[] { typeof(string), typeof(List<ModifierModel>) })]
internal static class NewLobbyGuard
{
    private static bool Prefix(StartRunLobby __instance, List<ModifierModel> __1)
    {
        try
        {
            if (__instance.NetService.Type != NetGameType.Singleplayer)
                throw new InvalidOperationException("このSelectMOD試作ではマルチ開始を停止しています。ソロで試してください。");
            ProbeRules.ValidateSelection(__1, __instance.IsCustom, __instance.GameMode, __instance.Players.Count);
            return true;
        }
        catch (Exception e)
        {
            __instance.SetReady(false);
            ModEntry.Reject(e.Message);
            return false;
        }
    }
}

[HarmonyPatch(typeof(LoadRunLobby), "BeginRunForAllPlayersIfAllReady")]
internal static class LoadLobbyGuard
{
    private static bool Prefix(LoadRunLobby __instance)
    {
        if (__instance.NetService.Type == NetGameType.Singleplayer || !__instance.IsAboutToBeginGame()) return true;
        // SetReady(false) re-enters this method. The not-ready branch above must
        // pass through so the original readiness check terminates that re-entry.
        __instance.SetReady(false);
        ModEntry.Reject("このSelectMOD試作ではマルチ再開を停止しています。");
        return false;
    }
}

// Defense at the actual setup boundary also covers a received begin message.
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class NewMultiplayerGuard
{
    private static void Prefix() => throw new InvalidOperationException("SelectMOD 0.1.0: マルチは未検証のため無効です。");
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class SavedMultiplayerGuard
{
    private static void Prefix() => throw new InvalidOperationException("SelectMOD 0.1.0: マルチ再開は未検証のため無効です。");
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class SoloNewGuard
{
    private static void Prefix(RunState __0) => ProbeRules.ValidateRun(__0);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
internal static class SoloLoadGuard
{
    private static void Prefix(RunState __0) => ProbeRules.ValidateRun(__0);
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class SavedSettingsGuard
{
    private static void Prefix(SerializableRun __0)
    {
        // Validate before restoring players/rooms or incrementing reload counters.
        var id = ModelDb.GetId<SelectSettingsProbe>();
        var saved = __0.Modifiers.Where(m => m.Id == id).ToArray();
        if (saved.Length == 0) return;
        ProbeRules.ValidateSelection(saved.Select(ModifierModel.FromSerializable),
            __0.IsCustomGame, __0.GameMode, __0.Players.Count);
    }
}
