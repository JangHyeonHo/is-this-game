using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Weapons;

/// <summary>
/// 장비 형태. <b>적성이 붙는 축이자, 파티에서의 역할을 결정하는 축</b>입니다.
/// <para>
/// 무기를 개별 아이템이 아니라 형태로 묶은 이유는, 무기가 "얼마나 세냐"가 아니라
/// <b>"파티에서 무슨 역할을 할 수 있냐"</b>를 정해야 하기 때문입니다.
/// 능력치가 이미 강함을 담당하고 있으므로, 무기가 그걸 반복하면 축이 중복됩니다.
/// </para>
/// </summary>
public enum WeaponStyle
{
    /// <summary>한손무기 + 방패. 방어와 도발.</summary>
    SwordAndShield,
    /// <summary>쌍수. 다단 공격과 선제.</summary>
    DualWield,
    /// <summary>양손무기. 고위력과 광역, 대신 느림.</summary>
    TwoHanded,
    /// <summary>활. 곡사라 후열을 직접 노립니다.</summary>
    Bow,
    /// <summary>석궁. 직사·관통. 방어를 무시하지만 느립니다.</summary>
    Crossbow,
    /// <summary>지팡이. 마법 공격과 회복.</summary>
    Staff,
    /// <summary>창·언월도. 긴 리치로 후열에서도 전열을 칩니다.</summary>
    Polearm
}

/// <summary>
/// 무기종. 데미지 특성만 바꿉니다.
/// <para>
/// <b>여기에는 적성이 붙지 않습니다.</b> 순수하게 플레이어가 고르는 레버로 남겨,
/// 통제 불가능한 랜덤 축이 늘어나지 않게 합니다.
/// </para>
/// </summary>
public enum WeaponClass
{
    /// <summary>도검. 균형형.</summary>
    Blade,
    /// <summary>둔기. 방어를 일부 무시합니다.</summary>
    Blunt,
    /// <summary>도끼. 고위력 고분산.</summary>
    Axe,
    /// <summary>자루·찌르기. 관통.</summary>
    Pierce
}

/// <summary>전투에서의 위치.</summary>
public enum Row
{
    Front,
    Back
}

/// <summary>
/// 스타일이 여는 전술적 능력.
/// <para>
/// 이 표가 곧 파티 편성 퍼즐의 정답지입니다. "후열을 때릴 수단이 없다",
/// "회복이 없다" 같은 구멍이 여기서 생깁니다.
/// </para>
/// </summary>
/// <param name="CanStrikeBackRow">적 후열을 직접 노릴 수 있는가.</param>
/// <param name="CanActFromBackRow">아군 후열에 서서도 제 몫을 하는가.</param>
/// <param name="CanHeal">회복이 가능한가.</param>
/// <param name="CanTaunt">도발로 공격을 끌 수 있는가.</param>
/// <param name="HitsMultipleTargets">광역 또는 다단 공격인가.</param>
/// <param name="UsesMagic">마법 능력치를 쓰는가.</param>
/// <param name="SpeedModifier">행동 순서 보정.</param>
/// <param name="DamageModifier">기본 위력 보정.</param>
/// <param name="CritChanceModifier">
/// 치명타 확률 배율. 쌍수처럼 자잘하게 자주 터지는 무기가 높습니다.
/// </param>
/// <param name="CritMultiplier">
/// 치명타 배율. 양손무기처럼 한 방이 큰 무기가 높습니다.
/// <para>확률과 배율을 반대로 배치해 스타일 성격이 갈리게 합니다.</para>
/// </param>
public sealed record StyleCapability(
    bool CanStrikeBackRow,
    bool CanActFromBackRow,
    bool CanHeal,
    bool CanTaunt,
    bool HitsMultipleTargets,
    bool UsesMagic,
    double SpeedModifier,
    double DamageModifier,
    double CritChanceModifier,
    double CritMultiplier);

public static class WeaponStyles
{
    public static IReadOnlyList<WeaponStyle> All { get; } = Enum.GetValues<WeaponStyle>();

