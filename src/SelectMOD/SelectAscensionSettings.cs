using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SelectMOD;

// Model presence means independent mode; absence means the game's normal mode.
// Missing fields use invalid sentinels. An explicit zero mask is valid all-OFF.
public sealed class SelectAscensionSettings : ModifierModel
{
    private int _schema = -1;
    private int _mask = -1;
    private bool _frozen;
    [SavedProperty]
    public int SelectAscensionSchema
    {
        get => _schema;
        set { EnsureChangeAllowed(_schema, value); _schema = value; }
    }
    [SavedProperty]
    public int SelectAscensionMask
    {
        get => _mask;
        set { EnsureChangeAllowed(_mask, value); _mask = value; }
    }
    public AscensionSettings Snapshot => new(_schema, _mask);
    public override LocString Title => ProbeText.Create("ascension_title", "SelectMOD 個別アセンション");
    public override LocString Description => ProbeText.Create("ascension_description",
        $"ON: {Snapshot.Describe()}。選んだレベルまでの有効な効果を使います。");
    public AscensionSettings Freeze()
    {
        Snapshot.Validate();
        _frozen = true;
        return Snapshot;
    }
    private void EnsureChangeAllowed(int oldValue, int newValue)
    {
        if (_frozen && oldValue != newValue)
            throw new InvalidOperationException("SelectMOD: 開始済みランの設定は変更できません。");
    }
    protected override void AfterRunCreated(RunState runState) => Observe(runState, "ASCENSION_CREATED");
    protected override void AfterRunLoaded(RunState runState) => Observe(runState, "ASCENSION_LOADED");
    private void Observe(RunState state, string stage)
    {
        ProbeRules.ValidateRun(state);
        ModEntry.WriteLog($"{stage} schema={_schema} mask={_mask} enabled={Snapshot.Describe()}");
    }
}
