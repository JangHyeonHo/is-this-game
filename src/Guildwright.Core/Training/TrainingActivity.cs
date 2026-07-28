using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Training;

/// <summary>
/// 한 달 동안 시키는 활동.
/// <para>
/// <b>능력치 이름이 아니라 활동 이름입니다.</b> 이전에는 「힘 훈련」처럼 능력치와 1:1로 붙어 있었는데,
/// 그러면 12개월 계획이 <b>같은 6지선다를 12번 반복</b>하는 것이 되고, 3월의 선택지와
/// 11월의 선택지가 완전히 동일해집니다.
/// </para>
/// <para>
/// 활동마다 여러 능력치에 걸쳐 있어서 "민첩만 순수하게" 같은 게 불가능합니다.
/// 배분이 진짜 고민이 됩니다. 그리고 아트가 없는 게임에서
/// <b>"달리기를 시켰다"가 "민첩 훈련"보다 훨씬 잘 읽힙니다.</b>
/// </para>
/// 근거: docs/08-design-revision.md §2
/// </summary>
public enum TrainingActivity
{
    /// <summary>근력 훈련 — 웨이트.</summary>
    Strength,
    /// <summary>지구력 훈련 — 달리기.</summary>
    Endurance,
    /// <summary>기술 훈련 — 무기 수련. <b>무기 숙련도가 오르는 유일한 훈련입니다.</b></summary>
    Technique,
    /// <summary>학술 훈련 — 서적 읽기.</summary>
    Study,
    /// <summary>명상.</summary>
    Meditation,
    /// <summary>모의전 — 스파링. <b>판단력이 오르는 유일한 훈련입니다.</b></summary>
    Sparring,
    /// <summary>휴식. 성장은 없지만 피로가 크게 줄고 컨디션이 회복됩니다.</summary>
    Rest
}

/// <summary>
/// 활동 하나가 무엇을 얼마나 키우는지.
/// </summary>
/// <param name="Activity">활동.</param>
/// <param name="Name">표시 이름.</param>
/// <param name="Flavor">무엇을 하는지. 화면에 같이 보여줍니다.</param>
/// <param name="Weights">능력치별 가중치. 0이면 전혀 안 오릅니다.</param>
/// <param name="FatigueCost">
/// 이 활동 한 달의 피로 증감. <b>음수면 오히려 회복합니다.</b>
/// <para>
/// 앉아서 하는 명상이 역기 드는 것만큼 지치면 말이 안 됩니다. 몸을 쓰는 활동은 크게,
/// 머리를 쓰는 활동은 적게, 명상은 회복 쪽으로 둡니다.
/// </para>
/// <para>
/// 명상이 <b>회복</b>인 것이 중요합니다 — "한 달을 버리고 확실히 쉬는 휴식"과
/// "일하면서 조금 쉬는 명상" 사이에 판단이 생깁니다.
/// 그냥 덜 깎이기만 하면 선택지가 늘지 않습니다.
/// </para>
/// </param>
/// <param name="ProficiencyPerMonth">이 활동 한 달로 오르는 장착 무기 숙련도.</param>
/// <param name="JudgementPerMonth">
/// 이 활동 한 달로 오르는 판단력.
/// <para>
/// 판단력은 개정 이후 <b>회피의 핵심 축</b>이 되는데(08-design-revision §5),
/// 예전에는 훈련으로 올릴 방법이 아예 없었습니다 — 연 단위로 실전 +6, 훈련 +2뿐이었습니다.
/// </para>
/// <para>
/// 다만 <b>실전보다 느려야 합니다.</b> 훈련장에서 실전만큼 배운다면 죽음을 무릅쓸 이유가 사라집니다.
/// </para>
/// </param>
public sealed record TrainingActivityProfile(
    TrainingActivity Activity,
    string Name,
    string Flavor,
    IReadOnlyDictionary<PrimaryStat, double> Weights,
    int FatigueCost,
    double ProficiencyPerMonth = 0.0,
    double JudgementPerMonth = 0.0)
{
    public double WeightOf(PrimaryStat stat) => Weights.GetValueOrDefault(stat, 0.0);

    /// <summary>가중치가 붙은 능력치들. 화면 표시용.</summary>
    public IEnumerable<PrimaryStat> AffectedStats =>
        PrimaryStats.AllStats.Where(s => WeightOf(s) > 0.0);
}

