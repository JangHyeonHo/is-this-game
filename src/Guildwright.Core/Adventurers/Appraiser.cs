using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

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
/// <param name="AptitudeHints">스타일별 적성 추정. 확신도가 낮으면 실제와 다를 수 있습니다.</param>
public sealed record ScoutingReport(
    BloomTiming TimingHint,
    Temperament TemperamentHint,
    PrimaryStats EstimatedPotential,
    double Confidence,
    IReadOnlyDictionary<WeaponKind, AptitudeGrade> AptitudeHints)
{
    /// <summary>추정상 가장 잘 맞아 보이는 스타일.</summary>
    public WeaponKind SuggestedWeapon =>
        AptitudeHints.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;

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
    /// <param name="mentorBonus">
    /// 감정 역량 (0.0~1.0). 길드 시설과 멘토가 올려줍니다.
    /// </param>
    /// <param name="rng">난수원.</param>
    public static ScoutingReport Appraise(Adventurer adventurer, double mentorBonus, IRandomSource rng)
    {
        double confidence = ComputeConfidence(adventurer.CompletedYears, mentorBonus);
        double accuracy = BaseAccuracy + (1.0 - BaseAccuracy) * confidence;

        var truth = adventurer.Growth;

        var timing = rng.Chance(accuracy)
            ? truth.Timing
            : PickWrong(truth.Timing, rng);

        var temperament = rng.Chance(accuracy)
            ? truth.Temperament
            : PickWrong(truth.Temperament, rng);

        // 잠재력 추정에는 확신도에 반비례하는 오차가 붙습니다.
        //
        // ⚠️ 오차에 상한을 둡니다. 정규분포를 그대로 쓰면 실제 80인 능력치가 163으로 추정되는
        //    일이 생기는데, 그건 "정보가 부족한 것"이 아니라 "거짓말"입니다.
        //    계획 화면이 헛소리가 되면 플레이어는 아예 안 보게 됩니다.
        double noiseScale = (1.0 - confidence) * 0.30;
        double bound = noiseScale * 1.5;

        var estimated = PrimaryStats.Zero;
        foreach (var kind in PrimaryStats.AllStats)
        {
            double factor = Math.Clamp(1.0 + rng.NextGaussian() * noiseScale, 1.0 - bound, 1.0 + bound);
            estimated = estimated.With(kind, Math.Max(1, (int)Math.Round(truth.Potential[kind] * factor)));
        }

        // 무기 적성도 같은 확신도를 따릅니다. 등급이 인접 등급으로 흔들립니다.
        var aptitudeHints = new Dictionary<WeaponKind, AptitudeGrade>();
        foreach (var style in Weaponry.Trainable)
        {
            var actual = adventurer.Aptitudes[style];
            aptitudeHints[style] = rng.Chance(accuracy) ? actual : Shift(actual, rng);
        }

        return new ScoutingReport(timing, temperament, estimated, confidence, aptitudeHints);
    }

    /// <summary>
    /// 등급을 한 칸 위나 아래로 흔듭니다.
    /// <para>
    /// 완전 무작위 등급이 아니라 인접 등급으로만 틀리게 하는 이유는,
    /// S 적성을 E로 보는 식의 극단적 오류가 나오면 감정이라는 행위 자체가 무의미해 보이기 때문입니다.
    /// </para>
    /// </summary>
    private static AptitudeGrade Shift(AptitudeGrade grade, IRandomSource rng)
    {
        int direction = rng.Chance(0.5) ? -1 : 1;
        int shifted = (int)grade + direction;

        if (shifted < (int)AptitudeGrade.E) shifted = (int)AptitudeGrade.D;
        if (shifted > (int)AptitudeGrade.S) shifted = (int)AptitudeGrade.A;

        return (AptitudeGrade)shifted;
    }

    /// <summary>
    /// 확신도. 함께 보낸 연차와 감정 역량이 올립니다.
    /// <para>
    /// 관찰 연차의 기여는 점점 둔화됩니다 — 5년쯤 지켜보면 웬만큼은 알게 되고,
    /// 그 이상은 시설과 멘토에 투자해야 올라갑니다.
    /// </para>
    /// </summary>
    public static double ComputeConfidence(int observedYears, double mentorBonus)
    {
        double fromObservation = 1.0 - Math.Exp(-observedYears / 2.5);
        double skill = Math.Clamp(mentorBonus, 0.0, 1.0);

        // 관찰 60% + 감정 역량 40% 가중.
        return Math.Clamp(fromObservation * 0.6 + skill * 0.4, 0.0, 1.0);
    }

    private static T PickWrong<T>(T actual, IRandomSource rng) where T : struct, Enum
    {
        var options = Enum.GetValues<T>().Where(v => !v.Equals(actual)).ToArray();
        return options[rng.NextInt(0, options.Length)];
    }
}
