using Guildwright.Core.Rng;

namespace Guildwright.Core.Combat;

/// <summary>
/// 데미지 계산.
/// <para>
/// ⚠️ 여기 있는 수치는 <b>전부 임시값</b>입니다. M1에서 배치 시뮬레이션으로 검증한 뒤
/// 데이터 파일로 분리할 예정입니다. 감으로 고치지 말고 시뮬레이션 결과를 근거로 고치세요.
/// </para>
/// </summary>
public static class DamageModel
{
    /// <summary>방어 태세일 때 받는 피해 배율.</summary>
    public const double DefendMultiplier = 0.5;

    /// <summary>회복약이 회복하는 최대 HP 비율.</summary>
    public const double PotionHealRatio = 0.4;

    /// <summary>데미지 변동 폭 (±20%).</summary>
    private const double Variance = 0.2;

    /// <summary>난수를 배제한 기댓값. AI가 "이번 턴에 죽일 수 있는가"를 판단할 때 씁니다.</summary>
    public static int ExpectedDamage(Combatant attacker, Combatant defender)
    {
        double raw = attacker.Attack - defender.Defense * 0.5;
        if (defender.IsDefending) raw *= DefendMultiplier;
        return Math.Max(1, (int)Math.Round(raw));
    }

    public static int RollDamage(Combatant attacker, Combatant defender, IRandomSource rng)
    {
        double raw = attacker.Attack - defender.Defense * 0.5;
        if (defender.IsDefending) raw *= DefendMultiplier;

        double multiplier = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * Variance;
        return Math.Max(1, (int)Math.Round(raw * multiplier));
    }

    public static int PotionHealAmount(Combatant target) =>
        Math.Max(1, (int)Math.Round(target.MaxHp * PotionHealRatio));
}
