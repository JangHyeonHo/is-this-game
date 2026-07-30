using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Weapons;

/// <summary>전투에서의 위치.</summary>
public enum Row
{
    Front,
    Back
}

/// <summary>몇 손으로 드는가.</summary>
public enum Hands
{
    One = 1,
    Two = 2
}

/// <summary>
/// 사거리. <b>무기가 정하는 셋 중 하나입니다.</b>
/// <para>
/// 활이 적 후열을 노리는 것은 스킬이 아니라 <b>활이라는 물건의 성질</b>입니다.
/// 반면 회복·도발·광역은 무기가 아니라 스킬이 정합니다.
/// </para>
/// 근거: docs/07-decisions.md §16.2
/// </summary>
public enum Reach
{
    /// <summary>붙어야 때립니다. 후열에서는 제 몫을 못 합니다.</summary>
    Melee,
    /// <summary>리치가 길어 <b>후열에 서서도 전열을 칩니다.</b> 창·언월도.</summary>
    Extended,
    /// <summary>멀리 닿습니다. <b>적 후열을 직접 노릴 수 있습니다.</b></summary>
    Ranged
}

/// <summary>
/// 무기 종류. <b>숙련도와 적성이 붙는 단위</b>입니다.
/// <para>
/// ⚠️ 예전에는 <c>WeaponStyle</c>(한손+방패 · 쌍수 · 양손 · …)이 열거형에 박혀 있었고,
/// 거기에 회복·도발·광역·마법 능력이 딸려 있었습니다. 그러면
/// <b>무기가 "파티에서 무슨 역할이냐"까지 정해서</b> 직업·스킬과 축이 겹칩니다.
/// </para>
/// <para>
/// 지금은 <b>손에 무엇을 들었는지가 곧 스타일</b>입니다 —
/// 오른손 검 + 왼손 방패면 그게 한손+방패고, 양쪽 다 검이면 쌍수입니다.
/// 새 무기를 추가하는 것은 <see cref="Weaponry.Catalogue"/>에 줄 하나를 넣는 일입니다.
/// </para>
/// 근거: docs/07-decisions.md §16.2b
/// </summary>
public enum WeaponKind
{
    /// <summary>빈 손.</summary>
    None,
    Sword,
    Axe,
    Mace,
    Greatsword,
    Spear,
    Shield,
    Bow,
    Crossbow,
    Staff,
    /// <summary>곡괭이. 채굴에도 쓰고 둔기 대용도 됩니다.</summary>
    Pickaxe,
    /// <summary>
    /// 가방. <b>위력이 없고 양손을 차지하며 보조무기 칸을 쓸 수 없습니다.</b>
    /// <para>
    /// 그래서 가방을 든 사람은 그 파견 동안 무방비이고, 파티가 지켜야 합니다.
    /// 짐 용량이 근력이 아니라 <b>칸을 내주는 대가</b>로 정해지는 이유입니다.
    /// </para>
    /// 근거: docs/07-decisions.md §16.8b
    /// </summary>
    Backpack
}

/// <summary>
/// 무기 하나의 명세. <b>위력 · 속도 · 사거리. 그게 전부입니다.</b>
/// <para>
/// 회복·도발·광역·마법·치명타 특성은 여기 없습니다 — 스킬과 숙련도가 담당합니다.
/// </para>
/// </summary>
/// <param name="Kind">종류.</param>
/// <param name="Korean">표시 이름.</param>
/// <param name="Hands">몇 손으로 드는가.</param>
/// <param name="Reach">사거리.</param>
/// <param name="Power">
/// 위력 배율. 방패와 가방은 0입니다 — 때리는 물건이 아닙니다.
/// <para>
/// ⚠️ 여기 있는 수치는 전부 임시값입니다. 능력치 999 스케일 전환 뒤에
/// 배치 시뮬레이션으로 다시 재야 합니다.
/// </para>
/// </param>
/// <param name="Speed">속도 배율. 무거운 물건은 느립니다.</param>
/// <param name="UsesMagicPower">
/// 물리 위력이 아니라 마법 위력을 쓰는가.
/// <para>
/// 이것은 <b>위력의 일부</b>입니다 — 지팡이는 후려치는 물건이 아니라 마력을 통하는
/// 물건이라는 뜻이고, "마법을 쓸 수 있다"는 능력이 아닙니다. 무슨 마법을 쓰는지는
/// 스킬이 정합니다.
/// </para>
/// </param>
/// <param name="Load">
/// 적재량. 가방만 값이 있습니다.
/// <para>⚠️ 짐 상한은 아직 미결입니다 — docs/07 §18.6</para>
/// </param>
public sealed record WeaponSpec(
    WeaponKind Kind,
    string Korean,
    Hands Hands,
    Reach Reach,
    double Power,
    double Speed,
    bool UsesMagicPower = false,
    int Load = 0)
{
    /// <summary>때릴 수 있는 물건인가.</summary>
    public bool IsWeapon => Power > 0.0;

    /// <summary>적 후열을 직접 노릴 수 있는가. <b>사거리에서 나옵니다.</b></summary>
    public bool CanStrikeBackRow => Reach == Reach.Ranged;

    /// <summary>아군 후열에 서서도 제 몫을 하는가.</summary>
    public bool CanActFromBackRow => Reach is Reach.Extended or Reach.Ranged;
}

