using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using SelectMOD;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

var dir = Path.Combine(Path.GetTempPath(), "selectmod-contract-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", dir);
File.WriteAllText(Path.Combine(dir, "config.json"), "{\"ProbeValue\":73,\"SelectionDialog\":true}");
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(name); }
IEnumerable<T> Descendants<T>(Node root) where T : Node => root.Children.OfType<T>().Concat(root.Children.SelectMany(Descendants<T>));
AcceptDialog Dialog() => Engine.Tree.Root.Children.OfType<AcceptDialog>().Last();
var harmony = new Harmony(ModEntry.Owner);
try
{
    harmony.PatchAll(typeof(ModEntry).Assembly);
    typeof(ModEntry).GetProperty(nameof(ModEntry.IsActive))!.SetValue(null, true);
    var boss1 = ModelDb.Register(new EncounterModel { Id = new("ENCOUNTER", "boss1") });
    var boss2 = ModelDb.Register(new EncounterModel { Id = new("ENCOUNTER", "boss2") });
    var ancient1 = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "ancient1") });
    var ancient2 = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "ancient2") });
    var shared = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "shared") });
    var acts = Enumerable.Range(0, 3).Select(i => ModelDb.Register(new ActModel { Id = new("ACT", "act" + i), Bosses = [boss1, boss2], Ancients = [ancient1, ancient2] })).ToArray();
    var player = new Player(); player.UnlockState.SharedAncients.Add(shared);
    var catalog = EncounterCatalog.Build(acts, player.UnlockState);
    Check(!catalog[0].Ancients.Contains(shared) && catalog[1].Ancients.Contains(shared), "shared eligibility");
    var game = new NGame();
    var start = game.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false, 10);
    Check(!start.IsCompleted && game.Calls == 0 && game.Manager.GameRngDraws == 0, "start did not wait asynchronously before original/RNG");
    var duplicate = game.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false, 10);
    Check(ReferenceEquals(start, duplicate), "duplicate start created second attempt");
    var dialog = Dialog();
    var options = Descendants<OptionButton>(dialog).ToArray();
    Check(options.Length == 6, "missing per-Act controls");
    options[0].Select(2); options[1].Select(2); // Act1 boss2 and ancient2.
    dialog.Confirm(); dialog.Confirm();
    var state = await start.WaitAsync(TimeSpan.FromSeconds(10));
    Check(game.Calls == 1, "original start invoked more than once");
    Check(state.IsCustomGame && state.AscensionLevel == 10, "changed run classification/cumulative ascension");
    Check(state.Acts[0].BossEncounterForTest() == boss2, "selected boss not applied");
    Check(state.Acts[0].GetRoomsDebug().Ancient == ancient2, "selected ancient not applied");
    Check(state.Acts[1].GetRoomsDebug().Boss == boss1, "unselected Act changed");
    Check(game.Manager.GameRngDraws == 7, "selection consumed additional game RNG");
    var settings = state.Modifiers.OfType<SelectEncounterSettings>().Single();
    Check(settings.SelectEncounterResults.Length != 0, "resolved plan not saved");
    Reject(() => settings.SelectEncounterRules = "[]", "frozen rules changed");
    Reject(() => settings.SelectEncounterResults = "[]", "resolved results changed");
    Reject(() => game.Manager.GenerateRooms(), "duplicate room generation accepted");
    Check(game.Manager.GameRngDraws == 7, "duplicate generation advanced RNG");
    // All IDs/results survive an independent JSON-property round trip. This is
    // not game SavedProperties or a real network serializer.
    var props = typeof(SelectEncounterSettings).GetProperties().Where(p => p.IsDefined(typeof(SavedPropertyAttribute)));
    var json = JsonSerializer.Serialize(props.ToDictionary(p => p.Name, p => p.GetValue(settings)));
    var restoredModel = new SelectEncounterSettings { Id = ModelDb.GetId<SelectEncounterSettings>() };
    foreach (var p in JsonDocument.Parse(json).RootElement.EnumerateObject())
    { var property = typeof(SelectEncounterSettings).GetProperty(p.Name)!; property.SetValue(restoredModel, property.PropertyType == typeof(int) ? (object)p.Value.GetInt32() : p.Value.GetString()); }
    var save = new SerializableRun { IsCustomGame = true, Ascension = 10, Modifiers = [new() { Id = restoredModel.Id, Value = restoredModel }] };
    save.Acts = state.Acts.Select(a => new SerializableActModel { Id = a.Id, SerializableRooms = new() { BossId = a.GetRoomsDebug().Boss.Id, SecondBossId = a.GetRoomsDebug().SecondBoss?.Id, AncientId = a.GetRoomsDebug().HasAncient ? a.GetRoomsDebug().Ancient.Id : null } }).ToList();
    var loaded = RunState.FromSerializable(save);
    var loadManager = new RunManager();
    await loadManager.SetUpSavedSingleplayer(loaded, save);
    Check(loadManager.GameRngDraws == 0 && loaded.Acts[0].GetRoomsDebug().Boss == boss2, "load rerolled or lost selected result");
    var before = RunState.Restores;
    save.Acts[0].SerializableRooms.BossId = boss1.Id;
    Reject(() => RunState.FromSerializable(save), "mismatched saved result accepted");
    Check(RunState.Restores == before, "invalid save reached original restoration");
    save.Acts[0].SerializableRooms.BossId = boss2.Id;
    game.Manager.CleanUp(); loadManager.CleanUp();
    // Standard second boss equals selected primary: swap, with no new draw.
    var original = new ResolvedEncounter(2, EncounterCatalog.Key(acts[2]), EncounterCatalog.Key(boss1), EncounterCatalog.Key(boss2), EncounterCatalog.Key(ancient1));
    var rule = new EncounterRule(2, original.Act, EncounterCatalog.Key(boss2), null);
    var plan = EncounterRules.Resolve(rule, original, [EncounterCatalog.Key(boss1), EncounterCatalog.Key(boss2)], []);
    Check(plan.Boss == EncounterCatalog.Key(boss2) && plan.SecondBoss == EncounterCatalog.Key(boss1), "A10 collision handling");
    Check(EncounterRules.Resolve(rule with { Boss = null }, original, [], []) == original, "vanilla rule changed result");
    Reject(() => EncounterRules.Resolve(rule with { Boss = new("ENCOUNTER", "missing") }, original, [], []), "unknown boss accepted");
    Reject(() => EncounterRules.Decode<EncounterRule>("broken"), "broken JSON accepted");
    Reject(() => EncounterRules.Decode<EncounterRule>(new string('x', 32769)), "oversized JSON accepted");
    Reject(() => EncounterRules.Validate([new(1, original.Act, null, null)]), "invalid Act index accepted");
    // Closing the pre-run window is an explicit unchanged start, not a hung task.
    var vanillaGame = new NGame();
    var vanilla = vanillaGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false, 10);
    Dialog().Close();
    var vanillaState = await vanilla.WaitAsync(TimeSpan.FromSeconds(10));
    Check(!vanillaState.IsCustomGame && vanillaState.Modifiers.Count == 0 && vanillaState.AscensionLevel == 10, "unchanged start altered standard run");
    vanillaGame.Manager.CleanUp();
    // External scene teardown must abort, never leave the start waiting forever.
    var abortedGame = new NGame();
    var aborted = abortedGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false);
    Dialog().QueueFree();
    try { await aborted.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("destroyed dialog started a run"); }
    catch (OperationCanceledException) { checks++; }
    Check(abortedGame.Calls == 0, "destroyed dialog reached original");
    var retried = abortedGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false);
    Dialog().Close();
    await retried.WaitAsync(TimeSpan.FromSeconds(10));
    Check(abortedGame.Calls == 1, "aborted attempt left stale start association");
    abortedGame.Manager.CleanUp();
    var disabledGame = new NGame();
    File.WriteAllText(Path.Combine(dir, "config.json"), "{\"ProbeValue\":73,\"SelectionDialog\":false}");
    var disabled = await disabledGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false);
    Check(!disabled.IsCustomGame && !Engine.Tree.Root.Children.OfType<AcceptDialog>().Any(), "disabled dialog still shown");
    disabledGame.Manager.CleanUp();
    Reject(() => ProbeRules.ValidateSelection([new SelectAscensionSettings()], true, GameMode.Standard, 1), "retired ascension accepted");
    var retired = new SerializableRun { IsCustomGame = true, Modifiers = [new() { Id = ModelDb.GetId<SelectAscensionSettings>(), Value = new SelectAscensionSettings { SelectAscensionSchema = 1, SelectAscensionMask = 10 } }] };
    Reject(() => RunState.FromSerializable(retired), "retired save silently converted");
    Check(Harmony.GetAllPatchedMethods().All(m => m.DeclaringType != typeof(MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager)), "ascension hook still active");
    Check(new MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager().HasLevel(MegaCrit.Sts2.Core.Entities.Ascension.AscensionLevel.TightBelt), "standard ascension changed");
    Check(MegaCrit.Sts2.Core.Odds.CardRarityOdds.regularCommonOdds == 0.6f, "A7 cache altered");
    var host = new MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby { Ready = true };
    host.NetService.Type = MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host;
    host.BeginRunForAllPlayers("seed", []);
    Check(host.Starts == 0 && !host.Ready, "multiplayer not blocked");
    var log = File.ReadAllText(Path.Combine(dir, "selectmod.log"));
    Check(log.Contains("START_COMPLETED") && log.Contains("ENCOUNTER_RUN_LOADED"), "progress diagnostics missing");
    Console.WriteLine($"PASS: {checks} independent selector/UI-task/Harmony checks. Actual game build, UI and gameplay acceptance pending.");
}
finally
{
    foreach (var method in Harmony.GetAllPatchedMethods().ToArray()) harmony.Unpatch(method, HarmonyPatchType.All, ModEntry.Owner);
    Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", null); Directory.Delete(dir, true);
}
static class TestAccess { public static EncounterModel BossEncounterForTest(this ActModel act) => act.GetRoomsDebug().Boss; }
