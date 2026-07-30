using Guildwright.Core.Weapons;

namespace Guildwright.Core.Items;

/// <summary>
/// 상점 카탈로그 (docs/07 §20.3 — 대장간은 무기, 약국은 포션).
/// <para>
/// 파는 것은 §20.7이 확정한 초급 아이템뿐이다 — 나무·철·강철 × 검·방패 6종과
/// 소형 포션 2종. 은퇴 모험가 제작(재료 아이템)은 이후 단계다.
/// </para>
/// <para>
/// ⚠️ <b>가격은 전부 임시값이다.</b> 배치 시뮬레이션으로 경제를 측정한 뒤 사람이 정한다.
/// 감으로 고치지 말고 근거를 docs/08-balance-log.md에 남긴다.
/// </para>
/// </summary>
public static class Shop
{
    /// <summary>대장간 — 무기 6종. 표 순서가 화면 순서다.</summary>
    public static IReadOnlyList<(WeaponItem Item, int Price)> Smithy { get; } =
    [
        (new WeaponItem(WeaponKind.Sword, WeaponMaterial.Wood), 20),
        (new WeaponItem(WeaponKind.Shield, WeaponMaterial.Wood), 15),
        (new WeaponItem(WeaponKind.Sword, WeaponMaterial.Iron), 80),
        (new WeaponItem(WeaponKind.Shield, WeaponMaterial.Iron), 70),
        (new WeaponItem(WeaponKind.Sword, WeaponMaterial.Steel), 200),
        (new WeaponItem(WeaponKind.Shield, WeaponMaterial.Steel), 180)
    ];

    /// <summary>약국 — 소형 포션 2종.</summary>
    public static IReadOnlyList<(ConsumableKind Item, int Price)> Apothecary { get; } =
    [
        (ConsumableKind.HealthPotionSmall, 30),
        (ConsumableKind.ManaPotionSmall, 30)
    ];

    public static int PriceOf(WeaponItem item) =>
        Smithy.First(o => o.Item == item).Price;

    public static int PriceOf(ConsumableKind kind) =>
        Apothecary.First(o => o.Item == kind).Price;
}
