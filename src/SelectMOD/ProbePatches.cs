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
    private static bool Prefix(StartRunLobby __instance, ref List<ModifierModel> __1)
    {
        try
        {
            if (__instance.NetService.Type != NetGameType.Singleplayer) return MultiplayerSelection.FreezeHost(__instance, ref __1);
            ProbeRules.ValidateSelection(__1, __instance.IsCustom, __instance.GameMode, __instance.Players.Count);
            return true;
        }
        catch (Exception e)
        {
            if (MultiplayerSelection.IsNetwork(__instance)) MultiplayerSelection.Reject(__instance, e.Message);
            else { try { __instance.SetReady(false); } catch (Exception readiness) { ModEntry.WriteLog("UNREADY_FAILED " + readiness.Message); } ModEntry.Reject(e.Message); }
            return false;
        }
    }
}
[HarmonyPatch(typeof(LoadRunLobby), "BeginRunForAllPlayersIfAllReady")]
internal static class LoadLobbyGuard
{
    private static bool Prefix(LoadRunLobby __instance)
    {
        if (!__instance.IsAboutToBeginGame()) return true;
        try { SavedSettingsGuard.Validate(__instance.Run); if (__instance.Run.Modifiers.Any(m => m.Id == ModelDb.GetId<SelectEncounterSettings>() || m.Id == ModelDb.GetId<SelectAscensionSettings>()) && __instance.Players.Count != __instance.Run.Players.Count) throw new InvalidOperationException("SelectMOD: 保存ランの参加者全員が揃ってから再開してください。"); return true; }
        catch (Exception e) { try { __instance.SetReady(false); } catch (Exception readiness) { ModEntry.WriteLog("MP_LOAD_UNREADY_FAILED " + readiness.Message); } ModEntry.Reject(e.Message); return false; }
    }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class NewMultiplayerGuard
{
    private static void Prefix(RunManager __instance, RunState __0, out object? __state)
    {
        __state = null; ProbeRules.ValidateRun(__0);
        __state = EncounterRuntime.Prepare(__instance, __0);
        MultiplayerSelection.Track(__instance, __0);
    }
    private static Exception? Finalizer(RunManager __instance, object? __state, Exception? __exception)
    {
        if (__exception != null && __state != null) { EncounterRuntime.Release(__instance, __state); MultiplayerSelection.Clear(__instance); }
        return __exception;
    }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class SavedMultiplayerGuard
{
    private static void Prefix(RunManager __instance, RunState __0, LoadRunLobby __1, out object? __state)
    {
        __state = null;
        if (__instance.IsInProgress) throw new InvalidOperationException("SelectMOD: 前のランが終了していません。");
        ModEntry.WriteLog("MP_RESUME_ATTEMPT role=" + __1.NetService.Type);
        SavedSettingsGuard.Validate(__1.Run); ProbeRules.ValidateRun(__0);
        if (__0.Modifiers.Any(m => m is SelectEncounterSettings or SelectAscensionSettings) && (__0.Players.Count < 2 || __1.Players.Count != __0.Players.Count)) throw new InvalidOperationException("SelectMOD: 保存ランの参加者全員が必要です。");
        foreach (var settings in __0.Modifiers.OfType<SelectEncounterSettings>()) EncounterRuntime.ValidateLoaded(__0, settings);
        __state = MultiplayerSelection.Track(__instance, __0);
    }
    private static Exception? Finalizer(RunManager __instance, RunState __0, LoadRunLobby __1, object? __state, Exception? __exception, ref Task __result)
    {
        if (__state == null) return __exception;
        if (__exception != null) MultiplayerSelection.Clear(__instance, __state);
        else __result = Observe(__result, __instance, __state, __0, __1);
        return __exception;
    }
    private static async Task Observe(Task task, RunManager manager, object token, RunState state, LoadRunLobby lobby)
    {
        try {
            await task;
            if (!MultiplayerSelection.IsCurrent(manager, token)) return;
            foreach (var model in state.Modifiers.OfType<SelectEncounterSettings>())
            {
                EncounterRuntime.ValidateLoaded(state, model);
                var envelope = MultiplayerContract.Read(model.SelectMultiplayerOrigin);
                var context = MultiplayerContract.Hash(EncounterRules.Encode(new { Kind = "resume", lobby.Run.StartTime, lobby.Run.CurrentActIndex, Players = state.Players.Count, state.AscensionLevel }));
                ModEntry.WriteLog($"MP_CONTRACT_CAPTURED digest={envelope.Digest} role={lobby.NetService.Type} phase=resume");
                ModEntry.WriteLog("MP_START_CONTEXT digest=" + context);
                ModEntry.WriteLog("ROOM_PLAN_DIGEST digest=" + MultiplayerContract.Hash(model.SelectEncounterResults));
                ModEntry.WriteLog("MP_RESUME_COMPLETED digest=" + envelope.Digest + " saved results verified; no reroll");
            }
        }
        catch { MultiplayerSelection.Clear(manager, token); throw; }
    }
}

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
internal static class SoloLoadGuard { private static void Prefix(RunState __0) { ProbeRules.ValidateRun(__0); if (__0.Modifiers.Any(m => m is SelectEncounterSettings or SelectAscensionSettings) && __0.Players.Count != 1) throw new InvalidOperationException("SelectMOD: マルチ保存はマルチで再開してください。"); } }

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class EncounterGeneratePatch
{
    private static void Prefix(RunManager __instance) => EncounterRuntime.BeforeGenerate(__instance);
    private static void Postfix(RunManager __instance) => EncounterRuntime.Resolve(__instance);
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class EncounterCleanupPatch
{
    private static Exception? Finalizer(RunManager __instance, Exception? __exception) { EncounterRuntime.Release(__instance); MultiplayerSelection.Clear(__instance); return __exception; }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpReplay))]
internal static class EncounterReplayGuard
{
    private static void Prefix(RunState __0)
    {
        if (__0.Modifiers.Any(m => m is SelectEncounterSettings or SelectAscensionSettings or SelectLobbySettings)) throw new InvalidOperationException("SelectMOD: この選択ランのリプレイ再生は未対応です。");
    }
}
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class SavedSettingsGuard
{
    private static void Prefix(SerializableRun __0) => Validate(__0);
    internal static void Validate(SerializableRun __0)
    {
        var ids = new[] { ModelDb.GetId<SelectSettingsProbe>(), ModelDb.GetId<SelectAscensionSettings>(), ModelDb.GetId<SelectEncounterSettings>(), ModelDb.GetId<SelectLobbySettings>() };
        var models = __0.Modifiers.Where(m => ids.Contains(m.Id)).Select(ModifierModel.FromSerializable).ToArray();
        ProbeRules.ValidateSelection(models, __0.IsCustomGame, __0.GameMode, __0.Players.Count);
        ProbeRules.ValidateAscension(models, __0.Ascension);
        foreach (var model in models.OfType<SelectEncounterSettings>())
            EncounterRuntime.ValidateSaved(__0, model);
    }
}
