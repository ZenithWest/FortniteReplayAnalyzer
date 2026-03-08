using FortniteReplayReader;
using System;

var path = @"C:\Users\Zenit\AppData\Local\FortniteGame\Saved\Demos\UnsavedReplay-2026.03.07-22.16.57.replay";
var replay = new ReplayReader(parseMode: Unreal.Core.Models.Enums.ParseMode.Full).ReadReplay(path);
Console.WriteLine($"KillFeed={replay.KillFeed.Count}");
Console.WriteLine($"DamageEvents={replay.DamageEvents.Count}");
if (replay.DamageEvents.Count > 0)
{
    var first = replay.DamageEvents[0];
    Console.WriteLine($"First: t={first.ReplicatedWorldTimeSecondsDouble ?? first.ReplicatedWorldTimeSeconds}, inst={first.InstigatorId}/{first.InstigatorName}, target={first.TargetId}/{first.TargetName}, mag={first.Magnitude}, loc={first.Location}");
}
