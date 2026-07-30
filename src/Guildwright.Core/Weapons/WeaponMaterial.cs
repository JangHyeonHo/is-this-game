namespace Guildwright.Core.Weapons;

/// <summary>
/// 무기 재질 (docs/07 §20.7 — 초급 아이템은 나무·철·강철 × 검·방패).
/// <para>
/// 재질은 <b>위력만</b> 바꾼다. 속도·사거리·숙련은 무기 종류의 것이다 —
/// 철검에서 강철검으로 바꿔도 검 숙련은 그대로 이어진다.
/// </para>
/// </summary>
public enum WeaponMaterial
{
    Wood,
    Iron,
    Steel
}

public static class WeaponMaterials
{
    /// <summary>
    /// 재질의 위력 배율.
    /// <para>
    /// ⚠️ <b>임시값.</b> 나무 0.45는 옛 나무검(검 위력의 45%)의 값을 그대로 승계했고,
    /// 철 1.0이 기존 무기의 위력이다. 강철은 배치 시뮬레이션으로 검증 전이다 —
    /// 감으로 고치지 말고 근거를 docs/08-balance-log.md에 남긴다.
    /// </para>
    /// </summary>
    public static double PowerFactor(this WeaponMaterial material) => material switch
    {
        WeaponMaterial.Wood => 0.45,
        WeaponMaterial.Iron => 1.00,
        WeaponMaterial.Steel => 1.15,
        _ => 1.00
    };

    public static string ToKorean(this WeaponMaterial material) => material switch
    {
        WeaponMaterial.Wood => "나무",
        WeaponMaterial.Iron => "철",
        WeaponMaterial.Steel => "강철",
        _ => "?"
    };
}
