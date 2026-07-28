using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

/// <summary>한 번의 공격이 어떻게 끝났는지.</summary>
/// <param name="Damage">실제로 들어간 피해. 회피했으면 0.</param>
/// <param name="Evaded">회피당했는지.</param>
/// <param name="Critical">치명타였는지.</param>
public readonly record struct AttackResult(int Damage, bool Evaded, bool Critical);

/// <summary>
/// 데미지·회복 계산.
/// <para>
/// ⚠️ 여기 있는 수치는 <b>전부 임시값</b>입니다. 배치 시뮬레이션으로 검증한 뒤
/// 데이터 파일로 분리할 예정입니다. 감으로 고치지 말고 근거를 docs/06-balance-log.md에 남기세요.
/// </para>
/// </summary>
public static class DamageModel
{
    /// <summary>방어 태세일 때 받는 피해 배율.</summary>
    public const double DefendMultiplier = 0.5;

    /// <summary>후열에 있을 때 받는 피해 배율.</summary>
    public const double BackRowDefenseBonus = 0.75;

    /// <summary>후열에서 근접 공격을 할 때의 위력 배율.</summary>
    public const double MeleeFromBackRowPenalty = 0.45;

    /// <summary>광역 공격의 대상당 위력 배율.</summary>
    public const double AreaAttackMultiplier = 0.55;

    /// <summary>회복약이 회복하는 최대 HP 비율.</summary>
    public const double PotionHealRatio = 0.35;

    /// <summary>마법 회복량 계수.</summary>
    public const double MagicHealScale = 1.6;

    public const double BuffMagnitude = 0.30;
    public const int BuffDuration = 3;
    public const int TauntDuration = 2;
    public const int ManaPerSpell = 8;

    /// <summary>
    /// 회피 확률의 상한.
    /// <para>
    /// 자동 전투에서 "빗나감"이 연달아 뜨면 답답합니다.
    /// 회피는 가끔 터지는 반전이어야지 일상이면 안 됩니다.
    /// </para>
    /// </summary>
    public const double MaxEvasionChance = 0.28;

    /// <summary>방어 태세일 때 회피 확률 보너스. 자세를 낮추면 피하기 쉽습니다.</summary>
    public const double DefendEvasionBonus = 0.10;

    /// <summary>광역 공격은 피하기 어렵습니다.</summary>
    public const double AreaEvasionPenalty = 0.5;

    private const double Variance = 0.2;

    /// <summary>
    /// 난수를 배제한 기댓값. AI가 "이번 턴에 죽일 수 있는가"를 판단할 때 씁니다.
    /// <para>회피와 치명타는 확률이므로 기댓값에는 넣지 않습니다 — AI는 정직하게 평균만 봅니다.</para>
    /// </summary>
    public static int ExpectedDamage(Combatant attacker, Combatant defender, bool area = false) =>
        ComputeDamage(attacker, defender, area, variance: 1.0, critMultiplier: 1.0);

    /// <summary>
    /// 실제 공격을 한 번 해결합니다. 회피 → 치명타 → 피해 순으로 판정합니다.
    /// </summary>
    public static AttackResult ResolveAttack(
        Combatant attacker,
        Combatant defender,
        IRandomSource rng,
        bool area = false)
    {
        if (RollEvasion(attacker, defender, area, rng))
        {
            return new AttackResult(0, Evaded: true, Critical: false);
        }

        bool critical = rng.Chance(attacker.CritChance);
        double critMultiplier = critical ? attacker.Capability.CritMultiplier : 1.0;

        double variance = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * Variance;
        int damage = ComputeDamage(attacker, defender, area, variance, critMultiplier);

        return new AttackResult(damage, Evaded: false, Critical: critical);
    }

    /// <summary>
    /// 회피 판정.
    /// <para>
    /// 절대 회피율이 아니라 <b>속도 차이</b>가 좌우합니다.
    /// 느린 석궁병이 재빠른 검객을 맞히기 어려운 게 자연스럽습니다.
    /// </para>
    /// </summary>
    private static bool RollEvasion(Combatant attacker, Combatant defender, bool area, IRandomSource rng)
    {
        double chance = defender.EvasionChance;

        // 상대보다 빠르면 더 잘 피하고, 느리면 덜 피합니다.
        double speedRatio = defender.EffectiveSpeed / Math.Max(1.0, attacker.EffectiveSpeed);
        chance *= Math.Clamp(speedRatio, 0.5, 1.8);

        if (defender.IsDefending) chance += DefendEvasionBonus;
        if (area) chance *= AreaEvasionPenalty;

        // 후열에서 근접 무기를 휘두르면 제대로 닿지 않으니 더 잘 피합니다.
        if (attacker.Row == Row.Back && !attacker.Capability.CanActFromBackRow) chance *= 1.4;

        return rng.Chance(Math.Clamp(chance, 0.0, MaxEvasionChance));
    }

    private static int ComputeDamage(
        Combatant attacker,
        Combatant defender,
        bool area,
        double variance,
        double critMultiplier)
    {
        bool magic = attacker.Capability.UsesMagic;

        double offense = attacker.EffectiveOffense;
        double guard = magic ? defender.EffectiveMagicGuard : defender.EffectivePhysicalGuard;

        double raw = offense - guard * 0.5;

        raw *= attacker.Capability.DamageModifier;
        raw *= attacker.WeaponEffectiveness;
        raw *= critMultiplier;

        // 근접 무기가 후열에서 휘두르면 제대로 닿지 않습니다.
        if (attacker.Row == Row.Back && !attacker.Capability.CanActFromBackRow)
        {
            raw *= MeleeFromBackRowPenalty;
        }

        if (area) raw *= AreaAttackMultiplier;
        if (defender.Row == Row.Back) raw *= BackRowDefenseBonus;
        if (defender.IsDefending) raw *= DefendMultiplier;

        return Math.Max(1, (int)Math.Round(raw * variance));
    }

    public static int PotionHealAmount(Combatant target) =>
        Math.Max(1, (int)Math.Round(target.MaxHp * PotionHealRatio));

    public static int MagicHealAmount(Combatant healer) =>
        Math.Max(1, (int)Math.Round(healer.EffectiveMagicPower * MagicHealScale * healer.WeaponEffectiveness));

    public static int PoisonDamage(Combatant victim) =>
        Math.Max(1, (int)Math.Round(victim.MaxHp * 0.05));
}
