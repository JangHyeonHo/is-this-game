using Guildwright.Core.Careers;
using Guildwright.Core.Parties;
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
/// 그러면 숙련도가 선택 대상이 아니게 됩니다. (docs/07-decisions.md §2)
/// </para>
/// </param>
/// <param name="Months">
/// 이 기록이 덮는 달 수. 훈련은 12달이지만 <b>파견은 의뢰 기간만큼</b>입니다.
/// <para>
/// 예전에는 이력 한 줄이 곧 1년이었습니다. 의뢰가 1달~1년이 되면서 그 전제가
/// 깨졌으므로, 나이는 이 값이 12달을 채울 때마다 오릅니다 (docs/07 §17.4).
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
    double? ProficiencyGain = null,
    int Months = 12);

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
    /// 타고난 스킬 (성격). <b>플레이어가 정할 수 없습니다.</b>
    /// <para>
    /// ⚠️ 여기 있던 주석은 폐기된 <c>SupportSkill</c> 5종(함정 감지·척후·운반·채집·감정)
    /// 설명이 그대로 남아 있었습니다. 그 개념은 §16.8c로 폐기됐고, 타입은 지웠지만
    /// 서술이 살아 있어 되살아날 위험이 있었습니다.
    /// </para>
    /// <para>
    /// ⚠️ <b>이해도로 하나씩 드러나는 은닉은 아직 구현되지 않았습니다</b> (§16.7). 지금은
    /// 이 프로퍼티가 그냥 공개되어 있습니다 — 예전 주석은 은닉이 있는 것처럼 서술했습니다.
    /// </para>
    /// </summary>
    public IReadOnlyList<SkillId> Innate { get; }

    /// <summary>장착 4칸 — 주무기(좌·우) + 보조무기(좌·우).</summary>
    public Loadout Loadout { get; private set; }

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
    /// 개인 등급 F ~ SS. <b>모두 F로 시작합니다.</b>
    /// <para>
    /// 직업·숙련과는 다른 축입니다 — 직업은 <b>무엇을 할 수 있는가</b>, 등급은
    /// <b>무엇을 맡길 수 있는가</b>입니다. 그래서 검성인 F등급이 이론상 가능합니다.
    /// </para>
    /// <para>
    /// 저절로 오르지 않습니다. <b>승급 의뢰</b>를 완수해야 오릅니다 (docs/07 §6.5) —
    /// 그래서 <see cref="Promote"/>는 여기 있고 판정은 의뢰 쪽에 있습니다.
    /// </para>
    /// </summary>
    public Rank Rank { get; private set; } = Ranks.Lowest;

    /// <summary>
    /// 한 단 승급합니다. <b>승급 의뢰를 완수했을 때만</b> 불립니다.
    /// </summary>
    /// <returns>실제로 올랐는지. 최고 등급이면 <c>false</c>.</returns>
    public bool Promote()
    {
        if (Rank == Ranks.Highest) return false;

        Rank = Rank.Above(1);
        return true;
    }

    /// <summary>
    /// 전직합니다. <b>조건도 비용도 없습니다.</b>
    /// <para>
    /// 대가는 규칙이 아니라 자연히 생깁니다 — 새 무기 숙련은 0부터입니다.
    /// "신궁이었던 애가 검사로 전직한다고 검성이 되진 않습니다."
    /// </para>
    /// <para>
    /// 다만 <b>고집</b>을 타고났으면 권유를 듣지 않습니다 (docs/07 §16.8).
    /// </para>
    /// <para>
    /// ⚠️ <b>[검토중] — 고집의 세기는 정해지지 않았습니다.</b> 문서는 "고집 같은 특성이
    /// 있으면 얘기가 <b>달라질 수 있습니다</b>"이고, §16.9는 "희망 직업 무시 시 반응 —
    /// 특성에 따라 다름"을 검토중으로 둡니다. 지금은 <b>영구 전면 봉쇄</b>인데, §16.2c로
    /// 계급이 직업 행이 된 뒤 사다리는 전직으로만 오르므로 <b>고집을 타고나면 평생
    /// 견습에 고정</b>됩니다. 그 결과는 문서에 없습니다.
    /// </para>
    /// <para>
    /// 그래서 <b>같은 계열 안에서 올라가는 것은 막지 않습니다</b> — 고집은 "다른 길로
    /// 가라는 권유를 듣지 않는" 것이고, 자기 길에서 숙달하는 것을 거부하는 것이 아닙니다.
    /// 이 완화도 확정이 아니므로 수치가 정해지면 여기만 고칩니다.
    /// </para>
    /// </summary>
    /// <returns>실제로 바뀌었는지.</returns>
    public bool ChangeJob(JobId job)
    {
        if (Innate.Contains(SkillId.Stubborn) && !SameLine(Job, job) && job != Job) return false;
        if (!Jobs.Of(job).IsUnlockedBy(k => Proficiency[k])) return false;

        Job = job;
        // 시작 장비도 직업이 정하므로 (§16.2) 전직하면 그 직업의 무기로 바꿔 듭니다.
        // 이게 없으면 짐꾼 출신 검객이 가방을 무기로 휘두릅니다 — 실제로 3년을 그랬습니다.
        Reequip();
        return true;
    }

    /// <summary>현재 직업의 시작 장비로 다시 갖춥니다 (나무검 → 진짜 검 등).</summary>
    public void Reequip() => Loadout = StartingLoadoutFor(Job);

    /// <summary>
    /// 같은 무기 계열인가. <b>고집이 막지 않는 범위</b>를 정합니다.
    /// <para>요구 무기가 없는 견습단은 <see cref="StartingWeaponFor"/>로 계열을 봅니다.</para>
    /// </summary>
    private static bool SameLine(JobId from, JobId to) => LineOf(from) == LineOf(to);

    private static WeaponKind LineOf(JobId job)
    {
        var requires = Jobs.Of(job).Requires;
        return requires.Count == 1 ? requires.Keys.First() : StartingWeaponFor(job);
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
    /// 장착할 액티브. 직업이 주는 것 중 슬롯 수만큼 — <b>지금 든 무기로 쓸 수 있고,
    /// 요구 숙련을 채워 이미 배운 것</b>만 (docs/07 §22 — 배우는 타이밍은 숙련이 정한다).
    /// </summary>
    public IReadOnlyList<SkillId> Actives =>
        JobProfile.Grants
            .Where(id => SkillBook.Of(id).Form == SkillForm.Active)
            .Where(id => SkillBook.Of(id).UsableWith(Loadout))
            .Where(id => SkillBook.Of(id).LearnedBy(Proficiency))
            .Take(JobProfile.ActiveSlots)
            .ToArray();


    /// <summary>수주할 수 있는 의뢰 난이도 상한. 실력이 아니라 자격입니다.</summary>
    public int MaxContractDifficulty => JobProfile.MaxContractDifficulty;


    public IReadOnlyList<YearRecord> History => _history;

    /// <summary>
    /// 지금까지 보낸 총 달 수.
    /// <para>
    /// <b>이력 한 줄이 곧 1년이 아닙니다.</b> 훈련은 12달이지만 파견은 의뢰 기간만큼이라,
    /// 1달 의뢰를 열두 번 한 사람과 1년 의뢰를 한 번 한 사람이 같은 나이가 되어야 합니다.
    /// </para>
    /// </summary>
    public int MonthsElapsed { get; private set; }

    /// <summary>지금까지 보낸 총 연차. 달 수에서 나옵니다.</summary>
    public int CompletedYears => MonthsElapsed / MonthsPerYear;

    /// <summary>훈련으로 보낸 달. 감정 정확도에 영향을 줍니다.</summary>
    public int TrainingMonths =>
        _history.Where(r => r.Activity == YearActivity.Training).Sum(r => r.Months);

    /// <summary>실전으로 보낸 달.</summary>
    public int DeploymentMonths =>
        _history.Where(r => r.Activity == YearActivity.Deployment).Sum(r => r.Months);

    public int TrainingYears => TrainingMonths / MonthsPerYear;

    public int DeploymentYears => DeploymentMonths / MonthsPerYear;

    /// <summary>1년은 12달입니다. 나이가 오르는 주기입니다.</summary>
    public const int MonthsPerYear = 12;

    public bool IsAlive => Status is AdventurerStatus.Active or AdventurerStatus.Retired or AdventurerStatus.Crippled;

    /// <summary>
    /// 실전 투입이 가능한가.
    /// <para>등록 첫 해는 무조건 훈련입니다 — 15세를 바로 전장에 보낼 수는 없습니다.</para>
    /// </summary>
    public bool CanDeploy => Status == AdventurerStatus.Active && CompletedYears >= 1;

    /// <summary>멘토가 될 수 있는가. 살아서 은퇴했어야 합니다.</summary>
    public bool CanMentor => Status is AdventurerStatus.Retired or AdventurerStatus.Crippled;

    /// <summary>
    /// 훈련 한 달을 그 자리에서 반영합니다. 성장과 달수는 달 단위 게임에서 달 단위로
    /// 움직여야 합니다 — 결산까지 모아 두면 화면의 능력치가 1년 내내 얼어 있습니다.
    /// </summary>
    internal void ApplyTrainingMonth(PrimaryStats statGain, double proficiencyGain)
    {
        Stats = (Stats + statGain).ClampToZero();
        AdvanceMonths(1);

        if (proficiencyGain > 0.0)
        {
            foreach (var kind in Loadout.Held)
            {
                Proficiency.Advance(kind, Aptitudes[kind], proficiencyGain);
            }
        }
    }

    /// <summary>
    /// 훈련 결산을 반영합니다 — 이력 기록과 잔여 보정(반올림 꼬리·노화)만.
    /// 달수와 숙련은 이미 매달 반영되었으므로 여기서 다시 더하지 않습니다.
    /// </summary>
    internal void ApplySettlement(YearRecord record)
    {
        _history.Add(record);
        Stats = (Stats + record.StatChange).ClampToZero();
    }

    internal void ApplyYear(YearRecord record)
    {
        _history.Add(record);
        Stats = (Stats + record.StatChange).ClampToZero();
        AdvanceMonths(record.Months);

        // 그 해를 들고 있던 무기의 숙련도가 오릅니다. 사망한 해는 제외합니다.
        if (record.Outcome != DeploymentOutcome.Died)
        {
            // 훈련 연도는 세션이 계산해 넘겨줍니다 — 기술 훈련을 몇 달 했느냐로 달라지므로.
            // 실전은 무기를 쓸 수밖에 없으니 연 단위 기본값을 기간만큼 나눠 씁니다 —
            // 1달 의뢰가 1년치 숙련을 주면 짧은 의뢰만 반복하는 게 최적해가 됩니다.
            double baseGain = record.ProficiencyGain
                ?? (record.Activity == YearActivity.Deployment
                    ? WeaponProficiency.PerDeploymentYear * record.Months / MonthsPerYear
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

    /// <summary>
    /// 달을 보냅니다. <b>12달을 채울 때마다 나이가 오릅니다.</b>
    /// <para>
    /// 나이를 이력 줄 수로 세면 1달 의뢰를 받은 사람이 한 살을 먹습니다. 의뢰 기간이
    /// 1달~1년으로 갈라졌으므로 달을 세는 것 말고는 방법이 없습니다.
    /// </para>
    /// </summary>
    private void AdvanceMonths(int months)
    {
        if (months <= 0) return;

        int before = MonthsElapsed;
        MonthsElapsed += months;

        // 걸친 해의 수만큼 올립니다 — 1년짜리 의뢰 하나로 한 살, 1달 열두 번으로도 한 살.
        Age += MonthsElapsed / MonthsPerYear - before / MonthsPerYear;
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
        // 적성과 어긋날 수 있고, 그 어긋남이 전직의 동기가 됩니다 (docs/07 §16.3).
        // 풀은 지금 열려 있는 직업만입니다 (docs/07 §20.5 — 당장은 한손검+방패 하나).
        var wished = Jobs.OpenForRecruit[rng.Fork($"wish:{id}").NextInt(0, Jobs.OpenForRecruit.Count)];
        var loadout = StartingLoadoutFor(wished);

        // 태생 패시브 하나를 타고납니다. 이해도가 올라야 드러납니다 (docs/07 §16.7).
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

    /// <summary>이 직업이 처음 드는 무기. 전직 화면에서 적성과 잇는 데도 씁니다.</summary>
    public static WeaponKind StartingWeaponFor(JobId job) => job switch
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
        $"[{Loadout} 숙련 {Proficiency[Loadout.MainWeapon]}]";
}
