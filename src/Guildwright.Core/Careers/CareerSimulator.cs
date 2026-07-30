using Guildwright.Core.Adventurers;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;

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
    /// <summary>
    /// 훈련으로 한 해를 보냅니다. 위험은 없지만 <b>훈련 중 부상</b>은 있을 수 있습니다.
    /// <para>
    /// 내부적으로 월 단위 <see cref="TrainingYearSession"/>을 방침에 따라 자동 진행합니다.
    /// <b>플레이어가 손으로 하는 경로와 완전히 같은 모델</b>이라, 배치 시뮬레이션으로 맞춘 밸런스가
    /// 실제 플레이와 어긋나지 않습니다. (별도 모델을 두었다가 결과가 크게 벌어지는 버그를 겪었습니다.)
    /// </para>
    /// </summary>
    /// <param name="adventurer">대상 모험가.</param>
    /// <param name="rng">난수원.</param>
    /// <param name="mentorship">멘토. 없으면 보너스 없음.</param>
    /// <param name="policy">훈련 방침. 생략하면 균형 방침.</param>
    public static YearRecord ResolveTrainingYear(
        Adventurer adventurer,
        IRandomSource rng,
        Mentorship? mentorship = null,
        TrainingPolicy? policy = null)
    {
        EnsureActive(adventurer);
        return AutoTrainer.RunYear(adventurer, policy ?? TrainingPolicy.Balanced, rng, mentorship);
    }

    /// <summary>
    /// 파견 한 건을 결산합니다. <b>의뢰 기간만큼만 시간이 갑니다</b> — 1달 의뢰면 1달입니다.
    /// <para>
    /// 이것이 <see cref="DeploymentSession"/>과 육성 파트를 잇는 다리입니다.
    /// 예전에는 파견이 곧 1년이라 이 다리가 필요 없었습니다.
    /// </para>
    /// </summary>
    /// <param name="adventurer">결산할 모험가. 파견에 나갔던 사람이어야 합니다.</param>
    /// <param name="session">진행이 끝난 파견.</param>
    /// <param name="result">그 파견의 결과.</param>
    /// <param name="rng">난수원.</param>
    public static YearRecord ResolveDeployment(
        Adventurer adventurer,
        DeploymentSession session,
        DeploymentResult result,
        IRandomSource rng)
    {
        bool downed = session.Hp.GetValueOrDefault(adventurer.Id, adventurer.MaxHp) <= 0;

        return ResolveDeploymentYear(
            adventurer,
            result.Contract.Difficulty,
            rng,
            experience: session.Experience.GetValueOrDefault(adventurer.Id),
            contract: result.Contract,
            battle: BattleReport.From(result, downed),
            months: Math.Max(1, result.MonthsSpent));
    }

    /// <summary>
    /// 실전으로 <b>한 해를</b> 보냅니다. 죽을 수 있습니다.
    /// <para>
    /// 달 단위 파견에서는 <see cref="ResolveDeployment"/>를 쓰세요. 이쪽은 배치
    /// 시뮬레이션처럼 <b>전투를 따로 돌리지 않고 연 단위로 요약</b>할 때 씁니다.
    /// </para>
    /// </summary>
    /// <param name="adventurer">대상 모험가.</param>
    /// <param name="difficulty">의뢰 난이도. 높을수록 보수가 크고 위험합니다.</param>
    /// <param name="rng">난수원.</param>
    /// <param name="experience">
    /// 그 기간에 실제로 무엇을 겪었는지. 어느 능력치가 자랄지를 정합니다.
    /// <para>
    /// 생략하면 장착 무기와 위치로 근사합니다. 실제 전투를 돌렸다면
    /// <see cref="CombatExperience.From"/>으로 만든 값을 넘기세요 —
    /// 그래야 <b>파티 편성과 전술 편성이 육성에 반영</b>됩니다.
    /// </para>
    /// </param>
    /// <param name="contract">
    /// 수행한 의뢰. 형태에 따라 전투 비중과 위험이 달라집니다.
    /// 생략하면 순수 전투 의뢰로 봅니다.
    /// </param>
    /// <param name="battle">
    /// 실제로 치른 전투의 결과. 생략하면 전투를 따로 돌리지 않은 것으로 보고 무난한 수행으로 처리합니다.
    /// <para>
    /// <b>졌으면 보수가 없고 위험이 크게 오릅니다.</b> 이게 없으면 전투에서 전멸하고도
    /// 보수를 받고 승급하는 일이 생기고, 그 순간 전투를 보는 의미가 사라집니다.
    /// </para>
    /// </param>
    /// <param name="months">
    /// 이 파견이 걸린 달. 성장·보수·위험·숙련이 전부 이 비율만큼만 들어옵니다.
    /// <b>이게 없으면 1달 의뢰만 반복하는 것이 최적해가 됩니다.</b>
    /// </param>
    public static YearRecord ResolveDeploymentYear(
        Adventurer adventurer,
        int difficulty,
        IRandomSource rng,
        CombatExperience? experience = null,
        Contract? contract = null,
        BattleReport? battle = null,
        int months = Adventurer.MonthsPerYear)
    {
        EnsureActive(adventurer);

        if (!adventurer.CanDeploy)
        {
            throw new InvalidOperationException(
                $"{adventurer.Name}은(는) 아직 실전에 나갈 수 없습니다. 등록 첫 해는 반드시 훈련입니다.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(difficulty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(months);

        // 1달 의뢰가 1년치 성장·보수·위험을 주면 짧은 의뢰만 반복하는 게 최적해가 됩니다.
        double share = (double)months / Adventurer.MonthsPerYear;

        double multiplier = adventurer.Growth.DeploymentMultiplier * share;

        // 전투 기록이 없으면 손에 든 것과 위치로 근사합니다.
        var lived = experience ?? CombatExperience.FromRole(
            adventurer.Loadout.MainWeapon,
            adventurer.Loadout.CanActFromBackRow ? Row.Back : Row.Front);

        var growth = ComputeStatChange(adventurer, multiplier, rng, lived);

        // 채집 의뢰는 전투 비중이 낮아 덜 위험합니다 — 전투력이 낮은 캐릭터의 자리입니다.
        double combatWeight = contract?.CombatWeight ?? 1.0;

        // 함정 감지와 척후가 사고 위험을 줄입니다. 전투 결과도 여기에 곱해집니다.
        var report = battle ?? BattleReport.NotFought;

        // 짧은 의뢰는 위험도 짧습니다 — 노출 시간이 곧 사고 확률입니다.
        double riskMultiplier = combatWeight * report.RiskMultiplier * share;

        var outcome = RollOutcome(adventurer, difficulty, riskMultiplier, rng);
        var penalty = ComputeMishapPenalty(adventurer, outcome, rng);

        // 사망한 해에는 성장이 없습니다.
        var change = outcome == DeploymentOutcome.Died ? penalty : growth + penalty;

        int income = outcome == DeploymentOutcome.Died
            ? 0
            : (int)Math.Round(
                difficulty * CareerRules.IncomePerDifficulty
                * SuccessRatio(adventurer, difficulty, combatWeight)
                * report.IncomeRatio
                * share);

        string what = contract?.Name ?? $"난이도 {difficulty} 의뢰";
        string role = $" ({adventurer.Title})";

        string note = outcome switch
        {
            DeploymentOutcome.Died => $"{adventurer.Age}세: {what}에서 전사",
            DeploymentOutcome.Crippled => $"{adventurer.Age}세: {what}에서 재기 불능의 부상",
            DeploymentOutcome.Injured => $"{adventurer.Age}세: {what}에서 부상{role}",
            _ when report.Failed => $"{adventurer.Age}세: {what} 실패 — 살아 돌아온 것에 만족{role}",
            _ when report.Inconclusive => $"{adventurer.Age}세: {what} 미완 — 일부 보수만{role}",
            _ => $"{adventurer.Age}세: {what} 수행{role}"
        };

        var record = new YearRecord(
            adventurer.Age, YearActivity.Deployment, change, outcome, income, note, adventurer.Job,
            Months: months);
        adventurer.ApplyYear(record);

        if (outcome != DeploymentOutcome.Died)
        {
            adventurer.GainJudgement(CareerRules.JudgementFromDeployment);

            // ★ 겪은 것이 파생 수치에 직접 붙습니다.
            //   계속 맞다 보면 몸이 단단해지고, 급소를 노리다 보면 손에 익습니다.
            foreach (var (stat, amount) in lived.Bonuses)
            {
                adventurer.ApplyDerivedBonus(stat, amount);
            }
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
    private static PrimaryStats ComputeStatChange(
        Adventurer adventurer,
        double activityMultiplier,
        IRandomSource rng,
        CombatExperience experience)
    {
        var growth = adventurer.Growth;
        double bloom = growth.BloomFactorAt(adventurer.Age);
        double decline = growth.DeclineFactorAt(adventurer.Age);

        var change = PrimaryStats.Zero;

        foreach (var kind in PrimaryStats.AllStats)
        {
            int current = adventurer.Stats[kind];
            int potential = growth.Potential[kind];

            int gain = 0;
            if (potential > current && bloom > 0.001)
            {
                double remaining = potential - current;
                double variance = 0.85 + rng.NextDouble() * 0.3;

                // 그 해에 실제로 쓴 능력치가 더 자랍니다.
                double lived = experience.WeightOf(kind);

                gain = (int)Math.Round(
                    remaining * CareerRules.LearnRate * bloom * activityMultiplier * lived * variance);
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
    private static DeploymentOutcome RollOutcome(
        Adventurer adventurer,
        int difficulty,
        double riskMultiplier,
        IRandomSource rng)
    {
        double required = difficulty * CareerRules.RequiredPowerPerDifficulty;
        double power = Math.Max(1.0, adventurer.Stats.Total);

        double risk = CareerRules.BaseRisk * Math.Pow(required / power, 2.2);
        risk *= 1.0 - adventurer.Judgement / 100.0 * CareerRules.JudgementRiskReduction;
        risk *= riskMultiplier;
        risk = Math.Clamp(risk, 0.002, 0.80);

        if (!rng.Chance(risk)) return DeploymentOutcome.Unharmed;

        double severity = rng.NextDouble();
        if (severity < CareerRules.DeathShareOfMishap) return DeploymentOutcome.Died;
        if (severity < CareerRules.DeathShareOfMishap + CareerRules.CrippleShareOfMishap) return DeploymentOutcome.Crippled;
        return DeploymentOutcome.Injured;
    }

    /// <summary>사고로 잃는 능력치 (음수).</summary>
    private static PrimaryStats ComputeMishapPenalty(Adventurer adventurer, DeploymentOutcome outcome, IRandomSource rng)
    {
        double lossRatio = outcome switch
        {
            DeploymentOutcome.Injured => CareerRules.InjuryLossMin
                + rng.NextDouble() * (CareerRules.InjuryLossMax - CareerRules.InjuryLossMin),
            DeploymentOutcome.Crippled => CareerRules.CrippleLoss,
            _ => 0.0
        };

        if (lossRatio <= 0.0) return PrimaryStats.Zero;

        var penalty = PrimaryStats.Zero;
        foreach (var kind in PrimaryStats.AllStats)
        {
            penalty = penalty.With(kind, -(int)Math.Round(adventurer.Stats[kind] * lossRatio));
        }
        return penalty;
    }

    /// <summary>
    /// 난이도 대비 실력 비율. 보수 산정에 씁니다. 최대 1.2배.
    /// <para>
    /// 전투 비중이 낮은 의뢰(채집 등)에서는 전투력이 보수를 덜 좌우합니다.
    /// 그래야 전투가 약한 캐릭터도 제 몫을 하는 자리가 생깁니다.
    /// </para>
    /// </summary>
    private static double SuccessRatio(Adventurer adventurer, int difficulty, double combatWeight)
    {
        double required = difficulty * CareerRules.RequiredPowerPerDifficulty;
        double byCombat = Math.Clamp(adventurer.Stats.Total / Math.Max(1.0, required), 0.3, 1.2);

        // 전투 비중만큼만 전투력을 반영하고, 나머지는 평범한 수행으로 봅니다.
        return byCombat * combatWeight + 1.0 * (1.0 - combatWeight);
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
