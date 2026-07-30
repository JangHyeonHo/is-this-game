using Guildwright.Core.Weapons;

namespace Guildwright.Core.Skills;

/// <summary>
/// 직업 이름.
/// <para>
/// <b>계급이 따로 없습니다.</b> 견습 궁수부터 신궁까지가 전부 <b>요구 숙련만 다른
/// 직업 행</b>입니다. 예전에는 <c>JobRank</c> 열거형(견습~대가 5단계)이 따로 있었고
/// 거기에 연봉과 수주 난이도까지 걸려 있었는데, 축이 하나 줄었습니다.
/// </para>
/// 근거: docs/07-decisions.md §16.2c
/// </summary>
public enum JobId
{
    // 검
    SwordApprentice, Swordsman, TwinBlade, Blademaster, SwordSaint,
    // 방패
    ShieldApprentice, Shieldbearer, Guardsman, Knight, Warden,
    // 대검
    GreatApprentice, Warrior, Veteran, Champion, Warlord,
    // 창
    SpearApprentice, Spearman, Lancer, SpearAdept, SpearSaint,
    // 활
    BowApprentice, Archer, Marksman, Sharpshooter, Divineshot,
    // 석궁
    BoltApprentice, Crossbowman, Sniper, Deadeye, Piercer,
    // 지팡이
    StaffApprentice, Mage, HighMage, Archmage, Sage,
    // 도끼·둔기·곡괭이 — 짧은 사다리
    Axeman, Berserker,
    Maceman, Warpriest,
    Miner, Prospector,
    // 짐꾼 (비전투)
    Porter, SkilledPorter, Quartermaster,
    // 히든 — 숙련 조합으로만 열립니다
    SpellArcher, SpellBlade
}

/// <summary>
/// 직업 하나. <b>표의 한 줄입니다.</b>
/// <para>
/// 직업이 수십 종이 될 예정이므로, 추가하는 것이 코드가 아니라
/// <see cref="Jobs.Catalogue"/>에 줄을 넣는 일이어야 합니다.
/// </para>
/// </summary>
/// <param name="Id">이름.</param>
/// <param name="Korean">표시 이름.</param>
/// <param name="Requires">
/// 요구 숙련 조합. <b>비어 있으면 처음부터 고를 수 있습니다.</b>
/// <para>
/// 여러 무기를 요구하면 히든 직업이 됩니다 — 활 60 + 지팡이 60 = 마궁사.
/// </para>
/// </param>
/// <param name="Grants">주는 스킬.</param>
/// <param name="ActiveSlots">
/// 액티브 스킬을 몇 개까지 장착할 수 있는가.
/// <para>
/// <b>"직업 숙련"이라는 별도 축은 없습니다.</b> 슬롯 수는 이 표의 한 열입니다.
/// 그리고 이 제한이 전술 규칙 부풀기를 막아줍니다 — 직업이 수십 종이어도
/// 한 캐릭터가 다루는 행동은 슬롯 수만큼입니다.
/// </para>
/// </param>
/// <param name="ProficiencyBonus">숙련도 상승 보정.</param>
/// <param name="MaxContractDifficulty">
/// 혼자·임시 조합으로 받을 수 있는 의뢰 난이도 상한. <c>JobRank</c>에서 흡수했습니다.
/// <para>실력이 아니라 <b>자격</b>입니다.</para>
/// </param>
/// <param name="Combat">
/// 전투 직업인가. 짐꾼은 거짓입니다.
/// <para>파티 구성 규칙(짐꾼 최대 1명 · 짐꾼만으로 구성 불가)에 씁니다.</para>
/// </param>
public sealed record Job(
    JobId Id,
    string Korean,
    IReadOnlyDictionary<WeaponKind, int> Requires,
    IReadOnlyList<SkillId> Grants,
    int ActiveSlots,
    double ProficiencyBonus = 0.0,
    int MaxContractDifficulty = 2,
    bool Combat = true)
{
    /// <summary>이 숙련도로 이 직업을 가질 수 있는가.</summary>
    public bool IsUnlockedBy(Func<WeaponKind, int> proficiencyOf) =>
        Requires.All(r => proficiencyOf(r.Key) >= r.Value);

    /// <summary>여러 무기를 요구하는가 — 히든 직업.</summary>
    public bool IsHidden => Requires.Count > 1;

    public override string ToString() => Korean;
}

/// <summary>
/// 직업 목록과 해금 판정.
/// <para>
/// ⚠️ <b>수치는 전부 임시값입니다.</b> 999 스케일 전환 뒤 배치 시뮬레이션으로 잡습니다.
/// 특히 <b>조합 직업이 단일 특화보다 약해지지 않게 하는 균형</b>은 아직 미결입니다 —
/// 합집합 원칙은 정해졌지만 수치가 안 정해졌습니다 (docs/07 §16.9).
/// </para>
/// </summary>
public static class Jobs
{
    /// <summary>사다리 한 단을 만듭니다 — 요구 숙련만 다른 같은 계열의 직업들.</summary>
    private static Job Rung(
        JobId id, string korean, WeaponKind weapon, int required,
        int slots, int difficulty, params SkillId[] grants) =>
        new(id, korean,
            required <= 0
                ? new Dictionary<WeaponKind, int>()
                : new Dictionary<WeaponKind, int> { [weapon] = required },
            grants, slots, ProficiencyBonus: 0.05 * slots,
            MaxContractDifficulty: difficulty);

