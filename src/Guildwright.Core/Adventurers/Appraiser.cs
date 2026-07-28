using Guildwright.Core.Rng;

namespace Guildwright.Core.Adventurers;

/// <summary>
/// 플레이어가 볼 수 있는 모험가 평가서. <b>틀릴 수 있습니다.</b>
/// </summary>
/// <param name="TimingHint">개화 시기 추정.</param>
/// <param name="TemperamentHint">기질 추정.</param>
/// <param name="EstimatedPotential">잠재력 추정치.</param>
/// <param name="Confidence">
/// 이 평가를 얼마나 믿을 수 있는지 (0.0~1.0). 플레이어에게 그대로 보여줍니다 —
/// "확신도 20%"라는 사실 자체가 판단 재료입니다.
/// </param>
public sealed record ScoutingReport(
    BloomTiming TimingHint,
    Temperament TemperamentHint,
    StatBlock EstimatedPotential,
    double Confidence)
{
    public string ConfidenceLabel => Confidence switch
    {
        < 0.25 => "짐작뿐",
        < 0.50 => "불확실",
        < 0.75 => "어느 정도 확신",
        _ => "거의 확실"
    };

    public string TimingText => TimingHint switch
    {
        BloomTiming.Early => "일찍 피는 편으로 보인다",
        BloomTiming.Late => "늦게 피는 편으로 보인다",
        _ => "평범한 성장세로 보인다"
    };

    public string TemperamentText => TemperamentHint switch
    {
        Temperament.Studious => "훈련장에서 배우는 타입",
        Temperament.Battleborn => "실전에서 배우는 타입",
        _ => "특별히 치우치지 않은 타입"
    };
}

/// <summary>
/// 숨겨진 성장 곡선을 <b>부정확하게</b> 추정합니다.
/// <para>
/// 이 게임의 핵심은 정보 비대칭입니다. 성장 타입이 완전히 숨겨져 있으면 매년의 선택이
/// 판단이 아니라 그냥 주사위가 되고, 완전히 공개되면 도박이 사라집니다.
/// 그래서 <b>틀릴 수 있는 힌트</b>를 주고, 관찰과 투자로 정확도를 올리게 합니다.
/// </para>
/// <para>
/// 결과적으로 <b>정보 자체가 길드 성장의 보상</b>이 됩니다.
/// </para>
/// 근거: docs/04-game-design.md §3.4
/// </summary>
public static class Appraiser
{
    /// <summary>확신도 0일 때도 이만큼은 맞습니다. 완전 무작위(1/3)보다는 나은 수준.</summary>
    private const double BaseAccuracy = 0.35;

    /// <summary>
    /// 평가서를 만듭니다.
    /// </summary>
    /// <param name="adventurer">대상.</param>
    /// <param name="appraisalSkill">
    /// 감정 역량 (0.0~1.0). 길드 시설과 멘토가 올려줍니다.
    /// </param>
    /// <param name="rng">난수원.</param>
    public static ScoutingReport Appraise(Adventurer adventurer, double appraisalSkill, IRandomSource rng)
    {
        double confidence = ComputeConfidence(adventurer.CompletedYears, appraisalSkill);
        double accuracy = BaseAccuracy + (1.0 - BaseAccuracy) * confidence;

        var truth = adventurer.Growth;

        var timing = rng.Chance(accuracy)
            ? truth.Timing
            : PickWrong(truth.Timing, rng);

        var temperament = rng.Chance(accuracy)
            ? truth.Temperament
            : PickWrong(truth.Temperament, rng);

        // 잠재력 추정에는 확신도에 반비례하는 오차가 붙습니다.
        double noiseScale = (1.0 - confidence) * 0.45;
        var estimated = StatBlock.Zero;
        foreach (var kind in StatBlock.AllKinds)
        {
            double noisy = truth.Potential[kind] * (1.0 + rng.NextGaussian() * noiseScale);
            estimated = estimated.With(kind, Math.Max(1, (int)Math.Round(noisy)));
        }

        return new ScoutingReport(timing, temperament, estimated, confidence);
    }

    /// <summary>
    /// 확신도. 함께 보낸 연차와 감정 역량이 올립니다.
    /// <para>
    /// 관찰 연차의 기여는 점점 둔화됩니다 — 5년쯤 지켜보면 웬만큼은 알게 되고,
    /// 그 이상은 시설과 멘토에 투자해야 올라갑니다.
    /// </para>
    /// </summary>
    public static double ComputeConfidence(int observedYears, double appraisalSkill)
    {
        double fromObservation = 1.0 - Math.Exp(-observedYears / 2.5);
        double skill = Math.Clamp(appraisalSkill, 0.0, 1.0);

        // 관찰 60% + 감정 역량 40% 가중.
        return Math.Clamp(fromObservation * 0.6 + skill * 0.4, 0.0, 1.0);
    }

    private static T PickWrong<T>(T actual, IRandomSource rng) where T : struct, Enum
    {
        var options = Enum.GetValues<T>().Where(v => !v.Equals(actual)).ToArray();
        return options[rng.NextInt(0, options.Length)];
    }
}
