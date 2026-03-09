using FortniteReplayReader.Models.Events;
using System.Collections.Generic;
using Unreal.Core.Models;

namespace FortniteReplayReader.Models;

public class FortniteReplay : Replay
{
    public IList<PlayerElimination> Eliminations { get; internal set; } = new List<PlayerElimination>();
    public Stats Stats { get; internal set; }
    public TeamStats TeamStats { get; internal set; }
    public GameData GameData { get; internal set; } = new GameData();
    public IEnumerable<TeamData> TeamData { get; internal set; }
    public IEnumerable<PlayerData> PlayerData { get; internal set; }
    public IEnumerable<Inventory> Inventories { get; internal set; }
    public IEnumerable<WeaponData> Weapons { get; internal set; }
    public IList<KillFeedEntry> KillFeed { get; internal set; } = new List<KillFeedEntry>();
    public IList<DamageEvent> DamageEvents { get; internal set; } = new List<DamageEvent>();
    public MapData MapData { get; internal set; } = new MapData();
}