    // 사다리 다섯 단이 공유하는 요구 숙련·슬롯·자격.
    // 예전 JobRank의 임계값(20/45/70/90)과 난이도(2/4/6/8/10)를 그대로 옮겼습니다.
    // 유지비는 직업과 무관합니다 — 등급 무관 정액(docs/07 §7, CareerRules.AnnualUpkeep).
    private static readonly int[] Steps = [0, 20, 45, 70, 90];
    private static readonly int[] Slots = [2, 3, 4, 5, 6];
    private static readonly int[] Difficulty = [2, 4, 6, 8, 10];

    /// <summary>
    /// 무기 계열이 주는 스킬. <b>히든 직업의 합집합을 여기서 계산합니다</b> —
    /// 손으로 골라 적으면 빠지는 것이 생깁니다(마궁사에서 약화가 빠져 있었습니다).
    /// <para>사다리 정의보다 먼저 선언해야 합니다 — 초기화 중에 채우면 static 가변 상태가 됩니다.</para>
    /// </summary>
    private static readonly Dictionary<WeaponKind, SkillId[]> LadderGrants = new()
    {
        [WeaponKind.Sword] = [SkillId.TwinStrike],
        [WeaponKind.Shield] = [SkillId.Shielding, SkillId.Provoke],
        [WeaponKind.Greatsword] = [SkillId.HeavyBlow, SkillId.Sweep],
        [WeaponKind.Spear] = [SkillId.HeavyBlow],
        [WeaponKind.Bow] = [SkillId.SteadyAim, SkillId.PiercingShot],
        [WeaponKind.Crossbow] = [SkillId.SteadyAim, SkillId.PiercingShot],
        [WeaponKind.Staff] = [SkillId.Cure, SkillId.Empower, SkillId.Enfeeble],
        [WeaponKind.Backpack] = [SkillId.HandPotion, SkillId.Packcraft]
    };

    private static IEnumerable<Job> Ladder(WeaponKind weapon, JobId[] ids, string[] names)
    {
        var grants = LadderGrants.GetValueOrDefault(weapon, []);

        for (int i = 0; i < ids.Length; i++)
        {
            // 숙련 패시브는 두 번째 단부터 붙습니다 — 견습이 숙달을 가질 수는 없습니다.
            var given = i >= 1 ? grants : [];
            yield return Rung(ids[i], names[i], weapon, Steps[i],
                Slots[i], Difficulty[i], given);
        }
    }

    /// <summary>
    /// 히든 직업 하나. <b>요구한 무기 계열들이 주는 스킬의 합집합</b>을 그대로 받습니다.
    /// <para>
    /// §16.5 [확정]: "평균이 아니라 <b>합집합</b>입니다 — 양쪽 직업이 각각 가진 장점을
    /// 흡수합니다." 손으로 골라 적었더니 마궁사에서 <b>약화</b>가, 마검사에서 <b>회복</b>이
    /// 빠져 있었습니다. 큐레이션된 부분집합은 합집합이 아닙니다.
    /// </para>
    /// </summary>
    private static Job Hidden(
        JobId id, string korean, int slots, int difficulty,
        double proficiencyBonus, params (WeaponKind Weapon, int Required)[] requires)
    {
        // 열거형 순서로 고정합니다 — Dictionary 순회 순서가 스킬 목록 순서를 바꾸면
        // 액티브 슬롯에 들어가는 스킬이 실행마다 달라집니다.
        var granted = requires
            .OrderBy(r => r.Weapon)
            .SelectMany(r => LadderGrants.GetValueOrDefault(r.Weapon, []))
            .Distinct()
            .ToArray();

        return new Job(id, korean,
            requires.ToDictionary(r => r.Weapon, r => r.Required),
            granted, slots, proficiencyBonus, difficulty);
    }

