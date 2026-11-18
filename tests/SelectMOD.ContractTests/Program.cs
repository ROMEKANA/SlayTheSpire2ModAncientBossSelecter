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

if (args.Length == 3 && args[0] == "--multiplayer-peer") { await MultiplayerTests.RunProcessPeer(args[1], args[2]); return; }

var dir = Path.Combine(Path.GetTempPath(), "selectmod-contract-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", dir);
File.WriteAllText(Path.Combine(dir, "config.json"), "{\"ProbeValue\":73,\"SelectionDialog\":true}");
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(name); }
IEnumerable<T> Descendants<T>(Node root) where T : Node => root.Children.OfType<T>().Concat(root.Children.SelectMany(Descendants<T>));
var harmony = new Harmony(ModEntry.Owner);
try
{
    var configPath = Path.Combine(dir, "config.json");
    File.Delete(configPath);
    Check(ModEntry.ReadSelectionDialog(), "fresh Workshop subscription must show panel without config");
    Check(!File.Exists(configPath), "reading subscriber defaults must not generate config");
    File.WriteAllText(configPath, "{\"ProbeValue\":73,\"SelectionDialog\":false}");
    Check(!ModEntry.ReadSelectionDialog(), "explicit installed panel preference must be retained");
    File.WriteAllText(configPath, "{\"ProbeValue\":73,\"SelectionDialog\":true}");
    var brokenNeow = new MegaCrit.Sts2.Core.Models.Events.Neow { State = new RunState { Modifiers = new[] { new SelectEncounterSettings() } } };
    Check(brokenNeow.OptionsForTest().Count == 0, "unpatched Neow failure witness no longer reproduces");
    harmony.PatchAll(typeof(ModEntry).Assembly);
    typeof(ModEntry).GetProperty(nameof(ModEntry.IsActive))!.SetValue(null, true);
    var boss1 = ModelDb.Register(new EncounterModel { Id = new("ENCOUNTER", "boss1") });
    var boss2 = ModelDb.Register(new EncounterModel { Id = new("ENCOUNTER", "boss2") });
    var ancient1 = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "ancient1") });
    var ancient2 = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "ancient2") });
    var shared = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "shared") });
    var acts = Enumerable.Range(0, 3).Select(i => ModelDb.Register(new ActModel { Id = new("ACT", "act" + i), Bosses = [boss1, boss2], Ancients = [ancient1, ancient2] })).ToArray();
    var variantBoss = ModelDb.Register(new EncounterModel { Id = new("ENCOUNTER", "variant-boss") });
    var lockedAncient = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "locked") });
    var variant = ModelDb.Register(new ActModel { Id = new("ACT", "alternate0"), Bosses = [variantBoss], Ancients = [lockedAncient] });
    ModelDb.ActsByIndex = [new ActModel[] { acts[0], variant }, new ActModel[] { acts[1] }, new ActModel[] { acts[2] }];
    ModelDb.AllSharedAncients = [shared];
    var lateBoss = ModelDb.Register(new EncounterModel { Id = new("ENCOUNTER", "late-boss") });
    var lateAncient = ModelDb.Register(new AncientEventModel { Id = new("ANCIENT", "late-ancient") });
    acts[2].Bosses.Add(lateBoss); acts[2].Ancients.Add(lateAncient);
    var globalOptions = EncounterCatalog.BuildOptions();
    Check(globalOptions[0].Bosses.Contains(lateBoss) && globalOptions[0].Ancients.Contains(lateAncient), "late targets missing in Act1");
    Check(!EncounterCatalog.IsNative(0, lateBoss) && !EncounterCatalog.IsNative(0, lateAncient), "late targets not marked cross-Act");
    var player = new Player();
    var catalog = EncounterCatalog.Build(acts, player.UnlockState);
    Check(catalog[0].Ancients.Contains(shared) && catalog[1].Ancients.Contains(shared), "global shared candidates missing");
    Check(!EncounterCatalog.IsNative(0, shared) && EncounterCatalog.IsNative(1, shared), "cross-Act label classification");
    Check(catalog[0].Bosses.Contains(variantBoss) && catalog[0].Ancients.Contains(lockedAncient), "alternate/locked candidates missing");
    var alternateCatalog = EncounterCatalog.Build([variant, acts[1], acts[2]], new());
    Check(catalog[0].Bosses.SequenceEqual(alternateCatalog[0].Bosses) && catalog[0].Ancients.SequenceEqual(alternateCatalog[0].Ancients), "catalog changed with seed variant/unlocks");
    ModelDb.ActsByIndex = [new ActModel[] { variant, acts[0] }, new ActModel[] { acts[1] }, new ActModel[] { acts[2] }];
    Check(catalog[0].Bosses.SequenceEqual(EncounterCatalog.BuildOptions()[0].Bosses), "variant enumeration changed order");
    EncounterSelectionPanel.Install();
    var screen = new MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen();
    Engine.Tree.Root.AddChild(screen);
    var panel = Descendants<PanelContainer>(screen).Single();
    Check(panel.AnchorLeft > 0.5f && panel.AnchorRight > 0.9f, "panel not upper-right");
    var toggle = Descendants<Button>(screen).Single(b => b.Name == "SelectMODToggle");
    var content = Descendants<VBoxContainer>(screen).Single(c => c.Name == "SelectMODSelectionContent");
    Check(!content.Visible && panel.AnchorBottom == panel.AnchorTop && panel.OffsetBottom == 72, "panel not initially collapsed to header height");
    toggle.Press();
    Check(content.Visible && panel.AnchorBottom > 0.8f && panel.OffsetBottom == 0, "panel did not expand");
    EncounterSelectionPanel.Attach(screen);
    Check(Descendants<PanelContainer>(screen).Count() == 1, "panel attached twice");
    var options = Descendants<OptionButton>(screen).ToArray();
    Check(options.Length == 6 && options.All(o => o.Items[0] == "ランダム"), "per-Act controls/random default missing");
    // Real user-selection signals, no confirm dialog. Capture the stable IDs immediately.
    options[0].UserSelect(Array.IndexOf(catalog[0].Bosses, variantBoss) + 1);
    options[1].UserSelect(Array.IndexOf(catalog[0].Ancients, lockedAncient) + 1);
    Check(EncounterDraft.Get(0) == (EncounterCatalog.Key(variantBoss), EncounterCatalog.Key(lockedAncient)), "selection signal lost selected IDs");
    var otherScreen = new MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen();
    Engine.Tree.Root.AddChild(otherScreen);
    Check(Descendants<OptionButton>(otherScreen).First().Selected == options[0].Selected, "new screen lost next-run choice");
    var otherOptions = Descendants<OptionButton>(otherScreen).ToArray();
    otherOptions[0].UserSelect(0);
    screen.ShowForTest();
    Check(options[0].Selected == 0, "cached screen displayed stale choice");
    options[0].UserSelect(Array.IndexOf(catalog[0].Bosses, variantBoss) + 1);
    var choiceBeforeCollapse = EncounterDraft.Get(0);
    toggle.Press();
    Check(!content.Visible && panel.AnchorBottom == panel.AnchorTop && panel.OffsetBottom == 72, "collapse retained full-height hit area");
    Check(EncounterDraft.Get(0) == choiceBeforeCollapse && toggle.Text.Contains("指定あり"), "collapse lost selection/status");
    toggle.Press();
    Check(content.Visible && options[0].Selected > 0, "reopening lost selection");
    toggle.Press();
    var game = new NGame();
    var state = await game.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false, 10);
    Check(game.Calls == 1, "original start invoked more than once");
    Check(state.IsCustomGame && state.AscensionLevel == 10, "changed run classification/cumulative ascension");
    Check(state.Acts[0].BossEncounterForTest() == variantBoss, "selected alternate boss not applied");
    Check(state.Acts[0].GetRoomsDebug().Ancient == lockedAncient, "selected locked ancient not applied");
    Check(state.Acts[1].GetRoomsDebug().Boss == boss1, "unselected Act changed");
    Check(game.Manager.GameRngDraws == 7, "selection consumed additional game RNG");
    var settings = state.Modifiers.OfType<SelectEncounterSettings>().Single();
    var neow = new MegaCrit.Sts2.Core.Models.Events.Neow { State = state };
    Check(neow.OptionsForTest().SequenceEqual(new[] { "standard-neow-reward" }), "SelectMOD storage suppressed normal Neow rewards");
    Check(state.Modifiers.Single() == settings, "Neow filtering mutated run modifiers");
    var foreignReward = new MegaCrit.Sts2.Core.Models.Events.FixtureRewardModifier();
    var mixedState = new RunState { Modifiers = new List<ModifierModel> { settings, foreignReward } };
    Check(new MegaCrit.Sts2.Core.Models.Events.Neow { State = mixedState }.OptionsForTest().Single() == foreignReward.Title.GetFormattedText(), "foreign modifier reward changed");
    Check(mixedState.Modifiers.Count == 2, "mixed Neow view changed persisted state");
    Check(new MegaCrit.Sts2.Core.Models.Events.Neow().OptionsForTest().Single() == "standard-neow-reward", "vanilla Neow branch changed");
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
    Check(loadManager.GameRngDraws == 0 && loaded.Acts[0].GetRoomsDebug().Boss == variantBoss, "load rerolled or lost selected result");
    var before = RunState.Restores;
    save.Acts[0].SerializableRooms.BossId = boss1.Id;
    Reject(() => RunState.FromSerializable(save), "mismatched saved result accepted");
    Check(RunState.Restores == before, "invalid save reached original restoration");
    save.Acts[0].SerializableRooms.BossId = variantBoss.Id;
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
    // Actually start with Act3 targets in Act1 through the real panel signal path.
    options[0].UserSelect(Array.IndexOf(catalog[0].Bosses, lateBoss) + 1);
    options[1].UserSelect(Array.IndexOf(catalog[0].Ancients, lateAncient) + 1);
    var crossGame = new NGame();
    var crossState = await crossGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false);
    Check(crossState.Acts[0].GetRoomsDebug().Boss == lateBoss && crossState.Acts[0].GetRoomsDebug().Ancient == lateAncient, "cross-Act fixed selection not applied");
    Check(crossGame.Manager.GameRngDraws == 6, "cross-Act selection advanced extra RNG");
    crossGame.Manager.CleanUp();
    // Changing next-run UI state must not alter the active run's frozen values.
    var frozenRules = settings.SelectEncounterRules;
    Descendants<Button>(screen).Single(b => b.Name == "SelectMODRandomReset").Press();
    Check(EncounterDraft.Get(0) == (null, null) && options.All(o => o.Selected == 0), "reset did not update draft and controls");
    Check(settings.SelectEncounterRules == frozenRules, "next-run edit changed saved rules");
    var vanillaGame = new NGame();
    var vanillaState = await vanillaGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false, 10);
    Check(!vanillaState.IsCustomGame && vanillaState.Modifiers.Count == 0 && vanillaState.AscensionLevel == 10, "random start altered standard run");
    vanillaGame.Manager.CleanUp();
    EncounterDraft.Set(0, true, EncounterCatalog.Key(variantBoss));
    var dailyGame = new NGame();
    var daily = await dailyGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false, 0, DateTimeOffset.UtcNow);
    Check(daily.Modifiers.Count == 0 && !daily.IsCustomGame, "daily start captured normal draft");
    dailyGame.Manager.CleanUp();
    var disabledGame = new NGame();
    File.WriteAllText(Path.Combine(dir, "config.json"), "{\"ProbeValue\":73,\"SelectionDialog\":false}");
    var disabled = await disabledGame.StartNewSingleplayerRun(player, true, acts, [], "seed", GameMode.Standard, false);
    Check(!disabled.IsCustomGame && disabled.Modifiers.Count == 0, "disabled panel still applied draft");
    disabledGame.Manager.CleanUp();
    EncounterDraft.Reset();
    Reject(() => ProbeRules.ValidateSelection([new SelectAscensionSettings()], true, GameMode.Standard, 1), "retired ascension accepted");
    var retired = new SerializableRun { IsCustomGame = true, Modifiers = [new() { Id = ModelDb.GetId<SelectAscensionSettings>(), Value = new SelectAscensionSettings { SelectAscensionSchema = 1, SelectAscensionMask = 10 } }] };
    Reject(() => RunState.FromSerializable(retired), "retired save silently converted");
    Check(Harmony.GetAllPatchedMethods().All(m => m.DeclaringType != typeof(MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager)), "ascension hook still active");
    Check(new MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager().HasLevel(MegaCrit.Sts2.Core.Entities.Ascension.AscensionLevel.TightBelt), "standard ascension changed");
    Check(MegaCrit.Sts2.Core.Odds.CardRarityOdds.regularCommonOdds == 0.6f, "A7 cache altered");
    var host = new MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby { Ready = true };
    host.NetService.Type = MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host;
    host.BeginRunForAllPlayers("seed", []);
    Check(host.Starts == 0 && !host.Ready, "uninitialized multiplayer start not blocked");
    checks += await MultiplayerTests.Run();
    var log = File.ReadAllText(Path.Combine(dir, "selectmod.log"));
    Check(log.Contains("START_COMPLETED") && log.Contains("ENCOUNTER_RUN_LOADED"), "progress diagnostics missing");
    Console.WriteLine($"PASS: {checks} independent selector/panel-signal/Harmony checks. Actual game build, UI and gameplay acceptance pending.");
}
finally
{
    EncounterSelectionPanel.Uninstall();
    foreach (var method in Harmony.GetAllPatchedMethods().ToArray()) harmony.Unpatch(method, HarmonyPatchType.All, ModEntry.Owner);
    Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", null); Directory.Delete(dir, true);
}
static class TestAccess { public static EncounterModel BossEncounterForTest(this ActModel act) => act.GetRoomsDebug().Boss; }
