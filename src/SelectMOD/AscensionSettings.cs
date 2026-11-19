namespace SelectMOD;

// This value object has no dependency on custom-run screens or multiplayer UI.
public readonly record struct AscensionSettings(int Schema, int Mask)
{
    public const int CurrentSchema = 2;
    public const int All = (1 << 10) - 1;
    public void Validate()
    {
        if (Schema != CurrentSchema || Mask < 0 || (Mask & ~All) != 0)
            throw new InvalidOperationException("SelectMOD: アセンション設定が欠落・破損、または未対応です。");
    }
    public bool Contains(int level) => level is >= 1 and <= 10 && (Mask & (1 << (level - 1))) != 0;
    public string Describe() => Mask == 0 ? "全OFF" : string.Join(", ", Enumerable.Range(1, 10).Where(Contains).Select(n => "A" + n));
}
