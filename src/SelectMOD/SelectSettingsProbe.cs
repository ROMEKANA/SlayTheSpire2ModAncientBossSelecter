using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SelectMOD;

// Real properties are discovered by the game's normal model/property cache.
// Invalid defaults detect a save with missing fields, without conflating zero with missing.
public sealed class SelectSettingsProbe : ModifierModel
{
    [SavedProperty] public int SelectProbeSchema { get; set; } = -1;
    [SavedProperty] public int SelectProbeValue { get; set; } = -1;

    public ProbeSettings Snapshot => new(SelectProbeSchema, SelectProbeValue);
    public override LocString Title => ProbeText.Create("title", "SelectMOD 設定保存テスト");
    public override LocString Description => ProbeText.Create("description",
        $"設定値 {SelectProbeValue} を保存します。効果の変更なし・ソロ専用の試作です。");

    protected override void AfterRunCreated(RunState runState) => Observe(runState, "RUN_CREATED");
    protected override void AfterRunLoaded(RunState runState) => Observe(runState, "RUN_LOADED");

    private void Observe(RunState state, string stage)
    {
        ProbeRules.ValidateRun(state);
        ModEntry.WriteLog($"{stage} schema={SelectProbeSchema} value={SelectProbeValue}");
    }
}