/// <summary>
/// 무기 목록과 조회.
/// <para>
/// ⚠️ <b>수치는 전부 임시값입니다.</b> 999 스케일 전환 뒤 배치 시뮬레이션으로 잡습니다.
/// 감으로 고치지 말고 근거를 docs/08-balance-log.md에 남기세요.
/// </para>
/// </summary>
public static class Weaponry
{
    private static readonly WeaponSpec[] Table =
    [
        new(WeaponKind.None,       "빈손",   Hands.One, Reach.Melee,    0.00, 1.00),

        // 한손 근접 — 방패나 다른 무기와 같이 들 수 있습니다.
        new(WeaponKind.Sword,      "검",     Hands.One, Reach.Melee,    1.00, 1.00),
        new(WeaponKind.Axe,        "도끼",   Hands.One, Reach.Melee,    1.15, 0.90),
        new(WeaponKind.Mace,       "둔기",   Hands.One, Reach.Melee,    1.05, 0.95),
        new(WeaponKind.Pickaxe,    "곡괭이", Hands.One, Reach.Melee,    0.85, 0.85),

        // 양손 근접
        new(WeaponKind.Greatsword, "대검",   Hands.Two, Reach.Melee,    1.60, 0.80),

        // 긴 리치 — 후열에 서서도 전열을 칩니다.
        new(WeaponKind.Spear,      "창",     Hands.Two, Reach.Extended, 1.30, 1.00),

        // 때리는 물건이 아닙니다. 방어와 도발은 방패 숙련이 여는 스킬이 담당합니다.
        new(WeaponKind.Shield,     "방패",   Hands.One, Reach.Melee,    0.00, 0.90),

        // 원거리 — 적 후열을 직접 노립니다.
        new(WeaponKind.Bow,        "활",     Hands.Two, Reach.Ranged,   1.15, 1.05),
        new(WeaponKind.Crossbow,   "석궁",   Hands.Two, Reach.Ranged,   1.45, 0.75),
        new(WeaponKind.Staff,      "지팡이", Hands.Two, Reach.Ranged,   0.90, 0.90, UsesMagicPower: true),

        // 짐꾼. 위력이 없고 양손을 차지합니다.
        new(WeaponKind.Backpack,   "가방",   Hands.Two, Reach.Melee,    0.00, 0.80, Load: 12)
    ];

    private static readonly Dictionary<WeaponKind, WeaponSpec> ByKind =
        Table.ToDictionary(w => w.Kind);

    public static IReadOnlyList<WeaponSpec> Catalogue => Table;

    /// <summary>숙련도와 적성이 붙는 대상. 빈손은 제외합니다.</summary>
    public static IReadOnlyList<WeaponKind> Trainable { get; } =
        Table.Where(w => w.Kind != WeaponKind.None).Select(w => w.Kind).ToArray();

    public static WeaponSpec Of(WeaponKind kind) => ByKind[kind];

    public static string ToKorean(this WeaponKind kind) => ByKind[kind].Korean;

    /// <summary>
    /// 그 무기가 어느 능력치와 어울리는지. 적성 굴림의 상관관계를 만듭니다.
    /// <para>
    /// 이 가중치가 있어야 "마법 잠재력 최고인데 대검 적성"같은 모순 캐릭터가 잘 안 나옵니다.
    /// 다만 굴림에 노이즈가 있어서 가끔은 나오고, <b>그게 발견의 재미</b>가 됩니다.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<PrimaryStat, double> AffinityOf(WeaponKind kind) => kind switch
    {
        WeaponKind.Sword => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Finesse] = 1.6, [PrimaryStat.Strength] = 1.2, [PrimaryStat.Agility] = 1.0 },

        WeaponKind.Axe => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 2.0, [PrimaryStat.Vitality] = 1.0 },

        WeaponKind.Mace => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 1.7, [PrimaryStat.Vitality] = 1.2 },

        // 대검과 배틀액스는 순수한 완력입니다.
        WeaponKind.Greatsword => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 2.4, [PrimaryStat.Vitality] = 1.3 },

        // 긴 자루를 다루는 건 힘과 간격 감각.
        WeaponKind.Spear => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 1.7, [PrimaryStat.Finesse] = 1.3, [PrimaryStat.Agility] = 1.0 },

        // 방패를 들고 버티려면 몸과 힘이 필요합니다.
        WeaponKind.Shield => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Vitality] = 2.0, [PrimaryStat.Strength] = 1.5, [PrimaryStat.Spirit] = 0.6 },

        // 활은 조준과 자세, 그리고 시위를 당길 힘.
        WeaponKind.Bow => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Finesse] = 2.0, [PrimaryStat.Agility] = 1.5, [PrimaryStat.Strength] = 1.0 },

        // 석궁은 힘보다 정확도와 침착함.
        WeaponKind.Crossbow => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Finesse] = 2.0, [PrimaryStat.Strength] = 1.2, [PrimaryStat.Spirit] = 0.8 },

        // 마법은 지식과 정신력.
        WeaponKind.Staff => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Intellect] = 2.4, [PrimaryStat.Spirit] = 1.8 },

        WeaponKind.Pickaxe => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 1.6, [PrimaryStat.Vitality] = 1.4 },

        // 짐을 지는 것은 힘과 몸입니다.
        WeaponKind.Backpack => new Dictionary<PrimaryStat, double>
            { [PrimaryStat.Strength] = 2.0, [PrimaryStat.Vitality] = 1.6 },

        _ => new Dictionary<PrimaryStat, double>()
    };
}
