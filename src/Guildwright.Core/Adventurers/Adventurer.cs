using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Adventurers;

public enum AdventurerStatus
{
    /// <summary>현역.</summary>
    Active,
    /// <summary>은퇴. 멘토가 될 수 있습니다.</summary>
    Retired,
    /// <summary>불구. 강제 은퇴하지만 멘토는 될 수 있습니다.</summary>
    Crippled,
    /// <summary>사망. 돌아오지 않습니다.</summary>
    Dead
}

/// <summary>한 해에 무엇을 했는지.</summary>
public enum YearActivity
{
    Training,
    Deployment
}

public enum DeploymentOutcome
{
    Unharmed,
    /// <summary>부상. 능력치를 영구적으로 잃습니다.</summary>
    Injured,
    /// <summary>불구. 강제 은퇴합니다.</summary>
    Crippled,
    Died
}

/// <param name="Age">그 해의 나이.</param>
/// <param name="Activity">훈련인지 실전인지.</param>
/// <param name="StatChange">그 해의 능력치 변화 (노화로 음수일 수 있습니다).</param>
/// <param name="Outcome">실전이었을 때의 결과.</param>
/// <param name="Income">벌어들인 금액.</param>
/// <param name="Note">이력에 남길 서술.</param>
/// <param name="JobAtTime">그 해의 직업.</param>
/// <param name="ProficiencyGain">
/// 그 해에 오른 장착 무기 숙련도. null이면 활동 종류에 따른 기본값을 씁니다.
/// <para>
/// 훈련 연도는 <b>기술 훈련을 몇 달 했는지</b>에 따라 달라지므로 세션이 직접 계산해 넘깁니다.
/// 예전에는 훈련 연도이기만 하면 무엇을 시켰든 자동으로 올랐는데,
/// 그러면 숙련도가 선택 대상이 아니게 됩니다. (docs/08-design-revision.md §2)
/// </para>
/// </param>
public sealed record YearRecord(
    int Age,
    YearActivity Activity,
    PrimaryStats StatChange,
    DeploymentOutcome? Outcome,
    int Income,
    string Note,
    JobId? JobAtTime = null,
    double? ProficiencyGain = null);

/// <summary>
/// 모험가 한 명.
/// <para>
/// 나이를 먹고 죽는 엔티티라 불변으로 다루기 어렵습니다.
/// 대신 상태 변경을 이 클래스의 메서드로만 하도록 제한하고, 변경은 전부 이력에 남깁니다.
/// </para>
/// </summary>
public sealed class Adventurer
{
    /// <summary>길드에 등록 가능한 최소 나이.</summary>
    public const int RecruitAge = 15;

    private readonly List<YearRecord> _history = [];

    public Adventurer(
        string id,
        string name,
        PrimaryStats startingStats,
        int judgement,
        GrowthProfile growth,
        int age = RecruitAge,
        WeaponAptitudes? aptitudes = null,
        Loadout? loadout = null,
        JobId job = JobId.SwordApprentice,
        IReadOnlyList<SkillId>? innate = null)
    {
        Id = id;
        Name = name;
        Stats = startingStats;
        Judgement = Math.Clamp(judgement, 0, 100);
        Growth = growth;
        Age = age;
        Aptitudes = aptitudes ?? WeaponAptitudes.Uniform(AptitudeGrade.C);
        // 기본 장비는 직업에서 나옵니다. 검+방패로 고정하면 짐꾼이 가방 없이 태어나고,
        // 가방을 요구하는 액티브(짐 건네기)가 조용히 죽습니다.
        Loadout = loadout ?? StartingLoadoutFor(job);
        Job = job;
        Innate = innate ?? [];
    }

    public string Id { get; }
    public string Name { get; }
    public int Age { get; private set; }
    public PrimaryStats Stats { get; private set; }
    public int Judgement { get; private set; }
    public AdventurerStatus Status { get; private set; } = AdventurerStatus.Active;

    /// <summary>
    /// 숨겨진 성장 곡선.
    /// <para>
    /// ⚠️ <b>UI 레이어에서 이걸 직접 읽어 표시하면 안 됩니다.</b>
    /// 플레이어에게는 <see cref="Appraiser"/>가 만든 부정확한 추정만 보여줍니다.
    /// 이 정보의 비대칭이 이 게임의 핵심 재미이므로, 실수로 새면 게임이 망가집니다.
    /// </para>
    /// </summary>
    public GrowthProfile Growth { get; }

