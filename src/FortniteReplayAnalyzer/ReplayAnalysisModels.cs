using FortniteReplayReader.Models;

namespace FortniteReplayAnalyzer;

internal sealed class ReplayBrowserRow
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required DateTime RecordedAt { get; init; }
    public required string RecordedAtText { get; init; }
    public required TimeSpan Duration { get; init; }
    public required string DurationText { get; init; }
    public int? Placement { get; init; }
    public required string PlacementText { get; init; }
    public uint? Kills { get; init; }
    public required string KillsText { get; init; }
    public int PlayerCount { get; init; }
    public required string PlayerCountText { get; init; }
    public FortniteReplay? Replay { get; init; }
    public required string Status { get; init; }

    public static ReplayBrowserRow CreateError(string filePath, string message)
    {
        var recordedAt = File.GetLastWriteTime(filePath);

        return new ReplayBrowserRow
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            RecordedAt = recordedAt,
            RecordedAtText = recordedAt.ToString("g"),
            Duration = TimeSpan.Zero,
            DurationText = "-",
            PlacementText = "-",
            KillsText = "-",
            PlayerCountText = "-",
            Status = "Error: " + message,
            PlayerCount = 0
        };
    }
}

internal sealed class PlayerSummaryRow
{
    public required PlayerData Player { get; init; }
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
    public required double TimeValue { get; init; }
    public required string TimeText { get; init; }
    public required string ActorName { get; init; }
    public int? ActorId { get; init; }
    public string? ActorLookupKey { get; init; }
    public required string EventText { get; init; }
    public required string TargetName { get; init; }
    public int? TargetId { get; init; }
    public string? TargetLookupKey { get; init; }
    public required string DistanceText { get; init; }
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
    public required string PlayerName { get; init; }
    public required string EventText { get; init; }
    public required string TimeText { get; init; }
    public required string DistanceText { get; init; }
}
