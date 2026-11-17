namespace SelectMOD;

public readonly record struct ProbeSettings(int Schema, int Value)
{
    public const int CurrentSchema = 1;
    public void Validate()
    {
        if (Schema != CurrentSchema)
            throw new InvalidOperationException("SelectMOD: 保存設定の形式が不明または欠落しています。");
        if (Value is < 0 or > 1000000)
            throw new InvalidOperationException("SelectMOD: 設定値は0～1000000で指定してください。");
    }
}
