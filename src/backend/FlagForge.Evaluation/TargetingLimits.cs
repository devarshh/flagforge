namespace FlagForge.Evaluation;

/// <summary>Limits enforced by <see cref="TargetingValidator"/>. The dashboard mirrors these values.</summary>
public static class TargetingLimits
{
    /// <summary>100% expressed in thousandths of a percent.</summary>
    public const int TotalWeight = 100_000;

    public const int MaxRules = 50;
    public const int MinClausesPerRule = 1;
    public const int MaxClausesPerRule = 10;
    public const int MaxValuesPerClause = 500;
    public const int MaxContextKeysPerTarget = 1000;
    public const int MaxRuleIdLength = 64;
    public const int MaxRuleDescriptionLength = 200;
}