    /// <summary>
    /// 무기 적성. 숨겨진 정보입니다 — <see cref="Growth"/>와 마찬가지로
    /// UI에는 <see cref="Appraiser"/>가 만든 추정만 보여줘야 합니다.
    /// </summary>
    public WeaponAptitudes Aptitudes { get; }

    /// <summary>스타일별 숙련도. 숨기지 않습니다 — 본인이 뭘 얼마나 했는지는 알 수 있어야 합니다.</summary>
    public WeaponProficiency Proficiency { get; } = new();

    /// <summary>
    /// 파생 보정. <b>원천 능력치와 무관하게 겪은 것으로 직접 붙는 값</b>입니다.
    /// <para>
    /// 계속 맞다 보면 몸이 단단해지고(물리 방어), 급소를 노리다 보면 손에 익습니다(치명타율).
    /// 원천 능력치가 같아도 이력이 다르면 다른 캐릭터가 되는 이유입니다.
    /// </para>
    /// </summary>
    public DerivedBonuses Bonuses { get; } = new();

    /// <summary>실제 전투에 쓰일 수치를 조회합니다. 원천 + 보정이 합쳐진 값입니다.</summary>
    public int MaxHp => DerivedStats.MaxHp(Stats, Bonuses);
    public int PhysicalPower => DerivedStats.PhysicalPower(Stats, Bonuses);
    public int PhysicalGuard => DerivedStats.PhysicalGuard(Stats, Bonuses);
    public int MagicPower => DerivedStats.MagicPower(Stats, Bonuses);
    public int MagicGuard => DerivedStats.MagicGuard(Stats, Bonuses);
    public double CritChance => DerivedStats.CritChance(Stats, Bonuses);
    public double EvasionChance => DerivedStats.EvasionChance(Stats, Bonuses);

    internal void ApplyDerivedBonus(DerivedStat stat, double amount) => Bonuses.Add(stat, amount);

    /// <summary>
    /// 비전투 역량. 함정 감지·척후·운반·채집·감정.
    /// <para>
    /// 이 목록은 이해도가 오르면서 하나씩 드러납니다 (docs/08 §16.7).
    /// </para>
    /// </summary>
    public IReadOnlyList<SkillId> Innate { get; }

    /// <summary>장착 4칸 — 주무기(좌·우) + 보조무기(좌·우).</summary>
    public Loadout Loadout { get; }

    /// <summary>
    /// 현재 직업.
    /// <para>
    /// <b>희망 직업으로 시작</b>하고, 이해도가 올라 적성이 드러나면 전직합니다.
    /// 전직은 자유이고 비용도 없지만, 새 무기 숙련이 0부터라 늦게 알아챌수록 손해입니다.
    /// </para>
    /// </summary>
    public JobId Job { get; private set; }

    public Job JobProfile => Jobs.Of(Job);

    /// <summary>현재 칭호. 직업 이름이 그대로 칭호입니다 — 계급이라는 별도 축이 없습니다.</summary>
    public string Title => JobProfile.Korean;

    /// <summary>
    /// 전직합니다. <b>조건도 비용도 없습니다.</b>
    /// <para>
    /// 대가는 규칙이 아니라 자연히 생깁니다 — 새 무기 숙련은 0부터입니다.
    /// "신궁이었던 애가 검사로 전직한다고 검성이 되진 않습니다."
    /// </para>
    /// <para>
    /// 다만 <b>고집</b>을 타고났으면 권유를 듣지 않습니다 (docs/08 §16.8).
    /// </para>
    /// </summary>
    /// <returns>실제로 바뀌었는지.</returns>
    public bool ChangeJob(JobId job)
    {
        if (Innate.Contains(SkillId.Stubborn) && job != Job) return false;
        if (!Jobs.Of(job).IsUnlockedBy(k => Proficiency[k])) return false;

        Job = job;
        return true;
    }

    /// <summary>무기를 끼웁니다.</summary>
    public void Equip(WeaponSet set, Hand hand, WeaponKind kind) => Loadout.Equip(set, hand, kind);

    /// <summary>현재 장비의 전투 효율. <b>주된 무기의 숙련도</b>에서 나옵니다.</summary>
    public double WeaponEffectiveness => Proficiency.EffectivenessOf(Loadout.MainWeapon);

