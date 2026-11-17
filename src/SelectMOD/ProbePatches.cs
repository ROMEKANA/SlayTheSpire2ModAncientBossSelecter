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
        var ascension = (SelectAscensionSettings)ModelDb.Modifier<SelectAscensionSettings>().ToMutable();
        ascension.SelectAscensionSchema = AscensionSettings.CurrentSchema;
        ascension.SelectAscensionMask = ModEntry.ReadNextAscensionMask();
        ModEntry.WriteLog($"ASCENSION_CHOICE mask={ascension.SelectAscensionMask} enabled={ascension.Snapshot.Describe()}");
        __result = __result.Concat(new ModifierModel[] { probe, ascension });
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
            if (__1.OfType<SelectAscensionSettings>().Any()) __instance.SyncAscensionChange(0);
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
    private static void Prefix() => throw new InvalidOperationException("SelectMOD 0.2.0: マルチは未検証のため無効です。");
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class SavedMultiplayerGuard
{
    private static void Prefix() => throw new InvalidOperationException("SelectMOD 0.2.0: マルチ再開は未検証のため無効です。");
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class SoloNewGuard
{
    private static void Prefix(RunManager __instance, RunState __0, out object? __state)
    {
        __state = null;
        ProbeRules.ValidateRun(__0);
        __state = AscensionRuntime.Prepare(__instance, __0);
    }
    private static Exception? Finalizer(RunManager __instance, object? __state, Exception? __exception)
    {
        if (__state != null && __exception != null) AscensionRuntime.Release(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
internal static class SoloLoadGuard
{
    private static void Prefix(RunManager __instance, RunState __0, out object? __state)
    {
        __state = null;
        ProbeRules.ValidateRun(__0);
        __state = AscensionRuntime.Prepare(__instance, __0);
    }
    private static void Postfix(RunManager __instance, object? __state, ref Task __result)
    {
        if (__state != null) __result = ObserveLoad(__result, __instance, __state);
    }
    private static async Task ObserveLoad(Task setup, RunManager manager, object binding)
    {
        try { await setup; }
        catch { AscensionRuntime.Release(manager, binding); throw; }
    }
    private static Exception? Finalizer(RunManager __instance, object? __state, Exception? __exception)
    {
        if (__state != null && __exception != null) AscensionRuntime.Release(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class SavedSettingsGuard
{
    private static void Prefix(SerializableRun __0)
    {
        // Validate before restoring players/rooms or incrementing reload counters.
        var id = ModelDb.GetId<SelectSettingsProbe>();
        var ascensionId = ModelDb.GetId<SelectAscensionSettings>();
        var saved = __0.Modifiers.Where(m => m.Id == id || m.Id == ascensionId).ToArray();
        if (saved.Length == 0) return;
        if (saved.Any(m => m.Id == ascensionId) && __0.Ascension != 0)
            throw new InvalidOperationException("SelectMOD: 保存された個別モードの累積値がA0ではありません。");
        ProbeRules.ValidateSelection(saved.Select(ModifierModel.FromSerializable),
            __0.IsCustomGame, __0.GameMode, __0.Players.Count);
    }
}
