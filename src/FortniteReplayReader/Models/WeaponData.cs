namespace FortniteReplayReader.Models;

public class WeaponData
{
    public uint? ChannelId { get; set; }
    public uint? OwnerActor { get; set; }
    public uint? InstigatorActor { get; set; }
    public bool? bIsEquippingWeapon { get; set; }
    public bool? bIsReloadingWeapon { get; set; }
    public string WeaponName { get; set; }
    public string WeaponAssetName { get; set; }
    public string WeaponClassName { get; set; }
    public string WeaponType { get; set; }
    public float? LastFireTimeVerified { get; set; }
    public int? WeaponLevel { get; set; }
    public int? AmmoCount { get; set; }

    public uint? A { get; set; }
    public uint? B { get; set; }
    public uint? C { get; set; }
    public uint? D { get; set; }
}