    private static readonly Job[] Table =
    [
        .. Ladder(WeaponKind.Sword,
            [JobId.SwordApprentice, JobId.Swordsman, JobId.TwinBlade, JobId.Blademaster, JobId.SwordSaint],
            ["견습 검객", "검객", "쌍검사", "명검객", "검성"]),

        .. Ladder(WeaponKind.Shield,
            [JobId.ShieldApprentice, JobId.Shieldbearer, JobId.Guardsman, JobId.Knight, JobId.Warden],
            ["견습 방패병", "방패병", "근위병", "기사", "수호기사"]),

        .. Ladder(WeaponKind.Greatsword,
            [JobId.GreatApprentice, JobId.Warrior, JobId.Veteran, JobId.Champion, JobId.Warlord],
            ["견습 전사", "전사", "역전의 전사", "맹장", "대전사"]),

        .. Ladder(WeaponKind.Spear,
            [JobId.SpearApprentice, JobId.Spearman, JobId.Lancer, JobId.SpearAdept, JobId.SpearSaint],
            ["견습 창병", "창병", "창기병", "창술사", "창성"]),

        .. Ladder(WeaponKind.Bow,
            [JobId.BowApprentice, JobId.Archer, JobId.Marksman, JobId.Sharpshooter, JobId.Divineshot],
            ["견습 궁수", "궁수", "사수", "명궁", "신궁"]),

        .. Ladder(WeaponKind.Crossbow,
            [JobId.BoltApprentice, JobId.Crossbowman, JobId.Sniper, JobId.Deadeye, JobId.Piercer],
            ["견습 석궁병", "석궁병", "저격수", "명사수", "관통자"]),

        .. Ladder(WeaponKind.Staff,
            [JobId.StaffApprentice, JobId.Mage, JobId.HighMage, JobId.Archmage, JobId.Sage],
            ["견습 마법사", "마법사", "상급 마법사", "대마법사", "현자"]),

        // 짧은 사다리 — 필요해지면 표에 줄을 더하면 됩니다.
        Rung(JobId.Axeman, "도부수", WeaponKind.Axe, 0, 2, 2),
        Rung(JobId.Berserker, "광부수", WeaponKind.Axe, 45, 4, 6, SkillId.HeavyBlow),
        Rung(JobId.Maceman, "둔기병", WeaponKind.Mace, 0, 2, 2),
        Rung(JobId.Warpriest, "전투 사제", WeaponKind.Mace, 45, 4, 6, SkillId.Cure),
        Rung(JobId.Miner, "광부", WeaponKind.Pickaxe, 0, 2, 1),
        Rung(JobId.Prospector, "탐광자", WeaponKind.Pickaxe, 45, 3, 3),

        // ---- 짐꾼 — 비전투 직업 ----
        // 누구나 시작할 수 있고(요구 숙련 없음), 오래 하면 세집니다.
        // 액티브 슬롯이 적은 대신 파티에 없으면 안 되는 자리입니다.
        new(JobId.Porter, "짐꾼",
            new Dictionary<WeaponKind, int>(),
            [SkillId.HandPotion],
            ActiveSlots: 2, MaxContractDifficulty: 2, Combat: false),

        new(JobId.SkilledPorter, "숙련 짐꾼",
            new Dictionary<WeaponKind, int> { [WeaponKind.Backpack] = 40 },
            [SkillId.HandPotion, SkillId.Packcraft],
            ActiveSlots: 3, MaxContractDifficulty: 5, Combat: false),

        // ⚠️ 예전에는 수송대장이 SkillId.Cheerful(태생 스킬)을 줬습니다. 직업이 태생을 주면
        //    "타고난다"와 "배운다"가 같아져 두 축이 붕괴합니다 (§10). 슬롯·자격·유지비가
        //    오르는 것으로 상위 단의 값은 이미 있습니다 — 짐꾼 계열 세 번째 스킬은 미정입니다.
        new(JobId.Quartermaster, "수송대장",
            new Dictionary<WeaponKind, int> { [WeaponKind.Backpack] = 75 },
            [SkillId.HandPotion, SkillId.Packcraft],
            ActiveSlots: 4, MaxContractDifficulty: 8, Combat: false),

        // ---- 히든 — 숙련 조합으로만 열립니다 ----
        // 대가는 규칙이 아니라 시간입니다 — 두 숙련을 올리는 데 배로 걸리고,
        // 그동안 그 캐릭터는 오래 어중간하며, 경력이 유한하므로 못 닿을 수도 있습니다.
        Hidden(JobId.SpellArcher, "마궁사", slots: 5, difficulty: 8,
            proficiencyBonus: 0.20,
            (WeaponKind.Bow, 60), (WeaponKind.Staff, 60)),

        Hidden(JobId.SpellBlade, "마검사", slots: 5, difficulty: 8,
            proficiencyBonus: 0.20,
            (WeaponKind.Sword, 60), (WeaponKind.Staff, 60))
    ];

    private static readonly Dictionary<JobId, Job> ById = Table.ToDictionary(j => j.Id);

    public static IReadOnlyList<Job> Catalogue => Table;

    public static Job Of(JobId id) => ById[id];

    /// <summary>요구 숙련이 없어 처음부터 고를 수 있는 직업. <b>희망 직업의 후보</b>입니다.</summary>
    public static IReadOnlyList<JobId> Starting { get; } =
        Table.Where(j => j.Requires.Count == 0).Select(j => j.Id).ToArray();

    /// <summary>
    /// 이 숙련도로 가질 수 있는 직업들.
    /// <para>
    /// 정렬 순서를 고정합니다 — <see cref="Table"/> 순서를 그대로 따르므로
    /// 같은 숙련도면 항상 같은 목록이 나옵니다.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Job> UnlockedBy(Func<WeaponKind, int> proficiencyOf) =>
        Table.Where(j => j.IsUnlockedBy(proficiencyOf)).ToArray();
}
