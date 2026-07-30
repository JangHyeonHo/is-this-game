namespace Guildwright.Core.Adventurers;

/// <summary>
/// 전투에 실제로 쓰이는 수치. 원천 능력치에서 계산되지만 <b>보정치로 직접 오르기도 합니다.</b>
/// </summary>
public enum DerivedStat
{
    MaxHp,
    MaxMana,
    /// <summary>물리 위력.</summary>
    PhysicalPower,
    /// <summary>물리 방어.</summary>
    PhysicalGuard,
    /// <summary>마법 위력.</summary>
    MagicPower,
    /// <summary>마법 방어.</summary>
    MagicGuard,
    /// <summary>행동 속도.</summary>
    ActionSpeed,
    /// <summary>치명타율 (%p 단위로 보정).</summary>
    CritChance,
    /// <summary>회피율 (%p 단위로 보정).</summary>
    EvasionChance
}

/// <summary>
/// 원천 능력치에 더해지는 파생 보정치.
/// <para>
/// <b>훈련이 아니라 겪은 것에서 옵니다.</b> 계속 맞다 보면 몸이 단단해지고,
/// 급소를 노리다 보면 손에 익습니다.
/// </para>
/// <para>
/// 이게 있어야 <b>원천 능력치가 같아도 이력이 다르면 다른 캐릭터</b>가 됩니다.
/// 그림 없이 개성을 만드는 장치이기도 합니다.
/// </para>
/// 근거: docs/01-game-design.md §3.3
/// </summary>
public sealed class DerivedBonuses
{
    private readonly Dictionary<DerivedStat, double> _values =
        Enum.GetValues<DerivedStat>().ToDictionary(s => s, _ => 0.0);

    /// <summary>보정 상한. 보정만으로 캐릭터가 완성되면 원천 능력치가 무의미해집니다.</summary>
    private static readonly Dictionary<DerivedStat, double> Caps = new()
    {
        [DerivedStat.MaxHp] = 60,
        [DerivedStat.MaxMana] = 30,
        [DerivedStat.PhysicalPower] = 20,
        [DerivedStat.PhysicalGuard] = 20,
        [DerivedStat.MagicPower] = 20,
        [DerivedStat.MagicGuard] = 20,
        [DerivedStat.ActionSpeed] = 15,
        [DerivedStat.CritChance] = 0.12,
        [DerivedStat.EvasionChance] = 0.08
    };

    public double this[DerivedStat stat] => _values[stat];

    internal void Add(DerivedStat stat, double amount)
    {
        _values[stat] = Math.Clamp(_values[stat] + amount, -Caps[stat], Caps[stat]);
    }

    public bool HasAny => _values.Values.Any(v => Math.Abs(v) > 0.001);

    public override string ToString() =>
        HasAny
            ? string.Join(" ", _values.Where(kv => Math.Abs(kv.Value) > 0.001)
                                      .Select(kv => $"{kv.Key}+{kv.Value:F1}"))
            : "없음";
}

/// <summary>
/// 원천 능력치와 보정치로부터 전투 수치를 계산합니다.
/// <para>
/// ⚠️ 계수는 전부 임시값입니다. 배치 시뮬레이션으로 검증한 뒤 데이터로 분리합니다.
/// </para>
/// </summary>
public static class DerivedStats
{
    // ⚠️ 방어 계수를 함부로 올리지 마세요. 처음에 방어를 활력*0.5 + 힘*0.3으로 잡았더니
    //    방어(23)가 위력(24)과 맞먹어 한 대에 11밖에 안 들어갔고, 전투가 37라운드까지 늘어졌습니다.
    //    방어는 피해를 "줄이는" 것이지 "막는" 것이 아니어야 합니다.

    public static int MaxHp(PrimaryStats p, DerivedBonuses? b = null) =>
        Round(p.Vitality * 2.8 + p.Strength * 0.8 + Bonus(b, DerivedStat.MaxHp), min: 1);

    public static int MaxMana(PrimaryStats p, DerivedBonuses? b = null) =>
        Round(p.Spirit * 2.0 + p.Intellect * 1.0 + Bonus(b, DerivedStat.MaxMana), min: 0);

    public static int PhysicalPower(PrimaryStats p, DerivedBonuses? b = null) =>
        Round(p.Strength * 1.0 + p.Finesse * 0.3 + Bonus(b, DerivedStat.PhysicalPower), min: 1);

    public static int PhysicalGuard(PrimaryStats p, DerivedBonuses? b = null) =>
        Round(p.Vitality * 0.35 + p.Strength * 0.15 + Bonus(b, DerivedStat.PhysicalGuard), min: 0);

    public static int MagicPower(PrimaryStats p, DerivedBonuses? b = null) =>
        Round(p.Intellect * 1.0 + p.Spirit * 0.2 + Bonus(b, DerivedStat.MagicPower), min: 1);

    public static int MagicGuard(PrimaryStats p, DerivedBonuses? b = null) =>
        Round(p.Spirit * 0.5 + p.Vitality * 0.15 + Bonus(b, DerivedStat.MagicGuard), min: 0);

    public static double ActionSpeed(PrimaryStats p, DerivedBonuses? b = null) =>
        Math.Max(1.0, p.Agility * 1.0 + p.Finesse * 0.2 + Bonus(b, DerivedStat.ActionSpeed));

    /// <summary>기본 치명타율. 무기 스타일 보정은 전투에서 곱해집니다.</summary>
    public static double CritChance(PrimaryStats p, DerivedBonuses? b = null) =>
        Math.Clamp(0.03 + p.Finesse / 1000.0 + Bonus(b, DerivedStat.CritChance), 0.0, 0.35);

    /// <summary>기본 회피율. 상대 속도에 따라 전투에서 조정됩니다.</summary>
    public static double EvasionChance(PrimaryStats p, DerivedBonuses? b = null) =>
        Math.Clamp(0.02 + p.Agility / 1250.0 + Bonus(b, DerivedStat.EvasionChance), 0.0, 0.25);

    private static double Bonus(DerivedBonuses? b, DerivedStat stat) => b?[stat] ?? 0.0;

    private static int Round(double value, int min) => Math.Max(min, (int)Math.Round(value));

    public static string ToKorean(this DerivedStat stat) => stat switch
    {
        DerivedStat.MaxHp => "최대 HP",
        DerivedStat.MaxMana => "최대 마나",
        DerivedStat.PhysicalPower => "물리 위력",
        DerivedStat.PhysicalGuard => "물리 방어",
        DerivedStat.MagicPower => "마법 위력",
        DerivedStat.MagicGuard => "마법 방어",
        DerivedStat.ActionSpeed => "행동 속도",
        DerivedStat.CritChance => "치명타율",
        DerivedStat.EvasionChance => "회피율",
        _ => "?"
    };
}
