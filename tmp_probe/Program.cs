using System;
using System.Linq;
using FortniteReplayReader;
using Unreal.Core.Models.Enums;

var reader = new ReplayReader(parseMode: ParseMode.Full);
var replay = reader.ReadReplay(@"C:\Users\Zenit\AppData\Local\FortniteGame\Saved\Demos\UnsavedReplay-2026.03.08-21.36.37.replay");
Console.WriteLine($"DamageEvents={replay.DamageEvents.Count}");
foreach (var row in replay.DamageEvents.Take(30))
{
    Console.WriteLine($"{row.EventSource} | inst={row.InstigatorName ?? row.InstigatorId?.ToString()} | tgt={row.TargetName ?? row.TargetId?.ToString()} | wType={row.WeaponType ?? "<null>"} | wName={row.WeaponName ?? "<null>"} | wAsset={row.WeaponAssetName ?? "<null>"} | wClass={row.WeaponClassName ?? "<null>"} | tag={row.EventTag ?? "<null>"}");
}
Console.WriteLine("Grouped:");
foreach (var g in replay.DamageEvents.GroupBy(x => new { x.WeaponType, x.WeaponName, x.WeaponAssetName, x.WeaponClassName }).OrderByDescending(g => g.Count()).Take(20))
{
    Console.WriteLine($"count={g.Count()} type={g.Key.WeaponType ?? "<null>"} name={g.Key.WeaponName ?? "<null>"} asset={g.Key.WeaponAssetName ?? "<null>"} class={g.Key.WeaponClassName ?? "<null>"}");
}
