// Independently written minimal contracts; NOT STS2 implementations or production references.
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
namespace MegaCrit.Sts2.Core.Modding { public class ModInitializerAttribute(string name) : Attribute { public string Method = name; } }
namespace MegaCrit.Sts2.Core.Saves.Runs
{
    public class SavedPropertyAttribute : Attribute { }
    public class SerializableActModel { public ModelId? Id { get; set; } public SerializableRoomSet SerializableRooms { get; set; } = new(); }
    public class SerializableRoomSet { public ModelId? BossId { get; set; } public ModelId? SecondBossId { get; set; } public ModelId? AncientId { get; set; } }
}
namespace MegaCrit.Sts2.Core.Localization
{
    public class LocString(string table, string key) { [MethodImpl(MethodImplOptions.NoInlining)] public string GetFormattedText() => table + ":" + key; }
}
namespace MegaCrit.Sts2.Core.Models
{
    public sealed record ModelId(string Category, string Entry);
    public abstract class AbstractModel
    {
        public ModelId Id { get; set; } = new("fixture", "unset");
    }
    public abstract class ModifierModel : AbstractModel
    {
        public virtual MegaCrit.Sts2.Core.Localization.LocString Title => new("fixture", Id.Entry);

        public virtual MegaCrit.Sts2.Core.Localization.LocString Description => new("fixture", "description");
        public virtual Func<Task>? GenerateNeowOption(EventModel model) => null;
        public ModifierModel ToMutable() => (ModifierModel)MemberwiseClone();
        protected virtual void AfterRunCreated(RunState state) { }
        protected virtual void AfterRunLoaded(RunState state) { }
        public void OnRunCreated(RunState state) => AfterRunCreated(state);
        public void OnRunLoaded(RunState state) => AfterRunLoaded(state);
        public static ModifierModel FromSerializable(SerializedModifier saved) => saved.Value;
    }
    public class EncounterModel : AbstractModel { public MegaCrit.Sts2.Core.Localization.LocString Title => new("fixture", Id.Entry); }
    public class EventModel : AbstractModel { public MegaCrit.Sts2.Core.Localization.LocString Title => new("fixture", Id.Entry); }
    public class AncientEventModel : EventModel { }
    public class ActModel : AbstractModel
    {
        public virtual MegaCrit.Sts2.Core.Localization.LocString Title => new("fixture", Id.Entry);

        public List<EncounterModel> Bosses { get; set; } = [];
        public List<AncientEventModel> Ancients { get; set; } = [];
        public IEnumerable<EncounterModel> AllBossEncounters => Bosses;
        public IEnumerable<AncientEventModel> AllAncients => Ancients;
        public IEnumerable<AncientEventModel> GetUnlockedAncients(MegaCrit.Sts2.Core.Unlocks.UnlockState state) => Ancients;
        private MegaCrit.Sts2.Core.Rooms.RoomSet _rooms = new();
        public MegaCrit.Sts2.Core.Rooms.RoomSet GetRoomsDebug() => _rooms;
        public void SetBossEncounter(EncounterModel model) => _rooms.Boss = model;
        public void SetSecondBossEncounter(EncounterModel? model) => _rooms.SecondBoss = model;
        public ActModel ToMutable() { var clone = (ActModel)MemberwiseClone(); clone._rooms = new(); return clone; }
    }
    public static class ModelDb
    {
        public static IReadOnlyList<IReadOnlyList<ActModel>> ActsByIndex { get; set; } = [];
        public static IEnumerable<AncientEventModel> AllSharedAncients { get; set; } = [];

