// Minimal independent contracts for exercising our code. NOT game implementations,
// production references, or evidence of actual STS2 serialization/loader behavior.
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
namespace MegaCrit.Sts2.Core.Modding { public class ModInitializerAttribute(string name) : Attribute { } }
namespace MegaCrit.Sts2.Core.Saves.Runs { public class SavedPropertyAttribute : Attribute { } }
namespace MegaCrit.Sts2.Core.Localization
{
    public class LocString(string table, string key)
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public string GetFormattedText() => table + ":" + key;
    }
}
namespace MegaCrit.Sts2.Core.Models
{
    public abstract class ModifierModel
    {
        public virtual MegaCrit.Sts2.Core.Localization.LocString Title => new("test", "title");
        public virtual MegaCrit.Sts2.Core.Localization.LocString Description => new("test", "description");
        public ModifierModel ToMutable() => (ModifierModel)MemberwiseClone();
        protected virtual void AfterRunCreated(RunState state) { }
        protected virtual void AfterRunLoaded(RunState state) { }
        public void OnRunCreated(RunState state) => AfterRunCreated(state);
        public void OnRunLoaded(RunState state) => AfterRunLoaded(state);
        public static ModifierModel FromSerializable(SerializedModifier saved) => saved.Value;
    }
    public static class ModelDb
    {
        public static T Modifier<T>() where T : ModifierModel, new() => new();
        public static string GetId<T>() => typeof(T).Name;
    }
}
namespace MegaCrit.Sts2.Core.Runs
{
    public enum GameMode { Standard, Blitz }
    public class RunState
    {
        public List<ModifierModel> Modifiers { get; set; } = [];
        public bool IsCustomGame { get; set; } = true;
        public GameMode GameMode { get; set; }
        public List<int> Players { get; set; } = [1];
        public static int Restores;
        [MethodImpl(MethodImplOptions.NoInlining)] public static RunState FromSerializable(SerializableRun save) { Restores++; return new(); }
    }
    public class RunManager
    {
        public int Starts;
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetUpNewSingleplayer(RunState state, bool save = true, DateTimeOffset? dailyTime = null) { Starts++; }
        [MethodImpl(MethodImplOptions.NoInlining)] public Task SetUpSavedSingleplayer(RunState state, SerializableRun save) { Starts++; return Task.CompletedTask; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void SetUpNewMultiplayer(RunState state, MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby lobby, bool save = true) { Starts++; }
        [MethodImpl(MethodImplOptions.NoInlining)] public Task SetUpSavedMultiplayer(RunState state, MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.LoadRunLobby lobby) { Starts++; return Task.CompletedTask; }
    }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Game
{
    public enum NetGameType { None, Singleplayer, Host, Client, Replay }
    public class NetService { public NetGameType Type { get; set; } = NetGameType.Singleplayer; }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Game.Lobby
{
    public class StartRunLobby
    {
        public MegaCrit.Sts2.Core.Multiplayer.Game.NetService NetService { get; } = new();
        public bool IsCustom { get; set; } = true;
        public GameMode GameMode { get; set; }
        public List<int> Players { get; } = [1];
        public int Starts;
        public bool Ready;
        public void SetReady(bool value) => Ready = value;
        [MethodImpl(MethodImplOptions.NoInlining)] public void BeginRunForAllPlayers(string seed, List<ModifierModel> modifiers) { Starts++; }
    }
    public class LoadRunLobby
    {
        public MegaCrit.Sts2.Core.Multiplayer.Game.NetService NetService { get; } = new();
        public int Starts;
        public bool Ready;
        public bool IsAboutToBeginGame() => Ready;
        public void SetReady(bool value) { Ready = value; BeginRunForAllPlayersIfAllReady(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void BeginRunForAllPlayersIfAllReady() { if (Ready) Starts++; }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Screens.MainMenu
{
    public class NCustomRunModifiersList
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public IEnumerable<ModifierModel> GetAllModifiers() => [];
    }
}
namespace MegaCrit.Sts2.Core.Saves
{
    public class SerializedModifier { public string Id { get; set; } = ""; public ModifierModel Value { get; set; } = null!; }
    public class SerializableRun
    {
        public List<SerializedModifier> Modifiers { get; set; } = [];
        public bool IsCustomGame { get; set; } = true;
        public GameMode GameMode { get; set; }
        public List<int> Players { get; set; } = [1];
    }
}
namespace Godot
{
    public static class GD { public static void PushError(string message) { } public static void PushWarning(string message) { } }
    public static class Engine { public static object GetMainLoop() => new object(); }
    public class Node { public void AddChild(Node child) { } public void QueueFree() { } }
    public class SceneTree { public Node Root { get; } = new(); }
    public class AcceptDialog : Node
    {
        public string Title { get; set; } = ""; public string DialogText { get; set; } = "";
        public event Action? Confirmed; public event Action? CloseRequested;
        public void PopupCentered() { }
    }
}
