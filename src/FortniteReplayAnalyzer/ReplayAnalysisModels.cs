using System.Drawing;
using FortniteReplayReader.Models;
using Unreal.Core.Models.Enums;

namespace FortniteReplayAnalyzer;

internal enum DamageParticipantCategory
{
    Player,
    Bot,
    Npc,
    Structure
}

internal sealed class ReplayBrowserRow
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public string RecordedAtText { get; set; } = "-";
    public TimeSpan Duration { get; set; }
    public string DurationText { get; set; } = "-";
    public int? Placement { get; set; }
    public string PlacementText { get; set; } = "-";
    public uint? Kills { get; set; }
    public string KillsText { get; set; } = "-";
    public int PlayerCount { get; set; }
    public string PlayerCountText { get; set; } = "-";
    public FortniteReplay? Replay { get; set; }
    public ReplayViewCache? ViewCache { get; set; }
    public List<ReplayWeaponStatsSnapshot> WeaponStatsSnapshots { get; set; } = [];
    public string Status { get; set; } = "Not loaded";
    public bool IsLoading { get; set; }
    public bool IsQueued { get; set; }
    public bool StopRequested { get; set; }
    public bool SummaryLoaded { get; set; }
    public ParseMode LoadedParseMode { get; set; } = ParseMode.EventsOnly;
    public ParseMode QueuedParseMode { get; set; } = ParseMode.Full;

    public static ReplayBrowserRow CreateFromFile(string filePath)
    {
        var recordedAt = File.GetLastWriteTime(filePath);

        return new ReplayBrowserRow
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            RecordedAt = recordedAt,
            RecordedAtText = recordedAt.ToString("g"),
            Status = "Not loaded"
        };
    }
}

internal sealed class PlayerSummaryRow
{
    public required PlayerData Player { get; init; }
    public Image? ProfileIcon { get; init; }
    public required string DisplayName { get; init; }
    public int? Team { get; init; }
    public required string TeamText { get; init; }
    public int? Placement { get; init; }
    public required string PlacementText { get; init; }
    public uint? Kills { get; init; }
    public required string KillsText { get; init; }
    public required string Platform { get; init; }
    public bool IsBot { get; init; }
}

internal sealed class KillFeedRow
{
    public required KillFeedEntry Entry { get; init; }
    public Image? ActorIcon { get; init; }
    public required double TimeValue { get; init; }
    public required string TimeText { get; init; }
    public required string ActorName { get; init; }
    public int? ActorId { get; init; }
    public string? ActorLookupKey { get; init; }
    public required string EventText { get; init; }
    public Image? TargetIcon { get; init; }
    public required string TargetName { get; init; }
    public int? TargetId { get; init; }
    public string? TargetLookupKey { get; init; }
    public bool ActorIsBot { get; init; }
    public bool TargetIsBot { get; init; }
    public bool ActorIsTeammate { get; init; }
    public bool TargetIsTeammate { get; init; }
    public bool InvolvesOwnerTeam { get; init; }
    public required string ReasonText { get; init; }
    public required string DistanceText { get; init; }
}

internal sealed class CombatEventRow
{
    public required double TimeValue { get; init; }
    public required string TimeText { get; init; }
    public Image? AttackerIcon { get; init; }
    public required string AttackerName { get; init; }
    public int? AttackerId { get; init; }
    public string? AttackerLookupKey { get; init; }
    public Image? TargetIcon { get; init; }
    public required string TargetName { get; init; }
    public int? TargetId { get; init; }
    public string? TargetLookupKey { get; init; }
    public DamageParticipantCategory AttackerCategory { get; init; }
    public DamageParticipantCategory TargetCategory { get; init; }
    public bool InvolvesOwnerTeam { get; init; }
    public required string EventText { get; init; }
    public required string DamageText { get; init; }
    public required string WeaponTypeText { get; init; }
    public required string ShieldText { get; init; }
    public required string FatalText { get; init; }
    public required string CriticalText { get; init; }
    public required string LocationText { get; init; }
}

internal sealed class DetailRow
{
    public DetailRow(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
}

internal sealed class PlayerVictimRow
{
    public Image? PlayerIcon { get; init; }
    public required string PlayerName { get; init; }
    public int? PlayerId { get; init; }
    public string? PlayerLookupKey { get; init; }
    public bool IsBot { get; init; }
    public required string EventText { get; init; }
    public required string ReasonText { get; init; }
    public required string TimeText { get; init; }
    public required string DistanceText { get; init; }
}

internal sealed class PlayerViewCache
{
    public required PlayerData Player { get; init; }
    public required string CacheKey { get; init; }
    public Image? ProfileIcon { get; init; }
    public required string DisplayName { get; init; }
    public required List<DetailRow> OverviewRows { get; init; }
    public required List<KillFeedRow> KillLogRows { get; init; }
    public required List<PlayerVictimRow> VictimRows { get; init; }
    public required List<CombatEventRow> DamageRows { get; init; }
}

internal sealed class ReplayViewCache
{
    public required List<DetailRow> ReplayDetails { get; init; }
    public required List<KillFeedRow> KillFeedRows { get; init; }
    public required List<CombatEventRow> CombatEventRows { get; init; }
    public required List<PlayerSummaryRow> PlayerRows { get; init; }
    public required Dictionary<string, PlayerViewCache> PlayerCaches { get; init; }
}

internal sealed class ReplayOwnerSummary
{
    public required PlayerData Owner { get; init; }
    public int HitsGiven { get; init; }
    public int HitsTaken { get; init; }
    public float DamageToPlayers { get; init; }
    public float DamageToBots { get; init; }
    public float DamageToStructures { get; init; }
    public float DamageTakenFromPlayers { get; init; }
    public float DamageTakenFromBots { get; init; }
    public float DamageTakenFromStructures { get; init; }
}

