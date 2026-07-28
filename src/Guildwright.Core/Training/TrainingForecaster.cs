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
        IReadOnlyList<TrainingFocus> plan,
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

        // 계획을 그대로 훑으며 평균적인 피로/컨디션을 가정해 누적합니다.
        var accumulated = new double[PrimaryStats.AllStats.Count];
        int fatigue = 0;

        foreach (var focus in plan)
        {
            if (focus == TrainingFocus.Rest)
            {
                fatigue = Math.Max(0, fatigue - TrainingRules.FatigueRecoveryOnRest);
                continue;
            }

            double fatiguePenalty = fatigue <= TrainingRules.FatigueSoftCap
                ? 1.0
                : Math.Max(0.35, 1.0 - (double)(fatigue - TrainingRules.FatigueSoftCap)
                    / (TrainingRules.MaxFatigue - TrainingRules.FatigueSoftCap) * 0.65);

            var focused = ToStat(focus);

            foreach (var stat in PrimaryStats.AllStats)
            {
                double current = adventurer.Stats[stat] + accumulated[(int)stat];
                int potential = guessed.Potential[stat];
                if (potential <= current) continue;

                double share = stat == focused ? 1.0 : TrainingRules.SpilloverRatio;
                accumulated[(int)stat] +=
                    (potential - current) * TrainingRules.MonthlyLearnRate * multiplier * fatiguePenalty * share;
            }

            fatigue = Math.Min(TrainingRules.MaxFatigue, fatigue + TrainingRules.FatiguePerTraining);
        }

        // 확신도가 낮을수록 범위가 넓어집니다.
        double spread = IrreducibleSpread + (1.0 - report.Confidence) * UncertaintySpread;

        return PrimaryStats.AllStats
            .Select(stat =>
            {
                double center = accumulated[(int)stat];
                int min = Math.Max(0, (int)Math.Floor(center * (1.0 - spread)));
                int max = (int)Math.Ceiling(center * (1.0 + spread));
                return new StatForecast(stat, min, max);
            })
            .ToList();
    }

    private static PrimaryStat ToStat(TrainingFocus focus) => focus switch
    {
        TrainingFocus.Strength => PrimaryStat.Strength,
        TrainingFocus.Agility => PrimaryStat.Agility,
        TrainingFocus.Finesse => PrimaryStat.Finesse,
        TrainingFocus.Vitality => PrimaryStat.Vitality,
        TrainingFocus.Intellect => PrimaryStat.Intellect,
        TrainingFocus.Spirit => PrimaryStat.Spirit,
        _ => throw new ArgumentOutOfRangeException(nameof(focus))
    };
}
