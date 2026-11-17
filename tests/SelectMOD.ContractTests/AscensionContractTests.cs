using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using SelectMOD;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

internal static class AscensionContractTests
{
    public static void Run(string dir, Action<bool, string> check, Action<Action, string> reject)
    {
        for (var mask = 0; mask <= AscensionSettings.All; mask++)
        {
            var model = Model(mask);
            var state = new RunState { Modifiers = [model] };
            var manager = new RunManager();
            var originalOdds = CardRarityOdds.regularCommonOdds;
            manager.SetUpNewSingleplayer(state);
            // Simulate a conflicting cumulative backing list while retaining the
            // same actual manager object: OFF must win, not OR with vanilla true.
            manager.AscensionManager.BaseLevel = 12;
            for (var level = 1; level <= 10; level++)
            {
                var expected = (mask & (1 << (level - 1))) != 0;
                check(manager.AscensionManager.HasLevel((AscensionLevel)level) == expected, "1024 masks / HasLevel");
                check(manager.HasAscension((AscensionLevel)level) == expected, "1024 masks / run helper");
            }
            check(manager.EarlyEffect == ((mask & 4) != 0), "query before InitializeShared postfix");
            check(manager.AscensionManager.HasLevel((AscensionLevel)11), "unknown enum changed");
            check(new AscensionManager(GameMode.Standard, 10).HasLevel(AscensionLevel.SwarmingElites), "unrelated manager changed");
            check(CardRarityOdds.regularCommonOdds == ((mask & 64) != 0 ? 0.615f : 0.6f), "A7 cached probability");
            check(state.InitialEffects.Contains(AscensionLevel.TightBelt) == ((mask & 8) != 0), "initial A4 decision");
            check(state.InitialEffects.Contains(AscensionLevel.AscendersBane) == ((mask & 16) != 0), "initial A5 decision");
            var oldManager = manager.AscensionManager;
            manager.CleanUp();
            check(CardRarityOdds.regularCommonOdds == originalOdds, "A7 restoration");
            check(oldManager.HasLevel(AscensionLevel.SwarmingElites), "manager binding leaked after cleanup");
        }
        reject(() => new AscensionSettings(-1, 0).Validate(), "missing schema accepted");
        reject(() => new AscensionSettings(1, -1).Validate(), "missing mask accepted");
        reject(() => new AscensionSettings(1, 1024).Validate(), "unknown bit accepted");
        var frozen = Model(2);
        frozen.Freeze();
        reject(() => frozen.SelectAscensionMask = 4, "active snapshot mutated");
        reject(() => frozen.SelectAscensionSchema = 3, "active schema mutated");
        var lobby = new StartRunLobby();
        lobby.BeginRunForAllPlayers("seed", [Model(2)]);
        check(lobby.Ascension == 0 && lobby.Starts == 1, "cumulative normalization");
        var plain = new StartRunLobby();
        plain.BeginRunForAllPlayers("seed", []);
        check(plain.Ascension == 10, "unselected cumulative changed");
        reject(() => ProbeRules.ValidateSelection([Model(1), Model(2)], true, GameMode.Standard, 1), "duplicate ascension models");
        reject(() => ProbeRules.ValidateRun(new RunState { AscensionLevel = 10, Modifiers = [Model(2)] }), "invalid saved cumulative value");

        File.WriteAllText(Path.Combine(dir, "config.json"), JsonSerializer.Serialize(new { ProbeValue = 73, AscensionMask = 2 }));
        var choice = new NCustomRunModifiersList().GetAllModifiers().OfType<SelectAscensionSettings>().Single();
        check(choice.Snapshot.Mask == 2, "new choice preference");
        // Reflection-based independent round trip; real SavedProperties/packets
        // are validated on the owner's game, never inferred from this fixture.
        var fields = typeof(SelectAscensionSettings).GetProperties().Where(p => p.IsDefined(typeof(SavedPropertyAttribute)));
        var payload = JsonSerializer.Serialize(fields.ToDictionary(p => p.Name, p => (int)p.GetValue(choice)!));
        File.WriteAllText(Path.Combine(dir, "config.json"), JsonSerializer.Serialize(new { ProbeValue = 73, AscensionMask = 1023 }));
        var loaded = new SelectAscensionSettings();
        foreach (var entry in JsonSerializer.Deserialize<Dictionary<string, int>>(payload)!)
            typeof(SelectAscensionSettings).GetProperty(entry.Key)!.SetValue(loaded, entry.Value);
        var restored = new RunState { Modifiers = [loaded], InitialEffects = { AscensionLevel.TightBelt } };
        var loadManager = new RunManager();
        loadManager.SetUpSavedSingleplayer(restored, new SerializableRun()).GetAwaiter().GetResult();
        check(loadManager.HasAscension(AscensionLevel.WearyTraveler) && !loadManager.HasAscension(AscensionLevel.SwarmingElites), "load used preferences instead of saved mask");
        check(restored.InitialEffects.Count == 1, "load reapplied initial effects");
        loaded.OnRunLoaded(restored);
        loadManager.CleanUp();
        foreach (var mask in new[] { 0, 512 })
        {
            var state = new RunState { Modifiers = [Model(mask)] };
            var manager = new RunManager();
            manager.SetUpNewSingleplayer(state);
            manager.GenerateRooms();
            check(state.Acts.Last().GetRoomsDebug().HasSecondBoss == (mask == 512), "A10 generation path");
            manager.CleanUp();
        }
        var active = new RunManager();
        active.SetUpNewSingleplayer(new RunState { Modifiers = [Model(2)] });
        reject(() => active.SetUpNewSingleplayer(new RunState { Modifiers = [Model(0)] }), "active run replaced");
        check(active.HasAscension(AscensionLevel.WearyTraveler), "failed second setup removed previous binding");
        active.CleanUp();
        var ordinary = new RunManager();
        ordinary.SetUpNewSingleplayer(new RunState { IsCustomGame = false, AscensionLevel = 10 });
        check(ordinary.HasAscension(AscensionLevel.DoubleBoss), "ordinary run changed without selection");
        ordinary.CleanUp();
        var delayed = new TaskCompletionSource();
        var asyncManager = new RunManager { PendingLoad = delayed.Task };
        var oldLoad = asyncManager.SetUpSavedSingleplayer(new RunState { Modifiers = [Model(64)] }, new SerializableRun());
        asyncManager.CleanUp();
        asyncManager.SetUpNewSingleplayer(new RunState { Modifiers = [Model(2)] });
        delayed.SetException(new InvalidOperationException("old async load failed"));
        reject(() => oldLoad.GetAwaiter().GetResult(), "old load failure swallowed");
        check(asyncManager.HasAscension(AscensionLevel.WearyTraveler), "old async failure removed new binding");
        asyncManager.CleanUp();
        var failedLoad = new RunManager { PendingLoad = Task.FromException(new InvalidOperationException("load failed")) };
        var oddsBeforeFailedLoad = CardRarityOdds.regularCommonOdds;
        reject(() => failedLoad.SetUpSavedSingleplayer(new RunState { Modifiers = [Model(64)] }, new SerializableRun()).GetAwaiter().GetResult(), "async load failure swallowed");
        check(!failedLoad.AscensionManager.HasLevel(AscensionLevel.Scarcity) && CardRarityOdds.regularCommonOdds == oddsBeforeFailedLoad, "failed async load leaked bindings or cache");
        failedLoad.CleanUp();
        var legacy = new RunManager();
        legacy.SetUpNewSingleplayer(new RunState { AscensionLevel = 10, Modifiers = [new SelectSettingsProbe { SelectProbeSchema = 1, SelectProbeValue = 42 }] });
        check(legacy.HasAscension(AscensionLevel.DoubleBoss), "legacy probe cumulative ascension changed");
        legacy.CleanUp();
        var badSaved = new SerializableRun { Modifiers = [new() { Id = ModelDb.GetId<SelectAscensionSettings>(), Value = new SelectAscensionSettings() }] };
        reject(() => RunState.FromSerializable(badSaved), "missing new model fields accepted");
        reject(() => new RunManager().SetUpReplay(new RunState { Modifiers = [Model(2)] }), "unsupported replay accepted");
        var foreign = new Harmony("SelectMOD.Tests.Foreign");
        var target = AccessTools.Method(typeof(AscensionManager), nameof(AscensionManager.HasLevel));
        foreign.Patch(target, postfix: new HarmonyMethod(typeof(AscensionContractTests), nameof(ForeignPostfix)));
        try { reject(() => new RunManager().SetUpNewSingleplayer(new RunState { Modifiers = [Model(1)] }), "competing ascension hook accepted"); }
        finally { foreign.Unpatch(target, HarmonyPatchType.All, "SelectMOD.Tests.Foreign"); }
    }
    private static SelectAscensionSettings Model(int mask) => new() { SelectAscensionSchema = 1, SelectAscensionMask = mask };
    private static void ForeignPostfix() { }
}
