using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
namespace SelectMOD;
public static class ProbeRules
{
    public static void ValidateSelection(IEnumerable<ModifierModel> modifiers, bool isCustom, GameMode mode, int players)
    {
        var models = modifiers.ToArray();
        var ascensions = models.OfType<SelectAscensionSettings>().ToArray();
        if (ascensions.Length > 0)
        {
            if (!ModEntry.IsActive || ascensions.Length != 1 || mode != GameMode.Standard || players is < 1 or > 4)
                throw new InvalidOperationException("SelectMOD: 個別アセンションのモード／人数／件数が不正です。");
            ascensions[0].Snapshot.Validate();
        }
        if (models.OfType<SelectLobbySettings>().Any()) throw new InvalidOperationException("SelectMOD: 未確定のロビー設定がランに混入しています。");
        var probes = models.OfType<SelectSettingsProbe>().ToArray();
        var encounters = models.OfType<SelectEncounterSettings>().ToArray();
        if (probes.Length == 0 && encounters.Length == 0) return;
        if (!ModEntry.IsActive) throw new InvalidOperationException("SelectMODは初期化されていません。");
        if (probes.Length > 1 || encounters.Length > 1) throw new InvalidOperationException("SelectMOD: 設定が重複しています。");
        if (!isCustom || mode != GameMode.Standard || players < 1 || players > MultiplayerContract.MaxPlayers || (probes.Length != 0 && players != 1)) throw new InvalidOperationException("SelectMOD: 選択ランは1～4人のStandardカスタム扱い専用です。");
        if (probes.Length == 1) probes[0].Snapshot.Validate();
        if (encounters.Length == 1)
        {
            var model = encounters[0]; var rules = model.ReadRules();
            if (model.SelectMultiplayerOrigin.Length != 0)
            {
                var origin = MultiplayerContract.Read(model.SelectMultiplayerOrigin);
                if (model.SelectMultiplayerPlayers != players || players < 2 || origin.Draft.Choices.Length != rules.Length)
                    throw new InvalidOperationException("SelectMOD: 同期設定の人数／Act数が一致しません。");
                for (var i = 0; i < rules.Length; i++)
                    if (origin.Draft.Choices[i].Boss != rules[i].Boss || origin.Draft.Choices[i].Ancient != rules[i].Ancient)
                        throw new InvalidOperationException("SelectMOD: ホストの確定設定とラン設定が一致しません。");
            }
            else if (players != 1 || model.SelectMultiplayerPlayers != 0) throw new InvalidOperationException("SelectMOD: マルチの確定設定が欠落しています。");
        }
    }
    public static void ValidateRun(RunState state)
    {
        ValidateSelection(state.Modifiers, state.IsCustomGame, state.GameMode, state.Players.Count);
        ValidateAscension(state.Modifiers, state.AscensionLevel);
    }

    internal static void ValidateAscension(IEnumerable<ModifierModel> models, int level)
    {
        var selection = models.OfType<SelectAscensionSettings>().SingleOrDefault();
        if (selection != null && (selection.Snapshot.Mask & ~AscensionPreference.Stack(level)) != 0)
            throw new InvalidOperationException("SelectMOD: 選択レベルを超えています。");
        foreach (var model in models.OfType<SelectEncounterSettings>().Where(m => m.SelectMultiplayerOrigin.Length != 0))
        {
            var draft = MultiplayerContract.Read(model.SelectMultiplayerOrigin).Draft;
            var stack = AscensionPreference.Stack(level);
            var expected = (draft.AscensionMask ?? AscensionSettings.All) & stack;
            if (draft.Ascension != level || (selection?.Snapshot.Mask ?? stack) != expected)
                throw new InvalidOperationException("SelectMOD: 保存アセンションがホストの開始設定と一致しません。");
        }
    }
}
