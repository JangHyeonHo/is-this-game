using Guildwright.Core.Weapons;

namespace Guildwright.Core.Items;

/// <summary>
/// 장비 아이템 하나 — 무기 종류 + 재질 (docs/07 §20.7).
/// "철검"과 "강철검"은 같은 검이라서 숙련을 공유하고, 위력 배율만 다르다.
/// </summary>
public readonly record struct WeaponItem(WeaponKind Kind, WeaponMaterial Material)
{
    /// <summary>표시 이름 — "나무검" · "철방패" · "강철검".</summary>
    public string Korean => $"{Material.ToKorean()}{Kind.ToKorean()}";

    public override string ToString() => Korean;
}
