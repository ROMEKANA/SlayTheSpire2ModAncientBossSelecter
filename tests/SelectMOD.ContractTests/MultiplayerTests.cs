using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using SelectMOD;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

internal static class MultiplayerTests
{
    public sealed record FixtureAct(ModelKey Id, ModelKey[] Bosses, ModelKey[] Ancients);
    public sealed record Manifest(FixtureAct[][] Groups, ModelKey[] Shared);
    public sealed record ProcessRequest(Manifest Catalog, string Payload, ModelKey[] Acts, int[] Players, int Ascension, string Seed);
    public sealed record ProcessResult(string Plan, string Context, int Rng, int Players);
    private static List<SerializedModifier> Wire(IEnumerable<ModifierModel> models) => models.Select(m => new SerializedModifier { Id = m.Id, Value = Clone(m) }).ToList();
    private static ModifierModel Clone(ModifierModel model)
    {
        var copy = (ModifierModel)Activator.CreateInstance(model.GetType())!; copy.Id = model.Id;
        foreach (var property in model.GetType().GetProperties().Where(p => p.IsDefined(typeof(SavedPropertyAttribute))))
        {
            var json = JsonSerializer.Serialize(property.GetValue(model), property.PropertyType);
            property.SetValue(copy, JsonSerializer.Deserialize(json, property.PropertyType));
        }
        return copy;
    }
    private static StartRunLobby Lobby(NetGameType role, int players = 2)
    {
        var lobby = new StartRunLobby { IsCustom = true, GameMode = GameMode.Standard, Players = Enumerable.Range(1, players).ToList() };
        lobby.NetService.Type = role; lobby.NetService.NetId = role == NetGameType.Host ? 1u : 2u;
        return lobby;
    }
    private static SerializableRun Save(RunState state) => new() {
        IsCustomGame = state.IsCustomGame, GameMode = state.GameMode, Ascension = state.AscensionLevel,
        Players = state.Players.ToList(), Modifiers = Wire(state.Modifiers),
        Acts = state.Acts.Select(a => new SerializableActModel { Id = a.Id, SerializableRooms = new() {
            BossId = a.GetRoomsDebug().Boss.Id, SecondBossId = a.GetRoomsDebug().SecondBoss?.Id,
            AncientId = a.GetRoomsDebug().HasAncient ? a.GetRoomsDebug().Ancient.Id : null
        } }).ToList()
    };
    public static async Task<int> Run()
    {
        var checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("MP: " + name); checks++; }
        void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception("MP accepted: " + name); }
        var options = EncounterCatalog.BuildOptions(); var acts = options.Select(o => o.Act).ToArray();
        var targetBoss = options[0].Bosses.First(b => !options[0].Act.AllBossEncounters.Any(original => original.Id == b.Id)); var targetAncient = options[0].Ancients.Last();
        var originalSolo = EncounterDraft.Get(0);
        EncounterDraft.Set(0, true, EncounterCatalog.Key(targetBoss));
        for (var count = 2; count <= 4; count++)
        {
            var host = Lobby(NetGameType.Host, count); var clients = Enumerable.Range(1, count - 1).Select(_ => Lobby(NetGameType.Client, count)).ToArray();
            var deliver = true;
            host.OnPreview = models => { if (deliver) foreach (var client in clients) client.HandleModifiersChangedMessage(new LobbyModifiersChangedMessage { modifiers = Wire(models) }, 1); };
            host.Ready = true; host.AddLocalHostPlayer();
            Check(!host.Ready && host.Modifiers.OfType<SelectLobbySettings>().Count() == 1, "initial host marker/unready");
            Check(clients.All(c => MultiplayerSelection.Choice(c, 0).Boss == EncounterCatalog.Key(targetBoss)), "initial preview not delivered");
            Reject(() => MultiplayerSelection.Edit(clients[0], 0, true, null), "guest edit");
            Reject(() => clients[0].SetModifiers([]), "guest native SetModifiers");
            var clientScreen = new MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen { Lobby = clients[0] };
            var left = new Control { Name = "LeftContainer" };
            var group = new Control { Name = "CharSelectButtons" };
            var icons = new Control { Name = "ButtonContainer", GlobalPosition = new(110, 200), Size = new(420, 80) };
            clientScreen.AddChild(left); left.AddChild(group); group.AddChild(icons);
            Engine.Tree.Root.AddChild(clientScreen); clientScreen.AfterInitialized();
            Check(Walk<HBoxContainer>(clientScreen).Single(b => b.Name == "SelectMODAscensionControls").GlobalPosition == new Vector2(110, 284),
                "custom flames not below character icons");
            var controls = Walk<OptionButton>(clientScreen).ToArray();
            Check(controls.Length == options.Length * 2 + 1 && controls.All(c => c.Disabled), "guest UI not read-only");
            Check(Walk<Button>(clientScreen).Single(b => b.Name == "SelectMODRandomReset").Disabled, "guest reset enabled");
            var hostScreen = new MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen { Lobby = host };
            Engine.Tree.Root.AddChild(hostScreen); hostScreen.AfterInitialized();
            var hostFlame = Walk<Button>(hostScreen).Single(b => b.Name == "SelectMODFlame4");
            var guestFlame = Walk<Button>(clientScreen).Single(b => b.Name == "SelectMODFlame4");
            hostFlame.Press();
            Check(guestFlame.Visible && guestFlame.Disabled && guestFlame.Text == "" && host.Ascension == 10, "guest missing host's lower-level toggle");
            guestFlame.Press();
            Check((AscensionPreference.Read() & 8) == 0, "guest changed preference through read-only flame");
            hostFlame.Press();
            Check(guestFlame.Text == "🔥", "guest flame did not relight");
            host.Ready = true; host.SyncAscensionChange(10);
            Check(!host.Ready && ((SelectLobbySettings)host.Modifiers.Single()).Read().Draft.Ascension == 10, "ascension change not captured/unready");
            var old = Wire(host.Modifiers);
            host.Ready = true; MultiplayerSelection.Edit(host, 0, false, EncounterCatalog.Key(targetAncient));
            Check(!host.Ready && MultiplayerSelection.Choice(clients[0], 0).Ancient == EncounterCatalog.Key(targetAncient), "edit delivery/readiness");
            var revision = ((SelectLobbySettings)host.Modifiers.Single()).Read().Draft.Revision;
            host.SetModifiers(host.Modifiers.ToArray());
            Check(((SelectLobbySettings)host.Modifiers.Single()).Read().Draft.Revision > revision, "ordinary modifier sync lost/reused snapshot");
            Reject(() => ((SelectLobbySettings)host.Modifiers.Single()).SelectLobbyPayload = "changed", "published host marker changed");
            var currentPayload = ((SelectLobbySettings)clients[0].Modifiers.Single()).SelectLobbyPayload;
            clients[0].HandleModifiersChangedMessage(new LobbyModifiersChangedMessage { modifiers = old }, 1);
            Check(((SelectLobbySettings)clients[0].Modifiers.Single()).SelectLobbyPayload == currentPayload, "stale preview overwrote new one");
            clients[0].HandleModifiersChangedMessage(new LobbyModifiersChangedMessage { modifiers = old }, 99);
            Check(((SelectLobbySettings)clients[0].Modifiers.Single()).SelectLobbyPayload == currentPayload, "non-host preview changed state");
            clients[0].HandleLobbyBeginRunMessage(new LobbyBeginRunMessage { playersInLobby = host.Players, modifiers = Wire(host.Modifiers), seed = "seed" }, 99);
            Check(clients[0].Starts == 0, "non-host start accepted");
            // Guest's local preferences change, and the last host preview is lost.
            // The authoritative final packet must still bind the latest values.
            EncounterDraft.Set(0, true, null); deliver = false;
            MultiplayerSelection.Edit(host, 1, true, EncounterCatalog.Key(options[1].Bosses.First()));
            MultiplayerSelection.Edit(host, 2, true, EncounterCatalog.Key(targetBoss));
            MultiplayerSelection.Edit(host, 2, true, EncounterCatalog.Key(targetBoss), second: true);
            var final = Wire(host.Modifiers);
            var states = new List<RunState>(); var games = new List<NGame>();
            var players = host.Players.Select(id => new Player { NetId = (ulong)id }).ToList();
            foreach (var client in clients) client.OnStart = models => {
                var game = new NGame(); games.Add(game);
                states.Add(game.StartNewMultiplayerRun(players, client, true, acts, models, "seed", 0).GetAwaiter().GetResult());
            };
            host.OnStart = models => {
                foreach (var client in clients) client.HandleLobbyBeginRunMessage(new LobbyBeginRunMessage { playersInLobby = host.Players.ToList(), modifiers = Wire(models), seed = "seed" }, 1);
                var game = new NGame(); games.Add(game);
                states.Add(game.StartNewMultiplayerRun(players, host, true, acts, models, "seed", 10).GetAwaiter().GetResult());
            };
            host.BeginRunForAllPlayers("seed", host.Modifiers.ToList());
            Check(states.Count == count, "not every peer started");
            Check(states.All(s => s.Acts[0].GetRoomsDebug().Boss.Id == targetBoss.Id && s.Acts[0].GetRoomsDebug().Ancient.Id == targetAncient.Id), "peers did not use host choices");
            Check(states.Select(s => s.Modifiers.OfType<SelectEncounterSettings>().Single().SelectEncounterResults).Distinct().Count() == 1, "peer result plans differ");
            Check(states.All(s => s.Acts[2].GetRoomsDebug().Boss.Id == targetBoss.Id && s.Acts[2].GetRoomsDebug().SecondBoss?.Id == targetBoss.Id), "guest lost same-boss A10 selection");
            Check(states.All(s => s.AscensionLevel == 10), "client used stale local ascension");
            Check(games.All(g => g.Manager.GameRngDraws == 7), "peer RNG advancement differs");
            Check(states.All(s => s.Players.Count == count && s.Modifiers.All(m => m is not SelectLobbySettings)), "unbound marker/player count in run");
            Check(EncounterDraft.Get(0).Boss == null, "host/guest traffic overwrote local defaults");
            Reject(() => MultiplayerSelection.Edit(host, 0, true, null), "edit after freeze");
            host.BeginRunForAllPlayers("seed", host.Modifiers.ToList());
            clients[0].HandleLobbyBeginRunMessage(new LobbyBeginRunMessage { playersInLobby = host.Players, modifiers = final, seed = "seed" }, 1);
            Check(states.Count == count, "duplicate start ran twice");
            Reject(() => games[0].Manager.SetUpNewMultiplayer(states[0], host), "second setup while active");
            Check(MultiplayerSelection.HasSelectedRun(games[0].Manager), "rejected second setup erased active tracking");
            Reject(() => games[0].Manager.GetRejoinMessage(), "unsupported live rejoin");
            var save = Save(states[0]);
            var inconsistent = Save(states[0]);
            var unexpected = new SelectAscensionSettings { SelectAscensionSchema = AscensionSettings.CurrentSchema, SelectAscensionMask = 1 };
            inconsistent.Modifiers.Add(new() { Id = ModelDb.GetId<SelectAscensionSettings>(), Value = unexpected });
            Reject(() => RunState.FromSerializable(inconsistent), "saved mask contradicts frozen host settings");
            var incompleteLobby = new LoadRunLobby { Run = save, Players = new List<int> { 1 }, Ready = true };
            incompleteLobby.NetService.Type = NetGameType.Host;
            incompleteLobby.BeginRunForAllPlayersIfAllReady();
            Check(incompleteLobby.Starts == 0 && !incompleteLobby.Ready, "resume began without the whole party");
            var restored = RunState.FromSerializable(save);
            var load = new RunManager(); var loadLobby = new LoadRunLobby { Run = save, Players = save.Players.ToList() };
            loadLobby.NetService.Type = NetGameType.Client;
            await load.SetUpSavedMultiplayer(restored, loadLobby);
            Check(load.GameRngDraws == 0 && restored.Modifiers.OfType<SelectEncounterSettings>().Single().SelectEncounterResults == states[0].Modifiers.OfType<SelectEncounterSettings>().Single().SelectEncounterResults, "save/load rerolled or lost results");
            var restoredModel = restored.Modifiers.OfType<SelectEncounterSettings>().Single();
            Reject(() => restoredModel.SelectMultiplayerOrigin = "changed", "loaded origin changed");
            Reject(() => new RunManager().SetUpSavedSingleplayer(restored, save), "multiplayer save in solo");
            var correctAscension = save.Ascension; save.Ascension = 0;
            Reject(() => RunState.FromSerializable(save), "saved ascension changed from host value");
            save.Ascension = correctAscension;
            var priorRestores = RunState.Restores; var boss = save.Acts[0].SerializableRooms.BossId;
            save.Acts[0].SerializableRooms.BossId = options[0].Bosses.First().Id;
            if (save.Acts[0].SerializableRooms.BossId == boss) save.Acts[0].SerializableRooms.BossId = new("ENCOUNTER", "missing");
            Reject(() => RunState.FromSerializable(save), "tampered saved encounter");
            Check(RunState.Restores == priorRestores, "bad multiplayer save reached restore");
            save.Acts[0].SerializableRooms.BossId = boss;
            if (count == 2)
            {
                var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var failedManager = new RunManager { SavedMultiplayerResult = delayed.Task };
                var pending = failedManager.SetUpSavedMultiplayer(RunState.FromSerializable(save), loadLobby);
                failedManager.CleanUp(); MultiplayerSelection.Track(failedManager, restored);
                delayed.SetException(new InvalidOperationException("injected late load failure"));
                try { await pending; throw new Exception("async failure was swallowed"); } catch (InvalidOperationException) { checks++; }
                Check(MultiplayerSelection.HasSelectedRun(failedManager), "old async failure cleared newer tracking");
                MultiplayerSelection.Clear(failedManager);
            }
            foreach (var game in games) game.Manager.CleanUp(); load.CleanUp();
            foreach (var client in clients) client.CleanUp(false); host.CleanUp(false); clientScreen.QueueFree();
            Reject(() => MultiplayerSelection.Edit(host, 0, true, null), "closed lobby edit");
            Check(!MultiplayerSelection.HasSelectedRun(load), "cleanup left selected-run tracking");
            if (count == 2) checks += await ProcessPeers(final.OfType<SerializedModifier>().Single().Value as SelectLobbySettings ?? throw new Exception(), acts, players, states[0]);
            EncounterDraft.Set(0, true, EncounterCatalog.Key(targetBoss));
        }
        var normalHost = Lobby(NetGameType.Host);
        var normalClient = Lobby(NetGameType.Client);
        normalHost.IsCustom = normalClient.IsCustom = false;
        AscensionPreference.Write(3);
        normalHost.AddLocalHostPlayer();
        MultiplayerSelection.Edit(normalHost, 2, true, EncounterCatalog.Key(targetBoss), second: true);
        var hostPacket = Wire(normalHost.Modifiers);
        AscensionPreference.Write(AscensionSettings.All);
        normalClient.HandleLobbyBeginRunMessage(new() { modifiers = hostPacket, playersInLobby = [1, 2], seed = "mask" }, 1);
        var peerGame = new NGame();
        var peer = await peerGame.StartNewMultiplayerRun([new Player(), new Player()], normalClient, true, acts,
            [], "mask", 0);
        Check(!peer.IsCustomGame && peer.AscensionLevel == 10 && peer.Modifiers.OfType<SelectAscensionSettings>().Single().Snapshot.Mask == 3,
            "normal guest used local preferences instead of final host mask");
        Check(peer.Modifiers.OfType<SelectEncounterSettings>().Single().ReadRules()[2].SecondBoss == null, "disabled A10 leaked a second-boss rule");
        Check(!peer.Acts.Last().GetRoomsDebug().HasSecondBoss, "guest A10 was not suppressed");
        peerGame.Manager.CleanUp();
        var daily = await peerGame.StartNewMultiplayerRun([new Player(), new Player()], normalClient, true, acts,
            hostPacket.Select(m => m.Value).ToArray(), "daily", 10, DateTimeOffset.UtcNow);
        Check(daily.Modifiers.Count == 0 && daily.Acts.Last().GetRoomsDebug().HasSecondBoss, "network daily did not retain full stack");
        peerGame.Manager.CleanUp();
        normalHost.CleanUp(false);
        normalClient.CleanUp(false);
        var invalidHost = Lobby(NetGameType.Host); invalidHost.AddLocalHostPlayer();
        var envelope = ((SelectLobbySettings)invalidHost.Modifiers.Single()).Read();
        SelectLobbySettings Marker(LobbyEnvelope value) => new() { SelectLobbyPayload = EncounterRules.Encode(value) };
        Reject(() => MultiplayerContract.Read(""), "missing snapshot");
        Reject(() => MultiplayerContract.Read(new string('x', EncounterRules.MaxJsonLength + 1)), "oversized snapshot");
        Reject(() => MultiplayerContract.Read(EncounterRules.Encode(envelope with { Digest = "bad" })), "changed digest");
        var wrongCatalog = envelope.Draft with { Catalog = new string('A', 64) };
        Reject(() => MultiplayerContract.Read(EncounterRules.Encode(new LobbyEnvelope(wrongCatalog, MultiplayerContract.Hash(EncounterRules.Encode(wrongCatalog))))), "catalog mismatch");
        var wrongProtocol = envelope.Draft with { Protocol = 999 };
        Reject(() => MultiplayerContract.Read(EncounterRules.Encode(new LobbyEnvelope(wrongProtocol, MultiplayerContract.Hash(EncounterRules.Encode(wrongProtocol))))), "protocol mismatch");
        Reject(() => MultiplayerSelection.ReadSnapshot([]), "missing marker");
        Reject(() => MultiplayerSelection.ReadSnapshot([Marker(envelope), Marker(envelope)]), "duplicate marker");
        var receiver = Lobby(NetGameType.Client); receiver.HandleModifiersChangedMessage(new() { modifiers = Wire(invalidHost.Modifiers) }, 1);
        var conflict = MultiplayerContract.Create(envelope.Draft.Session, envelope.Draft.Revision, envelope.Draft.Choices.Select(c => c with { Boss = null }).ToArray());
        Reject(() => MultiplayerSelection.Accept(receiver, [Marker(conflict)], false), "same-revision conflict");
        receiver.Ready = true; receiver.HandleModifiersChangedMessage(new() { modifiers = [] }, 1);
        Check(!receiver.Ready, "invalid incoming message didn't unready guest");
        receiver.HandleLobbyBeginRunMessage(new() { modifiers = [], playersInLobby = [1, 2], seed = "seed" }, 1);
        Check(receiver.Starts == 0, "failed validation still started");
        var tooMany = Lobby(NetGameType.Host, 5); tooMany.AddLocalHostPlayer(); tooMany.Ready = true;
        tooMany.BeginRunForAllPlayers("seed", tooMany.Modifiers.ToList());
        Check(tooMany.Starts == 0 && !tooMany.Ready, "unsupported player count started");
        invalidHost.CleanUp(false); receiver.CleanUp(false); tooMany.CleanUp(false);
        var clampedHost = Lobby(NetGameType.Host); clampedHost.AddLocalHostPlayer();
        clampedHost.Ascension = 2; clampedHost.Ready = true;
        clampedHost.BeginRunForAllPlayers("seed", clampedHost.Modifiers.ToList());
        Check(clampedHost.Starts == 0 && !clampedHost.Ready && ((SelectLobbySettings)clampedHost.Modifiers.Single()).Read().Draft.Ascension == 2, "native ascension clamp didn't require reconfirmation");
        clampedHost.Ready = true; clampedHost.BeginRunForAllPlayers("seed", clampedHost.Modifiers.ToList());
        Check(clampedHost.Starts == 1, "reconfirmed clamped ascension could not start");
        clampedHost.CleanUp(false);
        var modeHost = Lobby(NetGameType.Host); modeHost.GameMode = GameMode.Blitz; modeHost.AddLocalHostPlayer();
        Check(modeHost.Modifiers.Count == 0, "unsupported mode got network rules");
        modeHost.SetGameMode(GameMode.Standard);
        Check(modeHost.Modifiers.OfType<SelectLobbySettings>().Count() == 1 && !modeHost.Ready, "switch to Standard didn't initialize protocol");
        modeHost.CleanUp(false);
        EncounterDraft.Reset();
        var randomHost = Lobby(NetGameType.Host); randomHost.AddLocalHostPlayer();
        RunState? randomState = null; var randomGame = new NGame();
        randomHost.OnStart = mods => randomState = randomGame.StartNewMultiplayerRun(new() { new Player { NetId = 1 }, new Player { NetId = 2 } }, randomHost, true, acts, mods, "seed", 0).GetAwaiter().GetResult();
        randomHost.BeginRunForAllPlayers("seed", randomHost.Modifiers.ToList());
        Check(randomState != null && randomState.Modifiers.Count == 0, "all-random transport marker leaked into gameplay");
        Check(randomGame.Manager.GameRngDraws == 7 && randomState!.AscensionLevel == 10, "all-random native RNG/host ascension changed");
        randomGame.Manager.CleanUp(); randomHost.CleanUp(false);
        EncounterDraft.Set(0, true, originalSolo.Boss); EncounterDraft.Set(0, false, originalSolo.Ancient);
        return checks;
    }
    private static IEnumerable<T> Walk<T>(Node root) where T : Node => root.GetChildren().OfType<T>().Concat(root.GetChildren().SelectMany(Walk<T>));
    private static Manifest Capture() => new(ModelDb.ActsByIndex.Select(g => g.Select(a => new FixtureAct(EncounterCatalog.Key(a), a.AllBossEncounters.Select(EncounterCatalog.Key).ToArray(), a.AllAncients.Select(EncounterCatalog.Key).ToArray())).ToArray()).ToArray(), ModelDb.AllSharedAncients.Select(EncounterCatalog.Key).ToArray());
    private static void Install(Manifest manifest)
    {
        var bosses = new Dictionary<ModelKey, EncounterModel>(); var ancients = new Dictionary<ModelKey, AncientEventModel>();
        EncounterModel Boss(ModelKey key) { if (!bosses.ContainsKey(key)) bosses[key] = ModelDb.Register(new EncounterModel { Id = EncounterCatalog.Id(key) }); return bosses[key]; }
        AncientEventModel Ancient(ModelKey key) { if (!ancients.ContainsKey(key)) ancients[key] = ModelDb.Register(new AncientEventModel { Id = EncounterCatalog.Id(key) }); return ancients[key]; }
        ModelDb.ActsByIndex = manifest.Groups.Select(g => (IReadOnlyList<ActModel>)g.Select(a => ModelDb.Register(new ActModel { Id = EncounterCatalog.Id(a.Id), Bosses = a.Bosses.Select(Boss).ToList(), Ancients = a.Ancients.Select(Ancient).ToList() })).ToArray()).ToArray();
        ModelDb.AllSharedAncients = manifest.Shared.Select(Ancient).ToArray();
    }
    public static async Task RunProcessPeer(string input, string output)
    {
        var request = JsonSerializer.Deserialize<ProcessRequest>(File.ReadAllText(input))!;
        var config = output + ".config"; Directory.CreateDirectory(config);
        Environment.SetEnvironmentVariable("SELECTMOD_CONFIG_DIR", config);
        File.WriteAllText(Path.Combine(config, "config.json"), "{\"SelectionDialog\":false}");
        Install(request.Catalog);
        new Harmony(ModEntry.Owner).PatchAll(typeof(ModEntry).Assembly);
        typeof(ModEntry).GetProperty(nameof(ModEntry.IsActive))!.SetValue(null, true);
        // Deliberately different local defaults, with the local UI disabled.
        EncounterDraft.Set(0, true, null);
        var lobby = Lobby(NetGameType.Client, request.Players.Length); var game = new NGame();
        Task<RunState>? result = null;
        lobby.OnStart = models => result = game.StartNewMultiplayerRun(request.Players.Select(id => new Player { NetId = (ulong)id }).ToList(), lobby, true,
            request.Acts.Select(k => ModelDb.GetById<ActModel>(EncounterCatalog.Id(k))).ToArray(), models, request.Seed, request.Ascension);
        // Start with no prior preview: final packet is sufficient.
        lobby.HandleLobbyBeginRunMessage(new() { playersInLobby = request.Players.ToList(), seed = request.Seed, modifiers = Wire([new SelectLobbySettings { Id = ModelDb.GetId<SelectLobbySettings>(), SelectLobbyPayload = request.Payload }]) }, 1);
        var state = await (result ?? throw new Exception("Peer didn't start"));
        var log = File.ReadAllLines(Path.Combine(config, "selectmod.log"));
        var context = log.Last(l => l.Contains("MP_START_CONTEXT digest=")).Split("digest=")[1];
        File.WriteAllText(output, JsonSerializer.Serialize(new ProcessResult(state.Modifiers.OfType<SelectEncounterSettings>().Single().SelectEncounterResults, context, game.Manager.GameRngDraws, state.Players.Count)));
    }
    private static async Task<int> ProcessPeers(SelectLobbySettings marker, ActModel[] acts, List<Player> players, RunState expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "selectmod-multipeer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var request = new ProcessRequest(Capture(), marker.SelectLobbyPayload, acts.Select(EncounterCatalog.Key).ToArray(), players.Select(p => (int)p.NetId).ToArray(), 10, "seed");
            var input = Path.Combine(directory, "request.json"); File.WriteAllText(input, JsonSerializer.Serialize(request));
            async Task<ProcessResult> Child(int index)
            {
                var output = Path.Combine(directory, $"peer{index}.json");
                var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardError = true, RedirectStandardOutput = true };
                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(MultiplayerTests).Assembly.Location);
                foreach (var arg in new[] { "--multiplayer-peer", input, output }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!;
                var errors = process.StandardError.ReadToEndAsync(); var text = process.StandardOutput.ReadToEndAsync();
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { if (!process.HasExited) process.Kill(true); throw; }
                if (process.ExitCode != 0) throw new Exception($"Independent peer failed: {await errors} {await text}");
                return JsonSerializer.Deserialize<ProcessResult>(File.ReadAllText(output))!;
            }
            var peers = await Task.WhenAll(Child(1), Child(2));
            var expectedContext = MultiplayerContract.Hash(EncounterRules.Encode(new { Seed = "seed", Ascension = 10, Players = players.Select(p => p.NetId).ToArray(), Acts = acts.Select(EncounterCatalog.Key).ToArray() }));
            if (peers.Any(p => p.Context != expectedContext || p.Plan != expected.Modifiers.OfType<SelectEncounterSettings>().Single().SelectEncounterResults || p.Rng != 7 || p.Players != players.Count) || peers[0].Context != peers[1].Context)
                throw new Exception("Separate-process peers disagree with host or each other");
            Console.WriteLine("PASS: two separate .NET peer processes agree on host settings, resolved plan, RNG count and start context (independent JSON fixtures, not real game packets).");
            return 3;
        }
        finally { Directory.Delete(directory, true); }
    }
}
