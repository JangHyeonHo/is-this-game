using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;

namespace Guildwright.Core.Training;

/// <param name="Stat">능력치.</param>
/// <param name="Min">예상 하한.</param>
/// <param name="Max">예상 상한.</param>
public readonly record struct StatForecast(PrimaryStat Stat, int Min, int Max)
{
    public override string ToString() => $"{Stat.ToKorean()} +{Min}~+{Max}";
}

/// <summary>
/// 원천 능력치가 그만큼 오르면 <b>전투 수치가 얼마나 달라지는지</b>.
/// <para>
/// 원천만 보여주면 "힘 +12"가 실제로 뭘 의미하는지 알 수 없습니다.
/// 육성의 목적은 전투이므로, 계획 화면에서 전투 수치의 변화를 봐야 판단이 됩니다.
/// </para>
/// </summary>
/// <param name="Stat">파생 수치.</param>
/// <param name="Min">예상 하한 증가분.</param>
/// <param name="Max">예상 상한 증가분.</param>
public readonly record struct DerivedForecast(DerivedStat Stat, double Min, double Max)
{
    /// <summary>비율로 표시해야 하는 수치인지 (치명타율·회피율).</summary>
    public bool IsRate => Stat is DerivedStat.CritChance or DerivedStat.EvasionChance;

    /// <summary>눈에 띄게 움직이는지. 0에 가까운 항목까지 늘어놓으면 화면이 잡음이 됩니다.</summary>
    public bool Moves => Math.Abs(Max) >= (IsRate ? 0.005 : 0.5);
}

/// <summary>
/// 1년 계획의 예상 결과 전체.
/// <para>
/// 성장만 보여주고 <b>피로를 안 보여주면</b> 계획 화면이 반쪽입니다.
/// 피로는 이 게임에서 매달 선택의 대가 그 자체인데, 실행에 들어가서야 알게 되면
/// 미리 짜는 의미가 없습니다. 피로는 계획만으로 정확히 계산되므로
/// (부상이 나지 않는 한) <b>숨길 이유도 없습니다.</b>
/// </para>
/// </summary>
/// <param name="Stats">원천 능력치 예상 증가 범위.</param>
/// <param name="Derived">그에 따른 전투 수치 변화 범위.</param>
/// <param name="FatigueByMonth">
/// 각 달의 행동을 마친 시점의 피로도 (12개). <b>실패하지 않았을 때 기준</b>입니다 —
/// 실패하면 +25가 붙어 어긋나므로, 실패 확률은 <see cref="FailureChanceByMonth"/>로 따로 봅니다.
/// </param>
/// <param name="FailureChanceByMonth">각 달의 실패 확률. 휴식인 달은 0.</param>
/// <param name="ProficiencyGain">계획대로 했을 때 오르는 장착 무기 숙련도.</param>
public sealed record YearForecast(
    IReadOnlyList<StatForecast> Stats,
    IReadOnlyList<DerivedForecast> Derived,
    IReadOnlyList<int> FatigueByMonth,
    IReadOnlyList<double> FailureChanceByMonth,
    double ProficiencyGain)
{
    public int PeakFatigue => FatigueByMonth.Count == 0 ? 0 : FatigueByMonth.Max();

    /// <summary>실패 확률이 붙은 달 수. 0이 아니면 계획을 다시 볼 이유가 됩니다.</summary>
    public int MonthsAtRisk => FailureChanceByMonth.Count(c => c > 0.0);

    /// <summary>가장 위험한 달의 실패 확률.</summary>
    public double WorstFailureChance =>
        FailureChanceByMonth.Count == 0 ? 0.0 : FailureChanceByMonth.Max();

    /// <summary>12개월 중 실패할 것으로 기대되는 달 수.</summary>
    public double ExpectedFailedMonths => FailureChanceByMonth.Sum();
}

