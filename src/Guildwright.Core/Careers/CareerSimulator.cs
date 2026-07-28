using Guildwright.Core.Adventurers;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Careers;

/// <summary>
/// 한 해를 진행시킵니다.
/// <para>
/// 이 게임의 중심 선택은 <b>"올해 이 아이를 훈련시킬 것인가, 실전에 내보낼 것인가"</b>입니다.
/// </para>
/// <list type="bullet">
///   <item>훈련: 안전하고 잘 자라지만, 수입이 없고 <b>나이는 그대로 먹습니다</b>.</item>
///   <item>실전: 수입과 명성과 판단력을 얻지만, <b>죽을 수 있습니다</b>.</item>
/// </list>
/// <para>
/// 개화 시기가 숨겨져 있기 때문에 이 선택이 도박이 됩니다.
/// 대기만성형을 일찍 내보내면 약한 채로 죽고, 조숙형을 계속 훈련시키면 전성기를 훈련장에서 낭비합니다.
/// </para>
/// 근거: docs/04-game-design.md §5
/// </summary>
public static class CareerSimulator
{
    /// <summary>훈련으로 한 해를 보냅니다. 위험은 없습니다.</summary>
    public static YearRecord ResolveTrainingYear(
        Adventurer adventurer,
        IRandomSource rng,
        Mentorship? mentorship = null)
    {
        EnsureActive(adventurer);

        var mentor = mentorship ?? Mentorship.None;
        double multiplier = adventurer.Growth.TrainingMultiplier * mentor.TrainingMultiplier;

        var change = ComputeStatChange(adventurer, multiplier, rng);

        string note = mentor.TrainingMultiplier > 1.0
            ? $"{adventurer.Age}세: {mentor.MentorName}의 지도 아래 훈련"
            : $"{adventurer.Age}세: 훈련";

        var record = new YearRecord(adventurer.Age, YearActivity.Training, change, null, 0, note);
        adventurer.ApplyYear(record);
        adventurer.GainJudgement(CareerRules.JudgementFromTraining);
        return record;
    }

