using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using SelectMOD;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

var dir = Path.Combine(Path.GetTempPath(), "selectmod-contract-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", dir);
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(name); }
void Configure(int value) => File.WriteAllText(Path.Combine(dir, "config.json"), JsonSerializer.Serialize(new { ProbeValue = value }));
void Activate(bool value) => typeof(ModEntry).GetProperty(nameof(ModEntry.IsActive))!.SetValue(null, value);
var harmony = new Harmony(ModEntry.Owner);
try
{
    // Warm the independent fixture methods before applying real test-only Harmony.
    _ = new NCustomRunModifiersList().GetAllModifiers().ToArray();
    _ = new MegaCrit.Sts2.Core.Localization.LocString("test", "key").GetFormattedText();
    harmony.PatchAll(typeof(ModEntry).Assembly);
    Activate(true);
    Configure(42);
    var probe = (SelectSettingsProbe)new NCustomRunModifiersList().GetAllModifiers().OfType<SelectSettingsProbe>().Single();
    Check(probe.SelectProbeSchema == 1 && probe.SelectProbeValue == 42, "choice snapshot");
    Check(probe.Title.GetFormattedText() == "SelectMOD 設定保存テスト", "owned text");
    Check(new MegaCrit.Sts2.Core.Localization.LocString("original", "key").GetFormattedText() == "original:key", "unrelated text");
    var state = new RunState { Modifiers = [probe] };
    var manager = new RunManager();
    manager.SetUpNewSingleplayer(state);
    probe.OnRunCreated(state);
    Check(manager.Starts == 1, "solo start");
    var clone = (SelectSettingsProbe)probe.ToMutable();
    clone.SelectProbeValue = 7;
    Check(probe.SelectProbeValue == 42, "clone independence");
    // Independent property round trip, NOT a substitute for game SavedProperties packets.
    var properties = typeof(SelectSettingsProbe).GetProperties().Where(p => p.IsDefined(typeof(SavedPropertyAttribute))).ToArray();
    var json = JsonSerializer.Serialize(properties.ToDictionary(p => p.Name, p => (int)p.GetValue(probe)!));
    Configure(73);
    var restored = new SelectSettingsProbe();
    foreach (var property in JsonSerializer.Deserialize<Dictionary<string, int>>(json)!)
        typeof(SelectSettingsProbe).GetProperty(property.Key)!.SetValue(restored, property.Value);
    var loadedState = new RunState { Modifiers = [restored] };
    restored.OnRunLoaded(loadedState);
    Check(restored.SelectProbeValue == 42, "saved value wins over next-run preference");
    Check(((SelectSettingsProbe)new NCustomRunModifiersList().GetAllModifiers().OfType<SelectSettingsProbe>().Single()).SelectProbeValue == 73, "next-run preference");
    var saved = new SerializableRun { Modifiers = [new() { Id = ModelDb.GetId<SelectSettingsProbe>(), Value = restored }] };
    RunState.FromSerializable(saved);
    Check(RunState.Restores == 1, "valid restore");
    var zero = new SelectSettingsProbe { SelectProbeSchema = 1, SelectProbeValue = 0 };
    ProbeRules.ValidateSelection([zero], true, GameMode.Standard, 1);
    checks++;
    foreach (var invalid in new[] { new ProbeSettings(-1, 42), new ProbeSettings(2, 42), new ProbeSettings(1, -1), new ProbeSettings(1, 1000001) })
        Reject(invalid.Validate, "invalid schema/value accepted");
    Reject(() => ProbeRules.ValidateSelection([probe, probe], true, GameMode.Standard, 1), "duplicate settings accepted");
    Reject(() => ProbeRules.ValidateSelection([probe], false, GameMode.Standard, 1), "ordinary run accepted");
    Reject(() => ProbeRules.ValidateSelection([probe], true, GameMode.Blitz, 1), "Blitz accepted");
    saved.Modifiers[0].Value = new SelectSettingsProbe();
    Reject(() => RunState.FromSerializable(saved), "missing saved fields accepted");
    Check(RunState.Restores == 1, "invalid restore reached original");
    var lobby = new StartRunLobby { Ready = true };
    lobby.NetService.Type = NetGameType.Host;
    lobby.BeginRunForAllPlayers("seed", [probe]);
    Check(lobby.Starts == 0 && !lobby.Ready, "host start not stopped");
    var load = new LoadRunLobby { Ready = true };
    load.NetService.Type = NetGameType.Host;
    load.BeginRunForAllPlayersIfAllReady();
    Check(load.Starts == 0 && !load.Ready, "host load not stopped");
    Reject(() => manager.SetUpNewMultiplayer(state, lobby), "received multiplayer setup accepted");
    Reject(() => manager.SetUpSavedMultiplayer(state, load), "received multiplayer load accepted");
    Check(manager.Starts == 1, "multiplayer reached original");
    AscensionContractTests.Run(dir, Check, Reject);
    Activate(false);
    Reject(() => ProbeRules.ValidateRun(state), "inactive run accepted");
    Check(!new NCustomRunModifiersList().GetAllModifiers().Any(), "inactive choice exposed");
    var log = File.ReadAllText(Path.Combine(dir, "selectmod.log"));
    Check(log.Contains("RUN_CREATED schema=1 value=42") && log.Contains("RUN_LOADED schema=1 value=42"), "run evidence missing");
    Console.WriteLine($"PASS: {checks} independent contract/Harmony checks. Actual game build, packet round trips and runtime acceptance pending.");
}
finally
{
    foreach (var method in Harmony.GetAllPatchedMethods().ToArray()) harmony.Unpatch(method, HarmonyPatchType.All, ModEntry.Owner);
    Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", null);
    Directory.Delete(dir, recursive: true);
}