/// <summary>
/// 활동 목록과 가중치표.
/// <para>
/// ⚠️ <b>여기 있는 가중치는 전부 임시값입니다.</b> 배치 시뮬레이션으로 검증한 뒤
/// 데이터 파일로 분리합니다. 감으로 고치지 말고 근거를 docs/06-balance-log.md에 남기세요.
/// </para>
/// <para>
/// 활동별 가중치 <b>합계를 1.5 안팎으로 맞춰</b> 두었습니다. 합계가 크게 다르면
/// 특정 활동만 시키는 게 언제나 정답이 됩니다.
/// </para>
/// </summary>
public static class TrainingActivities
{
    private static readonly TrainingActivityProfile[] Profiles =
    [
        new(TrainingActivity.Strength, "근력 훈련", "웨이트",
            new Dictionary<PrimaryStat, double>
            {
                [PrimaryStat.Strength] = 1.0,
                [PrimaryStat.Vitality] = 0.5
            },
            FatigueCost: 20),

        new(TrainingActivity.Endurance, "지구력 훈련", "달리기",
            new Dictionary<PrimaryStat, double>
            {
                [PrimaryStat.Agility] = 0.8,
                [PrimaryStat.Vitality] = 0.7,
                [PrimaryStat.Strength] = 0.2
            },
            FatigueCost: 20),

        // 무기 숙련도가 오르는 유일한 훈련입니다.
        // 이전에는 훈련 연도를 보내기만 하면 선택과 무관하게 자동으로 올랐습니다.
        new(TrainingActivity.Technique, "기술 훈련", "무기 수련",
            new Dictionary<PrimaryStat, double>
            {
                [PrimaryStat.Finesse] = 1.0,
                [PrimaryStat.Agility] = 0.3,
                [PrimaryStat.Strength] = 0.2
            },
            FatigueCost: 17,
            ProficiencyPerMonth: 1.2),

        new(TrainingActivity.Study, "학술 훈련", "서적 읽기",
            new Dictionary<PrimaryStat, double>
            {
                [PrimaryStat.Intellect] = 1.0,
                [PrimaryStat.Spirit] = 0.3,
                [PrimaryStat.Finesse] = 0.2
            },
            FatigueCost: 8),

        new(TrainingActivity.Meditation, "명상", "정신 수양",
            new Dictionary<PrimaryStat, double>
            {
                [PrimaryStat.Spirit] = 1.0,
                [PrimaryStat.Intellect] = 0.3,
                [PrimaryStat.Vitality] = 0.2
            },
            FatigueCost: -5),

        // 판단력이 오르는 유일한 훈련. 무기도 들고 하므로 숙련도도 조금 붙습니다.
        // 12개월 내내 해도 판단력 +4.2로, 실전 1년(+6)에 못 미칩니다.
        // 훈련장에서 실전만큼 배운다면 죽음을 무릅쓸 이유가 사라집니다.
        new(TrainingActivity.Sparring, "모의전", "스파링",
            new Dictionary<PrimaryStat, double>
            {
                [PrimaryStat.Agility] = 0.6,
                [PrimaryStat.Finesse] = 0.6,
                [PrimaryStat.Strength] = 0.3
            },
            FatigueCost: 22,
            ProficiencyPerMonth: 0.5,
            JudgementPerMonth: 0.35),

        new(TrainingActivity.Rest, "휴식", "피로 회복",
            new Dictionary<PrimaryStat, double>(),
            FatigueCost: -TrainingRules.FatigueRecoveryOnRest)
    ];

    /// <summary>휴식을 제외한 훈련 활동. 메뉴 순서와 같습니다.</summary>
    public static IReadOnlyList<TrainingActivityProfile> Trainings { get; } =
        Profiles.Where(p => p.Activity != TrainingActivity.Rest).ToArray();

    public static IReadOnlyList<TrainingActivityProfile> All { get; } = Profiles;

    public static TrainingActivityProfile Of(TrainingActivity activity) =>
        Profiles[(int)activity];

    public static string NameOf(TrainingActivity activity) => Of(activity).Name;
}
