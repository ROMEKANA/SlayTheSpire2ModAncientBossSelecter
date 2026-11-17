using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SelectMOD;

[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers", new[] { typeof(string), typeof(List<ModifierModel>) })]
internal static class NewLobbyGuard
{
    private static bool Prefix(StartRunLobby __instance, List<ModifierModel> __1)
    {
        try
        {
            if (__instance.NetService.Type != NetGameType.Singleplayer) throw new InvalidOperationException("SelectMOD 0.4.0はソロ専用です。");
            ProbeRules.ValidateSelection(__1, __instance.IsCustom, __instance.GameMode, __instance.Players.Count);
            return true;
        }
        catch (Exception e) { __instance.SetReady(false); ModEntry.Reject(e.Message); return false; }
    }
}
[HarmonyPatch(typeof(LoadRunLobby), "BeginRunForAllPlayersIfAllReady")]
internal static class LoadLobbyGuard
{
    private static bool Prefix(LoadRunLobby __instance)
    {
        if (__instance.NetService.Type == NetGameType.Singleplayer || !__instance.IsAboutToBeginGame()) return true;
        __instance.SetReady(false); ModEntry.Reject("SelectMOD 0.4.0はソロ専用です。"); return false;
    }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class NewMultiplayerGuard { private static void Prefix() => throw new InvalidOperationException("SelectMOD: マルチは未対応です。"); }
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class SavedMultiplayerGuard { private static void Prefix() => throw new InvalidOperationException("SelectMOD: マルチ再開は未対応です。"); }

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class SoloNewGuard
{
    private static void Prefix(RunManager __instance, RunState __0, out object? __state)
    {
        __state = null; ProbeRules.ValidateRun(__0); __state = EncounterRuntime.Prepare(__instance, __0);
    }
    private static Exception? Finalizer(RunManager __instance, object? __state, Exception? __exception)
    {
        if (__state != null && __exception != null) EncounterRuntime.Release(__instance, __state);
        return __exception;
    }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
internal static class SoloLoadGuard { private static void Prefix(RunState __0) => ProbeRules.ValidateRun(__0); }

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class EncounterGeneratePatch
{
    private static void Prefix(RunManager __instance) => EncounterRuntime.BeforeGenerate(__instance);
    private static void Postfix(RunManager __instance) => EncounterRuntime.Resolve(__instance);
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class EncounterCleanupPatch
{
    private static Exception? Finalizer(RunManager __instance, Exception? __exception) { EncounterRuntime.Release(__instance); return __exception; }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpReplay))]
internal static class EncounterReplayGuard
{
    private static void Prefix(RunState __0)
    {
        if (__0.Modifiers.Any(m => m is SelectEncounterSettings or SelectAscensionSettings)) throw new InvalidOperationException("SelectMOD: この選択ランのリプレイ再生は未対応です。");
    }
}
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class SavedSettingsGuard
{
    private static void Prefix(SerializableRun __0)
    {
        var ids = new[] { ModelDb.GetId<SelectSettingsProbe>(), ModelDb.GetId<SelectAscensionSettings>(), ModelDb.GetId<SelectEncounterSettings>() };
        var models = __0.Modifiers.Where(m => ids.Contains(m.Id)).Select(ModifierModel.FromSerializable).ToArray();
        ProbeRules.ValidateSelection(models, __0.IsCustomGame, __0.GameMode, __0.Players.Count);
        foreach (var model in models.OfType<SelectEncounterSettings>()) EncounterRuntime.ValidateSaved(__0, model);
    }
}
