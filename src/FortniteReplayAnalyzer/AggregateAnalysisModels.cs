namespace FortniteReplayAnalyzer;

internal enum AggregateRangeOption
{
    AllTime,
    Last7Days,
    Last30Days,
    Custom
}

internal sealed class WeaponStatsRow
{
    public required string WeaponType { get; init; }
    public required string WeaponName { get; init; }
    public int Hits { get; init; }
    public int CriticalHits { get; init; }
    public int FatalHits { get; init; }
    public float TotalDamage { get; init; }
    public float AvgDamage { get; init; }
    public float CriticalRate { get; init; }
}

internal sealed class DamageTimelinePoint
{
    public double TimeValue { get; init; }
    public float Damage { get; init; }
}
