using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
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
/// <param name="SupportRole">그 해에 맡은 비전투 역할. 없으면 전투원으로만 굴렀다는 뜻입니다.</param>
public sealed record YearRecord(
    int Age,
    YearActivity Activity,
    StatBlock StatChange,
    DeploymentOutcome? Outcome,
    int Income,
    string Note,
    SupportSkill? SupportRole = null);

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
        StatBlock startingStats,
        int judgement,
        GrowthProfile growth,
        int age = RecruitAge,
        WeaponAptitudes? aptitudes = null,
        WeaponStyle equippedStyle = WeaponStyle.SwordAndShield,
        WeaponClass equippedClass = WeaponClass.Blade)
    {
        Id = id;
        Name = name;
        Stats = startingStats;
        Judgement = Math.Clamp(judgement, 0, 100);
        Growth = growth;
        Age = age;
        Aptitudes = aptitudes ?? WeaponAptitudes.Uniform(AptitudeGrade.C);
        EquippedStyle = equippedStyle;
        EquippedClass = equippedClass;
    }

    public string Id { get; }
    public string Name { get; }
    public int Age { get; private set; }
    public StatBlock Stats { get; private set; }
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
    /// 비전투 역량. 함정 감지·척후·운반·채집·감정.
    /// <para>
    /// 전투력이 낮아도 여기가 뛰어나면 파티에 자리가 있습니다.
    /// 무기 숙련도와 같은 원리로, 맡은 역할의 이력입니다.
    /// </para>
    /// </summary>
    public SupportSkillSet Support { get; } = new();

    public WeaponStyle EquippedStyle { get; private set; }

    public WeaponClass EquippedClass { get; private set; }

    /// <summary>
    /// 무기를 바꿉니다. 예전 숙련도는 사라지지 않지만, 새 스타일은 처음부터입니다.
    /// </summary>
    /// <exception cref="ArgumentException">해당 스타일에 장착할 수 없는 무기종인 경우.</exception>
    public void Equip(WeaponStyle style, WeaponClass weaponClass)
    {
        var allowed = WeaponStyles.AllowedClasses(style);
        if (!allowed.Contains(weaponClass))
        {
            throw new ArgumentException(
                $"{style.ToKorean()}에는 {weaponClass.ToKorean()}을(를) 장착할 수 없습니다. " +
                $"가능: {string.Join(", ", allowed.Select(c => c.ToKorean()))}",
                nameof(weaponClass));
        }

        EquippedStyle = style;
        EquippedClass = weaponClass;
    }

    /// <summary>현재 장비의 전투 효율. 숙련도에서 나옵니다.</summary>
    public double WeaponEffectiveness => Proficiency.EffectivenessOf(EquippedStyle);

    /// <summary>
    /// 현재 직업 등급. <b>지금 든 무기의 숙련도</b>에서 나옵니다.
    /// <para>
    /// 무기를 바꾸면 등급이 떨어집니다 — 대마법사가 대검을 잡으면 견습 전사입니다.
    /// 전직의 대가가 여기서 나옵니다. 다만 예전 숙련도는 남아 있으므로 돌아갈 수는 있습니다.
    /// </para>
    /// </summary>
    public JobRank Rank => JobRanks.FromProficiency(Proficiency[EquippedStyle]);

    /// <summary>현재 칭호. 예: "대마법사", "견습 창병".</summary>
    public string Title => JobRanks.TitleOf(EquippedStyle, Rank);

    /// <summary>
    /// 여태 도달한 최고 등급 (스타일 무관).
    /// <para>
    /// 전직해서 견습으로 돌아가도 "한때 대마법사였던" 사실은 남습니다.
    /// 이력이 곧 이 게임의 서사이므로, 잃어버린 것도 기록해 둡니다.
    /// </para>
    /// </summary>
    public JobRank PeakRank =>
        WeaponStyles.All.Select(s => JobRanks.FromProficiency(Proficiency[s])).Max();

    /// <summary>연간 급여. 등급이 오르면 유지비도 오릅니다.</summary>
    public int AnnualWage => JobRanks.AnnualWage(Rank);

    /// <summary>수주할 수 있는 의뢰 난이도 상한.</summary>
    public int MaxContractDifficulty => JobRanks.MaxContractDifficulty(Rank);

    /// <summary>길드 평판에 기여하는 정도.</summary>
    public int ReputationValue => JobRanks.ReputationValue(Rank);

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
            double baseGain = record.Activity == YearActivity.Deployment
                ? WeaponProficiency.PerDeploymentYear
                : WeaponProficiency.PerTrainingYear;

            Proficiency.Advance(EquippedStyle, Aptitudes[EquippedStyle], baseGain);

            // 비전투 역량은 실전에서만 늡니다. 훈련장에서는 함정을 만날 일이 없습니다.
            if (record.Activity == YearActivity.Deployment)
            {
                Support.AdvanceYear(record.SupportRole, Stats);
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
        var starting = StatBlock.Zero;
        foreach (var kind in StatBlock.AllKinds)
        {
            double ratio = 0.15 + rng.NextDouble() * 0.10;
            starting = starting.With(kind, Math.Max(1, (int)Math.Round(growth.Potential[kind] * ratio)));
        }

        int judgement = 10 + rng.NextInt(0, 25);
        var aptitudes = WeaponAptitudes.Roll(growth.Potential, rng.Fork($"aptitude:{id}"));

        // 기본 장비는 가장 잘 맞는 스타일로. 플레이어가 나중에 바꿀 수 있습니다.
        var style = aptitudes.Best;
        var weaponClass = WeaponStyles.AllowedClasses(style)[0];

        return new Adventurer(id, name, starting, judgement, growth, RecruitAge, aptitudes, style, weaponClass);
    }

    public override string ToString() =>
        $"{Name} · {Title} ({Age}세, {Status}) {Stats} 판단력 {Judgement} " +
        $"[{EquippedClass.ToKorean()} 숙련 {Proficiency[EquippedStyle]}, 연봉 {AnnualWage}]";
}