    /// <summary>
    /// 실전으로 한 해를 보냅니다. <b>죽을 수 있습니다.</b>
    /// </summary>
    /// <param name="adventurer">대상 모험가.</param>
    /// <param name="difficulty">의뢰 난이도. 높을수록 보수가 크고 위험합니다.</param>
    /// <param name="rng">난수원.</param>
    public static YearRecord ResolveDeploymentYear(
        Adventurer adventurer,
        int difficulty,
        IRandomSource rng)
    {
        EnsureActive(adventurer);

        if (!adventurer.CanDeploy)
        {
            throw new InvalidOperationException(
                $"{adventurer.Name}은(는) 아직 실전에 나갈 수 없습니다. 등록 첫 해는 반드시 훈련입니다.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(difficulty);

        double multiplier = adventurer.Growth.DeploymentMultiplier;
        var growth = ComputeStatChange(adventurer, multiplier, rng);

        var outcome = RollOutcome(adventurer, difficulty, rng);
        var penalty = ComputeMishapPenalty(adventurer, outcome, rng);

        // 사망한 해에는 성장이 없습니다.
        var change = outcome == DeploymentOutcome.Died ? penalty : growth + penalty;

        int income = outcome == DeploymentOutcome.Died
            ? 0
            : (int)Math.Round(difficulty * CareerRules.IncomePerDifficulty * SuccessRatio(adventurer, difficulty));

        string note = outcome switch
        {
            DeploymentOutcome.Died => $"{adventurer.Age}세: 난이도 {difficulty} 의뢰에서 전사",
            DeploymentOutcome.Crippled => $"{adventurer.Age}세: 난이도 {difficulty} 의뢰에서 재기 불능의 부상",
            DeploymentOutcome.Injured => $"{adventurer.Age}세: 난이도 {difficulty} 의뢰에서 부상",
            _ => $"{adventurer.Age}세: 난이도 {difficulty} 의뢰 수행"
        };

        var record = new YearRecord(adventurer.Age, YearActivity.Deployment, change, outcome, income, note);
        adventurer.ApplyYear(record);

        if (outcome != DeploymentOutcome.Died)
        {
            adventurer.GainJudgement(CareerRules.JudgementFromDeployment);
        }

        return record;
    }

    /// <summary>
    /// 그 해의 성장과 노화를 합친 능력치 변화.
    /// <para>
    /// 남은 잠재력의 일정 비율을 흡수하는 방식이라 상한을 자연스럽게 넘지 않습니다.
    /// 개화 시기에서 멀면 <see cref="GrowthProfile.BloomFactorAt"/>가 0에 가까워
    /// <b>훈련을 시켜도 거의 자라지 않습니다.</b>
    /// </para>
    /// </summary>
    private static StatBlock ComputeStatChange(Adventurer adventurer, double activityMultiplier, IRandomSource rng)
    {
        var growth = adventurer.Growth;
        double bloom = growth.BloomFactorAt(adventurer.Age);
        double decline = growth.DeclineFactorAt(adventurer.Age);

        var change = StatBlock.Zero;

        foreach (var kind in StatBlock.AllKinds)
        {
            int current = adventurer.Stats[kind];
            int potential = growth.Potential[kind];

            int gain = 0;
            if (potential > current && bloom > 0.001)
            {
                double remaining = potential - current;
                double variance = 0.85 + rng.NextDouble() * 0.3;
                gain = (int)Math.Round(remaining * CareerRules.LearnRate * bloom * activityMultiplier * variance);
            }

            int loss = decline > 0.0 ? (int)Math.Round(current * decline) : 0;

            change = change.With(kind, gain - loss);
        }

        return change;
    }

    /// <summary>
    /// 실전 사고 판정.
    /// <para>
    /// 능력치가 난이도 요구치에 못 미칠수록 위험이 가파르게 올라갑니다.
    /// 판단력이 높으면 위험을 줄입니다 — 똑똑한 모험가는 물러설 때를 압니다.
    /// </para>
    /// </summary>
    private static DeploymentOutcome RollOutcome(Adventurer adventurer, int difficulty, IRandomSource rng)
    {
        double required = difficulty * CareerRules.RequiredPowerPerDifficulty;
        double power = Math.Max(1.0, adventurer.Stats.Total);

        double risk = CareerRules.BaseRisk * Math.Pow(required / power, 2.2);
        risk *= 1.0 - adventurer.Judgement / 100.0 * CareerRules.JudgementRiskReduction;
        risk = Math.Clamp(risk, 0.002, 0.80);

        if (!rng.Chance(risk)) return DeploymentOutcome.Unharmed;

        double severity = rng.NextDouble();
        if (severity < CareerRules.DeathShareOfMishap) return DeploymentOutcome.Died;
        if (severity < CareerRules.DeathShareOfMishap + CareerRules.CrippleShareOfMishap) return DeploymentOutcome.Crippled;
        return DeploymentOutcome.Injured;
    }

    /// <summary>사고로 잃는 능력치 (음수).</summary>
    private static StatBlock ComputeMishapPenalty(Adventurer adventurer, DeploymentOutcome outcome, IRandomSource rng)
    {
        double lossRatio = outcome switch
        {
            DeploymentOutcome.Injured => CareerRules.InjuryLossMin
                + rng.NextDouble() * (CareerRules.InjuryLossMax - CareerRules.InjuryLossMin),
            DeploymentOutcome.Crippled => CareerRules.CrippleLoss,
            _ => 0.0
        };

        if (lossRatio <= 0.0) return StatBlock.Zero;

        var penalty = StatBlock.Zero;
        foreach (var kind in StatBlock.AllKinds)
        {
            penalty = penalty.With(kind, -(int)Math.Round(adventurer.Stats[kind] * lossRatio));
        }
        return penalty;
    }

    /// <summary>난이도 대비 실력 비율. 보수 산정에 씁니다. 최대 1.2배.</summary>
    private static double SuccessRatio(Adventurer adventurer, int difficulty)
    {
        double required = difficulty * CareerRules.RequiredPowerPerDifficulty;
        return Math.Clamp(adventurer.Stats.Total / Math.Max(1.0, required), 0.3, 1.2);
    }

    private static void EnsureActive(Adventurer adventurer)
    {
        if (adventurer.Status != AdventurerStatus.Active)
        {
            throw new InvalidOperationException(
                $"{adventurer.Name}은(는) 현역이 아닙니다 (상태: {adventurer.Status}).");
        }
    }
}