/// <summary>
/// 1년 계획의 예상 성장을 계산합니다.
/// <para>
/// <b>정확한 예상치를 보여주면 숨겨둔 성장 곡선이 새어나갑니다.</b>
/// "힘 훈련하면 +7"을 정확히 알려주는 순간 플레이어는 역산으로 잠재력과 개화 시기를
/// 알아내고, 그러면 감정 시스템이 통째로 무의미해집니다.
/// </para>
/// <para>
/// 그래서 예상은 <b>플레이어가 아는 것</b>(스카우트 리포트의 추정 잠재력과 추정 개화 시기)만
/// 가지고 계산하고, 확신도만큼만 범위를 좁힙니다.
/// 확신도가 낮으면 범위가 넓을 뿐 아니라 <b>중심값 자체가 틀릴 수 있습니다.</b>
/// </para>
/// <para>
/// 결과적으로 <b>감정에 투자할수록 계획을 정확히 세울 수 있게</b> 됩니다.
/// </para>
/// 근거: docs/04-game-design.md §5.5
/// </summary>
public static class TrainingForecaster
{
    /// <summary>확신도가 100%여도 이만큼의 폭은 남습니다 (변동과 컨디션 때문).</summary>
    private const double IrreducibleSpread = 0.14;

    /// <summary>확신도 0일 때 추가로 벌어지는 폭.</summary>
    private const double UncertaintySpread = 0.55;

    /// <summary>추정 개화 시기로부터 짐작하는 개화 나이.</summary>
    private static int GuessPeakAge(BloomTiming timing) => timing switch
    {
        BloomTiming.Early => 18,
        BloomTiming.Late => 25,
        _ => 21
    };

    /// <summary>
    /// 12개월 계획의 예상 성장을 냅니다.
    /// </summary>
    /// <param name="adventurer">대상.</param>
    /// <param name="report">플레이어가 가진 평가서. 이것만 근거로 씁니다.</param>
    /// <param name="plan">12개월 계획.</param>
    /// <param name="mentorship">멘토.</param>
    public static IReadOnlyList<StatForecast> ForecastYear(
        Adventurer adventurer,
        ScoutingReport report,
        IReadOnlyList<TrainingActivity> plan,
        Mentorship? mentorship = null)
        => Forecast(adventurer, report, plan, mentorship).Stats;

    /// <summary>
    /// 12개월 계획의 예상 결과를 전부 냅니다 — 원천 성장, 전투 수치 변화, 달마다의 피로.
    /// </summary>
    /// <param name="adventurer">대상.</param>
    /// <param name="report">플레이어가 가진 평가서. 성장 예상은 이것만 근거로 씁니다.</param>
    /// <param name="plan">12개월 계획.</param>
    /// <param name="mentorship">멘토.</param>
    public static YearForecast Forecast(
        Adventurer adventurer,
        ScoutingReport report,
        IReadOnlyList<TrainingActivity> plan,
        Mentorship? mentorship = null)
    {
        var mentor = mentorship ?? Mentorship.None;

        // 플레이어가 아는 것만 씁니다 — 실제 개화 나이가 아니라 추정 개화 나이.
        var guessed = new GrowthProfile
        {
            PeakAge = GuessPeakAge(report.TimingHint),
            BloomWidth = 3.2,
            Temperament = report.TemperamentHint,
            Potential = report.EstimatedPotential,
            DeclineAge = GuessPeakAge(report.TimingHint) + 8
        };

        double bloom = guessed.BloomFactorAt(adventurer.Age);
        double multiplier = bloom * guessed.TrainingMultiplier * mentor.TrainingMultiplier;

        // 계획을 그대로 훑으며 평균적인 컨디션을 가정해 누적합니다.
        //
        // 피로는 계획만으로 정확히 정해지지만, 실패하면 +25가 붙어 어긋납니다.
        // 그래서 예보는 <b>실패하지 않았을 때</b>를 기준으로 내고, 실패 확률을 따로 보여줍니다.
        var accumulated = new double[PrimaryStats.AllStats.Count];
        var fatigueByMonth = new List<int>(plan.Count);
        var failureByMonth = new List<double>(plan.Count);
        double proficiency = 0.0;
        int fatigue = 0;

        foreach (var activity in plan)
        {
            if (activity == TrainingActivity.Rest)
            {
                fatigue = Math.Max(0, fatigue - TrainingRules.FatigueRecoveryOnRest);
                fatigueByMonth.Add(fatigue);
                failureByMonth.Add(0.0);
                continue;
            }

            // 실패 판정은 훈련 전 피로도로 합니다. 세션과 같은 순서여야 화면과 실제가 맞습니다.
            failureByMonth.Add(TrainingYearSession.FailureChanceAt(fatigue));

            double fatiguePenalty = fatigue <= TrainingRules.FatigueSoftCap
                ? 1.0
                : Math.Max(0.35, 1.0 - (double)(fatigue - TrainingRules.FatigueSoftCap)
                    / (TrainingRules.MaxFatigue - TrainingRules.FatigueSoftCap) * 0.65);

            var profile = TrainingActivities.Of(activity);
            proficiency += profile.ProficiencyPerMonth * mentor.TrainingMultiplier;

            foreach (var stat in PrimaryStats.AllStats)
            {
                double weight = profile.WeightOf(stat);
                if (weight <= 0.0) continue;

                double current = adventurer.Stats[stat] + accumulated[(int)stat];
                int potential = guessed.Potential[stat];
                if (potential <= current) continue;

                accumulated[(int)stat] +=
                    (potential - current) * TrainingRules.MonthlyLearnRate * multiplier * fatiguePenalty * weight;
            }

            fatigue = Math.Min(TrainingRules.MaxFatigue, fatigue + TrainingRules.FatiguePerTraining);
            fatigueByMonth.Add(fatigue);
        }

        // 확신도가 낮을수록 범위가 넓어집니다.
        double spread = IrreducibleSpread + (1.0 - report.Confidence) * UncertaintySpread;

        var stats = PrimaryStats.AllStats
            .Select(stat =>
            {
                double center = accumulated[(int)stat];
                int min = Math.Max(0, (int)Math.Floor(center * (1.0 - spread)));
                int max = (int)Math.Ceiling(center * (1.0 + spread));
                return new StatForecast(stat, min, max);
            })
            .ToList();

        return new YearForecast(
            stats, ForecastDerived(adventurer, stats), fatigueByMonth, failureByMonth, proficiency);
    }