        private static readonly Dictionary<ModelId, AbstractModel> Models = new();
        public static ModelId GetId<T>() => new("MODIFIER", typeof(T).Name);
        public static T Modifier<T>() where T : ModifierModel, new()
        {
            var id = GetId<T>();
            if (!Models.ContainsKey(id)) Models[id] = new T { Id = id };
            return (T)Models[id];
        }
        public static T GetById<T>(ModelId id) where T : AbstractModel => Models.TryGetValue(id, out var value) ? (T)value : throw new InvalidOperationException("Missing model");
        public static T Register<T>(T model) where T : AbstractModel { Models[model.Id] = model; return model; }
    }
}
namespace MegaCrit.Sts2.Core.Unlocks { public class UnlockState { public List<AncientEventModel> SharedAncients { get; set; } = []; } }
namespace MegaCrit.Sts2.Core.Entities.Players { public class Player { public ulong NetId { get; set; } = 1; public MegaCrit.Sts2.Core.Unlocks.UnlockState UnlockState { get; set; } = new(); } }
namespace MegaCrit.Sts2.Core.Rooms
{
    public class RoomSet
    {
        public EncounterModel Boss { get; set; } = null!;
        public EncounterModel? SecondBoss { get; set; }
        public AncientEventModel Ancient { get; set; } = null!;
        public bool HasAncient => Ancient != null;
        public bool HasSecondBoss => SecondBoss != null;
    }
}
namespace MegaCrit.Sts2.Core.Runs
{
    public enum GameMode { Standard, Blitz }
    public interface IRunState { IReadOnlyList<ModifierModel> Modifiers { get; } }
    public class RunState : IRunState
    {
        public IReadOnlyList<ModifierModel> Modifiers { get; set; } = [];
        public bool IsCustomGame { get; set; }
        public GameMode GameMode { get; set; }
        public List<int> Players { get; set; } = [1];
        public int AscensionLevel { get; set; }
        public List<ActModel> Acts { get; set; } = [];
        public MegaCrit.Sts2.Core.Unlocks.UnlockState UnlockState { get; set; } = new();
        public static int Restores;
        [MethodImpl(MethodImplOptions.NoInlining)] public static RunState FromSerializable(SerializableRun save)
        {
            Restores++;
            var state = new RunState { Players = save.Players.ToList(), IsCustomGame = save.IsCustomGame, GameMode = save.GameMode, AscensionLevel = save.Ascension, Modifiers = save.Modifiers.Select(ModifierModel.FromSerializable).ToList() };
            foreach (var saved in save.Acts)
            {
                var act = ModelDb.GetById<ActModel>(saved.Id!).ToMutable(); var room = saved.SerializableRooms;
                act.SetBossEncounter(ModelDb.GetById<EncounterModel>(room.BossId!));
                act.SetSecondBossEncounter(room.SecondBossId == null ? null : ModelDb.GetById<EncounterModel>(room.SecondBossId));
                if (room.AncientId != null) act.GetRoomsDebug().Ancient = ModelDb.GetById<AncientEventModel>(room.AncientId);
                state.Acts.Add(act);
            }
            return state;
        }
    }
    public class RunManager
    {
        private RunState? State;
        public MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager AscensionManager { get; private set; } = new();
        [MethodImpl(MethodImplOptions.NoInlining)] private void InitializeShared() => AscensionManager = new() { Level = State!.AscensionLevel };
        [MethodImpl(MethodImplOptions.NoInlining)] public bool HasAscension(MegaCrit.Sts2.Core.Entities.Ascension.AscensionLevel level) => IsInProgress && AscensionManager.HasLevel(level);
        public int Starts; public int GameRngDraws;
        public Task? SavedMultiplayerResult { get; set; }
        public bool IsInProgress => State != null;
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetUpNewSingleplayer(RunState state, bool save = true, DateTimeOffset? dailyTime = null)
        {
            if (State != null) throw new InvalidOperationException("Already active");
            SetUpNewSingleplayerCore(state);
        }
        private void SetUpNewSingleplayerCore(RunState state)
        {
            State = state; InitializeShared(); Starts++;
            foreach (var model in state.Modifiers) model.OnRunCreated(state);
            GenerateRooms();
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public Task SetUpSavedSingleplayer(RunState state, SerializableRun save)
        { State = state; InitializeShared(); Starts++; foreach (var model in state.Modifiers) model.OnRunLoaded(state); return Task.CompletedTask; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void GenerateRooms()
        {
            foreach (var act in State!.Acts)
            {
                GameRngDraws += 2;
                act.SetBossEncounter(act.Bosses[0]);
                act.GetRoomsDebug().Ancient = act.Ancients.FirstOrDefault()!;
            }
            if (HasAscension(MegaCrit.Sts2.Core.Entities.Ascension.AscensionLevel.DoubleBoss)) { GameRngDraws++; State.Acts.Last().SetSecondBossEncounter(State.Acts.Last().Bosses[1]); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void CleanUp(bool graceful = true) { State = null; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetUpReplay(RunState state) { Starts++; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetUpNewMultiplayer(RunState state, MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby lobby, bool save = true, DateTimeOffset? daily = null) { SetUpNewSingleplayerCore(state); }
        [MethodImpl(MethodImplOptions.NoInlining)] public Task SetUpSavedMultiplayer(RunState state, MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.LoadRunLobby lobby) { State = state; InitializeShared(); Starts++; foreach (var model in state.Modifiers) model.OnRunLoaded(state); return SavedMultiplayerResult ?? Task.CompletedTask; }
        [MethodImpl(MethodImplOptions.NoInlining)] public object GetRejoinMessage() => new object();
    }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Game
{
    public enum NetGameType { None, Singleplayer, Host, Client, Replay }
    public class NetService { public NetGameType Type { get; set; } = NetGameType.Singleplayer; public ulong NetId { get; set; } = 1; public ulong HostNetId { get; set; } = 1; }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Game.Lobby
{
    public class StartRunLobby
    {
        public MegaCrit.Sts2.Core.Multiplayer.Game.NetService NetService { get; } = new();
        public bool IsCustom { get; set; } = true; public GameMode GameMode { get; set; }
        public List<int> Players { get; set; } = [1]; public int Starts; public bool Ready; public int Ascension = 10;
        public IReadOnlyList<ModifierModel> Modifiers { get; private set; } = Array.Empty<ModifierModel>();
        public Action<IReadOnlyList<ModifierModel>>? OnPreview;
        public Action<IReadOnlyList<ModifierModel>>? OnStart;
        public void SetReady(bool value) { Ready = value; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetGameMode(GameMode value) { GameMode = value; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void HandleGameModeChangedMessage(GameMode value, ulong sender) { GameMode = value; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SyncAscensionChange(int value) { Ascension = value; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void AddLocalHostPlayer() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetModifiers(IReadOnlyCollection<ModifierModel> models) { Modifiers = models.ToArray(); OnPreview?.Invoke(Modifiers); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void InitializeFromMessage(object message) { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void HandleModifiersChangedMessage(MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby.LobbyModifiersChangedMessage message, ulong sender) { Modifiers = message.modifiers.Select(ModifierModel.FromSerializable).ToArray(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void HandleLobbyBeginRunMessage(MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby.LobbyBeginRunMessage message, ulong sender) { Players = message.playersInLobby!; Starts++; OnStart?.Invoke(message.modifiers.Select(ModifierModel.FromSerializable).ToArray()); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void BeginRunForAllPlayers(string seed, List<ModifierModel> modifiers) { Starts++; OnStart?.Invoke(modifiers); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void CleanUp(bool disconnect) { }
    }
    public class LoadRunLobby
    {
        public MegaCrit.Sts2.Core.Multiplayer.Game.NetService NetService { get; } = new();
        public SerializableRun Run { get; set; } = new();
        public List<int> Players { get; set; } = [1];
        public int Starts; public bool Ready;
        public bool IsAboutToBeginGame() => Ready;
        public void SetReady(bool value) { Ready = value; BeginRunForAllPlayersIfAllReady(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void BeginRunForAllPlayersIfAllReady() { if (Ready) Starts++; }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Screens.MainMenu
{
    public class NCustomRunModifiersList { [MethodImpl(MethodImplOptions.NoInlining)] public IEnumerable<ModifierModel> GetAllModifiers() => []; }
}
namespace MegaCrit.Sts2.Core.Nodes
{
    public class NGame
    {
        public int Calls; public RunManager Manager { get; } = new();
        [MethodImpl(MethodImplOptions.NoInlining)] public Task<RunState> StartNewMultiplayerRun(List<MegaCrit.Sts2.Core.Entities.Players.Player> players,
            MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby lobby, bool shouldSave, IReadOnlyList<ActModel> acts, IReadOnlyList<ModifierModel> modifiers,
            string seed, int ascensionLevel, DateTimeOffset? dailyTime = null)
        {
            Calls++;
            var state = new RunState { Players = players.Select(p => (int)p.NetId).ToList(), Acts = acts.Select(a => a.ToMutable()).ToList(), Modifiers = modifiers.ToList(), GameMode = lobby.GameMode, IsCustomGame = lobby.IsCustom, AscensionLevel = ascensionLevel };
            Manager.SetUpNewMultiplayer(state, lobby, shouldSave, dailyTime);
            return Task.FromResult(state);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public Task<RunState> StartNewSingleplayerRun(MegaCrit.Sts2.Core.Entities.Players.Player player,
            bool shouldSave, IReadOnlyList<ActModel> acts, IReadOnlyList<ModifierModel> modifiers, string seed, GameMode mode,
            bool isCustomGame, int ascensionLevel = 0, DateTimeOffset? dailyTime = null)
        {
            Calls++;
            var state = new RunState { Acts = acts.Select(a => a.ToMutable()).ToList(), Modifiers = modifiers.ToList(), GameMode = mode, IsCustomGame = isCustomGame, AscensionLevel = ascensionLevel, UnlockState = player.UnlockState };
            Manager.SetUpNewSingleplayer(state);
            return Task.FromResult(state);
        }
    }
}
namespace MegaCrit.Sts2.Core.Saves
{
    public class SerializedModifier { public ModelId? Id { get; set; } public ModifierModel Value { get; set; } = null!; }
    public class SerializableRun
    {
        public List<SerializedModifier> Modifiers { get; set; } = []; public bool IsCustomGame { get; set; }
        public GameMode GameMode { get; set; } public List<int> Players { get; set; } = [1]; public int Ascension { get; set; }
        public long StartTime { get; set; } public int CurrentActIndex { get; set; }
        public List<MegaCrit.Sts2.Core.Saves.Runs.SerializableActModel> Acts { get; set; } = [];
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect
{
    public class NCharacterSelectScreen : Godot.Control {
        public MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby? Lobby { get; set; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void AfterInitialized() { }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Screens.CustomRun
{
    public class NCustomRunScreen : Godot.Control {
        public MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby? Lobby { get; set; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void AfterInitialized() { }
    }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby
{
    public struct LobbyModifiersChangedMessage { public List<SerializedModifier> modifiers; }
    public struct LobbyBeginRunMessage { public List<SerializedModifier> modifiers; public List<int>? playersInLobby; public string seed; }
}
// Witness APIs: production must not install any patches or alter their values.
namespace MegaCrit.Sts2.Core.Entities.Ascension
{
    public enum AscensionLevel { None, SwarmingElites, WearyTraveler, Poverty, TightBelt, AscendersBane, Inflation, Scarcity, ToughEnemies, DeadlyEnemies, DoubleBoss }
    public class AscensionManager { public int Level = 10; [MethodImpl(MethodImplOptions.NoInlining)] public bool HasLevel(AscensionLevel level) => (int)level >= 1 && (int)level <= Level; }
}
namespace MegaCrit.Sts2.Core.Odds { public static class CardRarityOdds { public static float regularCommonOdds = 0.6f; } }
#if !ACTUAL_GODOT
namespace Godot
{
    public static class GD { public static void PushError(string message) { } public static void PushWarning(string message) { } }
    public static class Engine { public static SceneTree Tree { get; } = new(); public static object GetMainLoop() => Tree; }
    public class Node
    {
        public List<Node> Children { get; } = []; private Node? Parent;
        public string Name { get; set; } = "";
        public event Action? TreeExiting;
        public event Action? Ready;
        public bool IsNodeReady() => true;
        public IEnumerable<Node> GetChildren() => Children;
        public void AddChild(Node child) { Children.Add(child); child.Parent = this; Engine.Tree.Added(child); child.Ready?.Invoke(); }
        public void QueueFree() { Parent?.Children.Remove(this); TreeExiting?.Invoke(); }
    }
    public class SceneTree { public Node Root { get; } = new(); public event Action<Node>? NodeAdded; public void Added(Node node) => NodeAdded?.Invoke(node); }
    public class Callable(Action action) { public static Callable From(Action action) => new(action); public void CallDeferred() => action(); }
    public readonly record struct Vector2(float X, float Y);
    public readonly record struct Vector2I(int X, int Y);
    public static class TextServer { public enum AutowrapMode { WordSmart } }
    public class Control : Node {
        public enum SizeFlags { ExpandFill }
        public SizeFlags SizeFlagsHorizontal { get; set; } public SizeFlags SizeFlagsVertical { get; set; }
        public Vector2 CustomMinimumSize { get; set; }
        public Vector2 Size { get; set; }
        public float AnchorLeft { get; set; } public float AnchorRight { get; set; }
        public float AnchorTop { get; set; } public float AnchorBottom { get; set; } public bool ClipContents { get; set; } public float OffsetLeft { get; set; } public float OffsetBottom { get; set; } public int ZIndex { get; set; }
        public bool Visible { get; set; } = true;
        public bool IsVisibleInTree() => Visible;
        public event Action? VisibilityChanged;
        public void ShowForTest() { Visible = true; VisibilityChanged?.Invoke(); }
        public void AddThemeFontSizeOverride(string name, int size) { }
        public void AddThemeConstantOverride(string name, int size) { }
    }
    public class Container : Control { } public class PanelContainer : Container { } public class MarginContainer : Control { }
    public class VBoxContainer : Control { } public class HBoxContainer : Control { } public class ScrollContainer : Control { }
    public class Label : Control { public string Text { get; set; } = ""; public TextServer.AutowrapMode AutowrapMode { get; set; } }
    public class OptionButton : Control { public bool Disabled { get; set; } public bool FitToLongestItem { get; set; } public Control GetPopup() => new(); public List<string> Items { get; } = []; public int Selected { get; private set; } public void AddItem(string item) => Items.Add(item); public void Select(int index) => Selected = index; public event Action<long>? ItemSelected; public void UserSelect(int index) { Selected = index; ItemSelected?.Invoke(index); } }
    public class CheckBox : Button { public bool ButtonPressed { get; set; } public void SetPressedNoSignal(bool value) => ButtonPressed = value; }
    public class Button : Control { public bool Disabled { get; set; } public string Text { get; set; } = ""; public event Action? Pressed; public void Press() => Pressed?.Invoke(); }
    public class AcceptDialog : Control
    {
        public string Title { get; set; } = ""; public string DialogText { get; set; } = ""; public bool DialogHideOnOk { get; set; }
        public event Action? Confirmed; public event Action? Canceled; public event Action? CloseRequested;
        private readonly Button Ok = new();
        public Button GetOkButton() => Ok;
        public Button AddButton(string text, bool right = false, string action = "") { var b = new Button { Text = text }; AddChild(b); return b; }
        public void PopupCentered(Vector2I size = default) { }
        public void Confirm() => Confirmed?.Invoke(); public void Cancel() => Canceled?.Invoke(); public void Close() => CloseRequested?.Invoke();
    }
}
#endif

// Independent reward-branch witness based on the supplied Neow source fragment.
namespace MegaCrit.Sts2.Core.Models.Events
{
    public class Neow : AncientEventModel
    {
        public IRunState State { get; set; } = new RunState();
        public IReadOnlyList<string> OptionsForTest() => GenerateInitialOptions();
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected IReadOnlyList<string> GenerateInitialOptions()
        {
            if (State.Modifiers.Count > 0) return GenerateNeowModifiers();
            return new[] { "standard-neow-reward" };
        }
        private IReadOnlyList<string> GenerateNeowModifiers() => State.Modifiers.Where(m => m.GenerateNeowOption(this) != null).Select(m => m.Title.GetFormattedText()).ToArray();
    }
    public sealed class FixtureRewardModifier : ModifierModel
    {
        public override Func<Task>? GenerateNeowOption(EventModel model) => () => Task.CompletedTask;
    }
}