    private static readonly Dictionary<WeaponStyle, StyleCapability> Capabilities = new()
    {
        [WeaponStyle.SwordAndShield] = new(
            CanStrikeBackRow: false, CanActFromBackRow: false, CanHeal: false, CanTaunt: true,
            HitsMultipleTargets: false, UsesMagic: false, SpeedModifier: 0.95, DamageModifier: 0.85,
            CritChanceModifier: 0.9, CritMultiplier: 1.5),

        // 자잘하게 자주 터집니다.
        [WeaponStyle.DualWield] = new(
            CanStrikeBackRow: false, CanActFromBackRow: false, CanHeal: false, CanTaunt: false,
            HitsMultipleTargets: true, UsesMagic: false, SpeedModifier: 1.20, DamageModifier: 0.95,
            CritChanceModifier: 1.9, CritMultiplier: 1.4),

        // 잘 안 터지지만 터지면 한 방입니다.
        [WeaponStyle.TwoHanded] = new(
            CanStrikeBackRow: false, CanActFromBackRow: false, CanHeal: false, CanTaunt: false,
            HitsMultipleTargets: true, UsesMagic: false, SpeedModifier: 0.80, DamageModifier: 1.35,
            CritChanceModifier: 0.55, CritMultiplier: 2.1),

        [WeaponStyle.Bow] = new(
            CanStrikeBackRow: true, CanActFromBackRow: true, CanHeal: false, CanTaunt: false,
            HitsMultipleTargets: false, UsesMagic: false, SpeedModifier: 1.05, DamageModifier: 0.95,
            CritChanceModifier: 1.3, CritMultiplier: 1.7),

        // 관통. 확률은 보통이고 한 발이 무겁습니다.
        [WeaponStyle.Crossbow] = new(
            CanStrikeBackRow: true, CanActFromBackRow: true, CanHeal: false, CanTaunt: false,
            HitsMultipleTargets: false, UsesMagic: false, SpeedModifier: 0.75, DamageModifier: 1.25,
            CritChanceModifier: 1.0, CritMultiplier: 2.0),

        [WeaponStyle.Staff] = new(
            CanStrikeBackRow: true, CanActFromBackRow: true, CanHeal: true, CanTaunt: false,
            HitsMultipleTargets: true, UsesMagic: true, SpeedModifier: 0.90, DamageModifier: 1.00,
            CritChanceModifier: 0.8, CritMultiplier: 1.8),

        [WeaponStyle.Polearm] = new(
            CanStrikeBackRow: false, CanActFromBackRow: true, CanHeal: false, CanTaunt: false,
            HitsMultipleTargets: false, UsesMagic: false, SpeedModifier: 1.00, DamageModifier: 1.10,
            CritChanceModifier: 1.1, CritMultiplier: 1.6)
    };

    public static StyleCapability CapabilityOf(WeaponStyle style) => Capabilities[style];

    /// <summary>
    /// 스타일이 어느 능력치와 어울리는지. 적성 굴림의 상관관계를 만듭니다.
    /// <para>
    /// 이 가중치가 있어야 "마공 최고인데 대검 적성"같은 모순 캐릭터가 잘 안 나옵니다.
    /// 다만 굴림에 노이즈가 있어서 가끔은 나오고, <b>그게 발견의 재미</b>가 됩니다.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<PrimaryStat, double> AffinityOf(WeaponStyle style) => style switch
    {
        // 방패를 들고 버티려면 몸과 힘이 필요합니다.
        WeaponStyle.SwordAndShield => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Vitality] = 2.0, [PrimaryStat.Strength] = 1.5, [PrimaryStat.Spirit] = 0.6 },

        // 두 자루를 동시에 다루는 건 민첩과 손재주입니다.
        WeaponStyle.DualWield => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Agility] = 2.0, [PrimaryStat.Finesse] = 1.7, [PrimaryStat.Strength] = 1.0 },

        // 대검과 배틀액스는 순수한 완력입니다.
        WeaponStyle.TwoHanded => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 2.4, [PrimaryStat.Vitality] = 1.3 },

        // 활은 조준과 자세, 그리고 시위를 당길 힘.
        WeaponStyle.Bow => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Finesse] = 2.0, [PrimaryStat.Agility] = 1.5, [PrimaryStat.Strength] = 1.0 },

        // 석궁은 힘보다 정확도와 침착함.
        WeaponStyle.Crossbow => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Finesse] = 2.0, [PrimaryStat.Strength] = 1.2, [PrimaryStat.Spirit] = 0.8 },

        // 마법은 지식과 정신력.
        WeaponStyle.Staff => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Intellect] = 2.4, [PrimaryStat.Spirit] = 1.8 },

        // 긴 자루를 다루는 건 힘과 간격 감각.
        WeaponStyle.Polearm => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 1.7, [PrimaryStat.Finesse] = 1.3, [PrimaryStat.Agility] = 1.0 },

        _ => new Dictionary<PrimaryStat, double>()
    };

    public static string ToKorean(this WeaponStyle style) => style switch
    {
        WeaponStyle.SwordAndShield => "한손+방패",
        WeaponStyle.DualWield => "쌍수",
        WeaponStyle.TwoHanded => "양손",
        WeaponStyle.Bow => "활",
        WeaponStyle.Crossbow => "석궁",
        WeaponStyle.Staff => "지팡이",
        WeaponStyle.Polearm => "창",
        _ => "?"
    };

    public static string ToKorean(this WeaponClass weaponClass) => weaponClass switch
    {
        WeaponClass.Blade => "도검",
        WeaponClass.Blunt => "둔기",
        WeaponClass.Axe => "도끼",
        WeaponClass.Pierce => "자루",
        _ => "?"
    };

    /// <summary>해당 스타일에 장착 가능한 무기종.</summary>
    public static IReadOnlyList<WeaponClass> AllowedClasses(WeaponStyle style) => style switch
    {
        WeaponStyle.SwordAndShield => [WeaponClass.Blade, WeaponClass.Blunt, WeaponClass.Axe],
        WeaponStyle.DualWield => [WeaponClass.Blade, WeaponClass.Blunt, WeaponClass.Axe],
        WeaponStyle.TwoHanded => [WeaponClass.Blade, WeaponClass.Blunt, WeaponClass.Axe],
        WeaponStyle.Polearm => [WeaponClass.Pierce, WeaponClass.Blade],
        WeaponStyle.Bow => [WeaponClass.Pierce],
        WeaponStyle.Crossbow => [WeaponClass.Pierce],
        WeaponStyle.Staff => [WeaponClass.Blunt],
        _ => []
    };
}
