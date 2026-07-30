using Guildwright.Core.Rng;

namespace Guildwright.Core.Adventurers;

/// <summary>
/// 기질. 훈련과 실전 중 어느 쪽에서 더 크게 성장하는지를 결정합니다.
/// </summary>
public enum Temperament
{
    /// <summary>수련형. 훈련에서 잘 자라지만 실전에서는 별로 배우지 못합니다.</summary>
    Studious,
    /// <summary>균형형.</summary>
    Balanced,
    /// <summary>실전형. 훈련보다 실전에서 훨씬 빠르게 성장합니다. 다만 실전은 죽을 수 있습니다.</summary>
    Battleborn
}

/// <summary>
/// 개화 시기. 능력이 언제 만개하는지를 나타냅니다.
/// <para>
/// 이 게임의 중심 도박입니다. 대기만성형을 일찍 실전에 내보내면 약한 채로 죽고,
/// 조숙형을 계속 육성하면 전성기를 훈련장에서 낭비합니다.
/// </para>
/// </summary>
public enum BloomTiming
{
    /// <summary>조숙형. 17세 전후에 만개하고 일찍 저뭅니다.</summary>
    Early,
    /// <summary>보통.</summary>
    Normal,
    /// <summary>대기만성형. 25세 전후에야 만개합니다. 그때까지 살아남아야 합니다.</summary>
    Late
}

/// <summary>
/// 한 모험가의 성장 곡선. <b>플레이어에게 직접 보이지 않습니다.</b>
/// <para>
/// 플레이어는 <see cref="ScoutingReport"/>의 부정확한 힌트로 추측하고,
/// 육성 연차가 쌓이면서 점점 정확히 알게 됩니다.
/// </para>
/// 근거: docs/01-game-design.md §3.4
/// </summary>
public sealed record GrowthProfile
{
    /// <summary>능력이 만개하는 나이.</summary>
    public required int PeakAge { get; init; }

    /// <summary>개화 구간의 폭. 클수록 완만하게 오래 자랍니다.</summary>
    public required double BloomWidth { get; init; }

    public required Temperament Temperament { get; init; }

    /// <summary>도달 가능한 능력치 상한. 훈련으로도 여기를 넘지 못합니다.</summary>
    public required PrimaryStats Potential { get; init; }

    /// <summary>노화가 시작되는 나이. 이후 매년 능력치가 조금씩 깎입니다.</summary>
    public required int DeclineAge { get; init; }

    public BloomTiming Timing => PeakAge switch
    {
        <= 19 => BloomTiming.Early,
        >= 24 => BloomTiming.Late,
        _ => BloomTiming.Normal
    };

    /// <summary>
    /// 개화기에서 완전히 벗어나도 최소한 이만큼은 자랍니다.
    /// <para>
    /// 이 하한이 없으면 대기만성형이 15~22세 동안 아무것도 못 배우게 되어,
    /// 플레이어가 데리고 있을 이유가 사라집니다. 개화기는 "성장 배율"이지
    /// "성장 가능 여부"가 아니어야 합니다.
    /// </para>
    /// </summary>
    public const double OffPeakGrowthFloor = 0.22;

    /// <summary>
    /// 해당 나이에 얼마나 잘 자라는지 (<see cref="OffPeakGrowthFloor"/> ~ 1.0).
    /// 개화 나이를 중심으로 한 종 모양 곡선입니다.
    /// </summary>
    public double BloomFactorAt(int age)
    {
        double d = age - PeakAge;
        double bell = Math.Exp(-(d * d) / (2.0 * BloomWidth * BloomWidth));
        return OffPeakGrowthFloor + (1.0 - OffPeakGrowthFloor) * bell;
    }

    /// <summary>해당 나이에 노화로 잃는 능력치 비율 (0.0 이상).</summary>
    public double DeclineFactorAt(int age)
    {
        if (age <= DeclineAge) return 0.0;
        return (age - DeclineAge) * 0.015;
    }

    /// <summary>훈련 성장 배율.</summary>
    public double TrainingMultiplier => Temperament switch
    {
        Temperament.Studious => 1.30,
        Temperament.Balanced => 1.00,
        Temperament.Battleborn => 0.70,
        _ => 1.0
    };

    /// <summary>실전 성장 배율. 실전은 수입과 명성을 주지만 성장은 기질에 크게 좌우됩니다.</summary>
    public double DeploymentMultiplier => Temperament switch
    {
        Temperament.Studious => 0.55,
        Temperament.Balanced => 0.85,
        Temperament.Battleborn => 1.35,
        _ => 1.0
    };

    /// <summary>무작위 성장 프로필을 만듭니다. 영입 시점에 확정되며 이후 바뀌지 않습니다.</summary>
    public static GrowthProfile Roll(IRandomSource rng, int potentialTier = 3)
    {
        // 개화 시기 분포: 조숙 25% / 보통 50% / 대기만성 25%
        double roll = rng.NextDouble();
        int peakAge = roll switch
        {
            < 0.25 => rng.NextInt(17, 20),   // 17~19
            < 0.75 => rng.NextInt(20, 24),   // 20~23
            _ => rng.NextInt(24, 28)         // 24~27
        };

        var temperament = rng.NextDouble() switch
        {
            < 0.30 => Temperament.Studious,
            < 0.70 => Temperament.Balanced,
            _ => Temperament.Battleborn
        };

        // 잠재력은 능력치마다 다릅니다 — 이게 캐릭터별 특화를 만듭니다.
        int baseline = 30 + potentialTier * 10;
        var potential = PrimaryStats.Zero;
        foreach (var kind in PrimaryStats.AllStats)
        {
            double variance = 0.65 + rng.NextDouble() * 0.7;   // 0.65 ~ 1.35배
            potential = potential.With(kind, Math.Max(10, (int)Math.Round(baseline * variance)));
        }

        return new GrowthProfile
        {
            PeakAge = peakAge,
            BloomWidth = 2.5 + rng.NextDouble() * 2.0,
            Temperament = temperament,
            Potential = potential,
            // 개화가 늦으면 저무는 것도 늦습니다.
            DeclineAge = peakAge + 6 + rng.NextInt(0, 5)
        };
    }
}