    /// <summary>지금 숙련도로 가질 수 있는 직업들. <b>히든 직업이 여기서 드러납니다.</b></summary>
    public IReadOnlyList<Job> AvailableJobs => Jobs.UnlockedBy(k => Proficiency[k]);

    /// <summary>가진 패시브 — 태생 + 현재 직업이 주는 것.</summary>
    public IReadOnlyList<SkillId> Passives =>
        [.. Innate.Where(id => SkillBook.Of(id).Form == SkillForm.Passive),
         .. JobProfile.Grants.Where(id => SkillBook.Of(id).Form == SkillForm.Passive)];

    /// <summary>
    /// 장착할 액티브. 직업이 주는 것 중 슬롯 수만큼, 그리고 <b>지금 든 무기로 쓸 수 있는 것</b>만.
    /// </summary>
    public IReadOnlyList<SkillId> Actives =>
        JobProfile.Grants
            .Where(id => SkillBook.Of(id).Form == SkillForm.Active)
            .Where(id => SkillBook.Of(id).UsableWith(Loadout))
            .Take(JobProfile.ActiveSlots)
            .ToArray();

    /// <summary>연간 유지비. 직업 데이터에서 나옵니다 — 예전 JobRank.AnnualWage를 흡수했습니다.</summary>
    public int AnnualWage => JobProfile.Upkeep;

    /// <summary>수주할 수 있는 의뢰 난이도 상한. 실력이 아니라 자격입니다.</summary>
    public int MaxContractDifficulty => JobProfile.MaxContractDifficulty;

    /// <summary>길드 평판에 기여하는 정도. 수주 자격에 비례합니다.</summary>
    public int ReputationValue => JobProfile.MaxContractDifficulty * 2;

    public IReadOnlyList<YearRecord> History => _history;

    /// <summary>지금까지 보낸 총 연차.</summary>
    public int CompletedYears => _history.Count;

    /// <summary>훈련으로 보낸 연차. 감정 정확도에 영향을 줍니다.</summary>
    public int TrainingYears => _history.Count(r => r.Activity == YearActivity.Training);

    public int DeploymentYears => _history.Count(r => r.Activity == YearActivity.Deployment);

    public bool IsAlive => Status is AdventurerStatus.Active or AdventurerStatus.Retired or AdventurerStatus.Crippled;

    /// <summary>
    /// 실전 투입이 가능한가.
    /// <para>등록 첫 해는 무조건 훈련입니다 — 15세를 바로 전장에 보낼 수는 없습니다.</para>
    /// </summary>
    public bool CanDeploy => Status == AdventurerStatus.Active && CompletedYears >= 1;

    /// <summary>멘토가 될 수 있는가. 살아서 은퇴했어야 합니다.</summary>
    public bool CanMentor => Status is AdventurerStatus.Retired or AdventurerStatus.Crippled;

    internal void ApplyYear(YearRecord record)
    {
        _history.Add(record);
        Stats = (Stats + record.StatChange).ClampToZero();
        Age++;

        // 그 해를 들고 있던 무기의 숙련도가 오릅니다. 사망한 해는 제외합니다.
        if (record.Outcome != DeploymentOutcome.Died)
        {
            // 훈련 연도는 세션이 계산해 넘겨줍니다 — 기술 훈련을 몇 달 했느냐로 달라지므로.
            // 실전은 무기를 쓸 수밖에 없으니 연 단위 기본값을 씁니다.
            double baseGain = record.ProficiencyGain
                ?? (record.Activity == YearActivity.Deployment
                    ? WeaponProficiency.PerDeploymentYear
                    : 0.0);

            // 든 것들의 숙련도가 각각 오릅니다 — 검+방패면 둘 다 늡니다.
            // 순서를 고정하기 위해 Loadout.Held의 순서를 그대로 씁니다.
            if (baseGain > 0.0)
            {
                foreach (var kind in Loadout.Held)
                {
                    Proficiency.Advance(kind, Aptitudes[kind], baseGain);
                }
            }
        }

        switch (record.Outcome)
        {
            case DeploymentOutcome.Died:
                Status = AdventurerStatus.Dead;
                break;
            case DeploymentOutcome.Crippled:
                Status = AdventurerStatus.Crippled;
                break;
        }
    }

