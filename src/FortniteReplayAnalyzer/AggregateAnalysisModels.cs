namespace FortniteReplayAnalyzer;

internal enum AggregateRangeOption
{
    CurrentMatch,
    AllTime,
    Last7Days,
    Last30Days,
    Custom
}

internal sealed class WeaponStatsRow
{
    public required string WeaponType { get; init; }
    public required string WeaponName { get; init; }
    public int MatchesUsed { get; init; }
    public int KillOrDownCount { get; init; }
    public int EliminationCount { get; init; }
    public int Hits { get; init; }
    public int HitsToPlayers { get; init; }
    public int HitsToBots { get; init; }
    public int HitsToNpcs { get; init; }
    public int HitsToStructures { get; init; }
    public int CriticalHits { get; init; }
    public int ShieldHits { get; init; }
    public int FatalHits { get; init; }
    public float TotalDamage { get; init; }
    public float DamageToPlayers { get; init; }
    public float DamageToBots { get; init; }
    public float DamageToNpcs { get; init; }
    public float DamageToStructures { get; init; }
    public float AvgDamage { get; init; }
    public float AvgDamagePerMatch { get; init; }
    public float AvgHitsPerMatch { get; init; }
    public float AvgKillOrDownsPerMatch { get; init; }
    public float CriticalRate { get; init; }
}

internal sealed class WeaponStatsAccumulator
{
    public string WeaponType { get; set; } = "Unknown";
    public string WeaponName { get; set; } = "Unknown";
    public HashSet<string> MatchKeys { get; } = [];
    public int KillOrDownCount { get; set; }
    public int EliminationCount { get; set; }
    public int Hits { get; set; }
    public int HitsToPlayers { get; set; }
    public int HitsToBots { get; set; }
    public int HitsToNpcs { get; set; }
    public int HitsToStructures { get; set; }
    public int CriticalHits { get; set; }
    public int ShieldHits { get; set; }
    public int FatalHits { get; set; }
    public float TotalDamage { get; set; }
    public float DamageToPlayers { get; set; }
    public float DamageToBots { get; set; }
    public float DamageToNpcs { get; set; }
    public float DamageToStructures { get; set; }
}

internal sealed class DamageTimelinePoint
{
    public double TimeValue { get; init; }
    public float Damage { get; init; }
}

internal sealed class MatchTrendRow
{
    public required string Label { get; init; }
    public float DamageToPlayersAndBots { get; init; }
    public int Kills { get; init; }
}
