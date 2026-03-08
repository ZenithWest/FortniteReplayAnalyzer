using Unreal.Core.Models;

namespace FortniteReplayReader.Models;

public class DamageEvent
{
    public int? InstigatorId { get; set; }
    public string? InstigatorName { get; set; }
    public bool InstigatorIsBot { get; set; }
    public int? TargetId { get; set; }
    public string? TargetName { get; set; }
    public bool TargetIsBot { get; set; }
    public float? ReplicatedWorldTimeSeconds { get; set; }
    public double? ReplicatedWorldTimeSecondsDouble { get; set; }
    public float? Magnitude { get; set; }
    public bool? IsFatal { get; set; }
    public bool? IsCritical { get; set; }
    public bool? IsShield { get; set; }
    public bool? IsShieldDestroyed { get; set; }
    public bool? IsShieldApplied { get; set; }
    public bool? IsBallistic { get; set; }
    public FVector? Location { get; set; }
    public FVector? Normal { get; set; }
}