    internal void GainJudgement(int amount) => Judgement = Math.Clamp(Judgement + amount, 0, 100);

    public void Retire()
    {
        if (Status == AdventurerStatus.Active) Status = AdventurerStatus.Retired;
    }

    /// <summary>신입 모험가를 무작위 생성합니다.</summary>
    public static Adventurer Recruit(string id, string name, IRandomSource rng, int potentialTier = 3)
    {
        var growth = GrowthProfile.Roll(rng.Fork($"growth:{id}"), potentialTier);

        // 시작 능력치는 잠재력의 일부만. 나머지는 육성으로 채웁니다.
        var starting = PrimaryStats.Zero;
        foreach (var kind in PrimaryStats.AllStats)
        {
            double ratio = 0.15 + rng.NextDouble() * 0.10;
            starting = starting.With(kind, Math.Max(1, (int)Math.Round(growth.Potential[kind] * ratio)));
        }

        int judgement = 10 + rng.NextInt(0, 25);
        var aptitudes = WeaponAptitudes.Roll(growth.Potential, rng.Fork($"aptitude:{id}"));

        // 희망 직업을 가지고 옵니다 — 플레이어가 배정하는 게 아닙니다.
        // 적성과 어긋날 수 있고, 그 어긋남이 전직의 동기가 됩니다 (docs/08 §16.3).
        var wished = Jobs.Starting[rng.Fork($"wish:{id}").NextInt(0, Jobs.Starting.Count)];
        var loadout = StartingLoadoutFor(wished);

        // 태생 패시브 하나를 타고납니다. 이해도가 올라야 드러납니다 (docs/08 §16.7).
        var pool = SkillBook.InnatePool;
        var innate = new[] { pool[rng.Fork($"innate:{id}").NextInt(0, pool.Count)] };

        return new Adventurer(
            id, name, starting, judgement, growth, RecruitAge, aptitudes, loadout, wished, innate);
    }

    /// <summary>희망 직업이 쓰는 무기를 손에 들려줍니다.</summary>
    private static Loadout StartingLoadoutFor(JobId job)
    {
        var requires = Jobs.Of(job).Requires;

        // 요구 숙련이 없는 시작 직업이므로, 그 계열의 무기를 찾아 들려줍니다.
        // 여러 무기를 요구하는 히든 직업은 요구가 높은 쪽을 주무기로 봅니다 —
        // Dictionary 순회 순서에 기대면 같은 시드가 다른 결과를 낼 수 있습니다.
        var kind = requires.Count > 0
            ? requires.OrderByDescending(r => r.Value).ThenBy(r => r.Key).First().Key
            : StartingWeaponFor(job);

        var spec = Weaponry.Of(kind);
        if (spec.Hands == Hands.Two) return Loadout.Single(kind);

        // 방패처럼 때릴 수 없는 물건은 주손에 들 것이 아닙니다 — 반대 손에 검을 들려줍니다.
        // (방패를 양손에 든 견습 방패병이 나오면 아무것도 때릴 수 없습니다.)
        return spec.IsWeapon
            ? Loadout.Pair(kind, WeaponKind.Shield)
            : Loadout.Pair(WeaponKind.Sword, kind);
    }

    private static WeaponKind StartingWeaponFor(JobId job) => job switch
    {
        JobId.SwordApprentice => WeaponKind.Sword,
        JobId.ShieldApprentice => WeaponKind.Shield,
        JobId.GreatApprentice => WeaponKind.Greatsword,
        JobId.SpearApprentice => WeaponKind.Spear,
        JobId.BowApprentice => WeaponKind.Bow,
        JobId.BoltApprentice => WeaponKind.Crossbow,
        JobId.StaffApprentice => WeaponKind.Staff,
        JobId.Axeman => WeaponKind.Axe,
        JobId.Maceman => WeaponKind.Mace,
        JobId.Miner => WeaponKind.Pickaxe,
        JobId.Porter => WeaponKind.Backpack,
        _ => WeaponKind.Sword
    };

    public override string ToString() =>
        $"{Name} · {Title} ({Age}세, {Status}) {Stats} 판단력 {Judgement} " +
        $"[{Loadout} 숙련 {Proficiency[Loadout.MainWeapon]}, 유지비 {AnnualWage}]";
}