    /// <summary>
    /// 원천 예상치를 전투 수치로 옮깁니다.
    /// <para>
    /// 새 불확실성을 더하지 않습니다 — 파생은 원천의 순수 함수이므로,
    /// 원천 예상의 하한·상한을 그대로 통과시킨 값이 곧 파생의 하한·상한입니다.
    /// </para>
    /// </summary>
    private static IReadOnlyList<DerivedForecast> ForecastDerived(
        Adventurer adventurer,
        IReadOnlyList<StatForecast> stats)
    {
        var now = adventurer.Stats;
        var atMin = now;
        var atMax = now;

        foreach (var f in stats)
        {
            atMin = atMin.With(f.Stat, now[f.Stat] + f.Min);
            atMax = atMax.With(f.Stat, now[f.Stat] + f.Max);
        }

        var bonuses = adventurer.Bonuses;

        return Enum.GetValues<DerivedStat>()
            .Select(kind =>
            {
                double baseline = ValueOf(kind, now, bonuses);
                return new DerivedForecast(
                    kind,
                    ValueOf(kind, atMin, bonuses) - baseline,
                    ValueOf(kind, atMax, bonuses) - baseline);
            })
            .ToList();
    }

    private static double ValueOf(DerivedStat kind, PrimaryStats p, DerivedBonuses b) => kind switch
    {
        DerivedStat.MaxHp => DerivedStats.MaxHp(p, b),
        DerivedStat.MaxMana => DerivedStats.MaxMana(p, b),
        DerivedStat.PhysicalPower => DerivedStats.PhysicalPower(p, b),
        DerivedStat.PhysicalGuard => DerivedStats.PhysicalGuard(p, b),
        DerivedStat.MagicPower => DerivedStats.MagicPower(p, b),
        DerivedStat.MagicGuard => DerivedStats.MagicGuard(p, b),
        DerivedStat.ActionSpeed => DerivedStats.ActionSpeed(p, b),
        DerivedStat.CritChance => DerivedStats.CritChance(p, b),
        DerivedStat.EvasionChance => DerivedStats.EvasionChance(p, b),
        _ => 0.0
    };

}
