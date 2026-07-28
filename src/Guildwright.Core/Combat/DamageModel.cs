using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

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

    /// <summary>
    /// 후열에 있을 때 받는 피해 배율.
    /// <para>후퇴에 실질적 이득이 있어야 포지션 판단이 의미를 갖습니다.</para>
    /// </summary>
    public const double BackRowDefenseBonus = 0.75;

    /// <summary>
    /// 후열에서 근접 공격을 할 때의 위력 배율.
    /// <para>
    /// 0으로 두지 않는 이유: 완전히 무력해지면 "일단 다 뒤로 빼기"가 불가능해지는 대신
    /// 후퇴 자체가 사실상 사망 선고가 되어 선택이 사라집니다. 아프되 가능해야 합니다.
    /// </para>
    /// </summary>
    public const double MeleeFromBackRowPenalty = 0.45;

    /// <summary>광역 공격의 대상당 위력 배율.</summary>
    public const double AreaAttackMultiplier = 0.55;

    /// <summary>회복약이 회복하는 최대 HP 비율.</summary>
    public const double PotionHealRatio = 0.35;

    /// <summary>마법 회복량 계수 (마공 기준).</summary>
    public const double MagicHealScale = 1.6;

    /// <summary>버프·디버프의 세기와 지속 라운드.</summary>
    public const double BuffMagnitude = 0.30;
    public const int BuffDuration = 3;
    public const int TauntDuration = 2;

    /// <summary>마법 행동 1회의 마나 소모.</summary>
    public const int ManaPerSpell = 8;

    private const double Variance = 0.2;

    /// <summary>난수를 배제한 기댓값. AI가 "이번 턴에 죽일 수 있는가"를 판단할 때 씁니다.</summary>
    public static int ExpectedDamage(Combatant attacker, Combatant defender, bool area = false) =>
        ComputeDamage(attacker, defender, area, variance: 1.0);

    public static int RollDamage(Combatant attacker, Combatant defender, IRandomSource rng, bool area = false)
    {
        double variance = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * Variance;
        return ComputeDamage(attacker, defender, area, variance);
    }

    private static int ComputeDamage(Combatant attacker, Combatant defender, bool area, double variance)
    {
        bool magic = attacker.Capability.UsesMagic;

        double offense = magic ? attacker.EffectiveMagicAttack : attacker.EffectiveAttack;
        double guard = magic ? defender.EffectiveMagicDefense : defender.EffectiveDefense;

        double raw = offense - guard * 0.5;

        raw *= attacker.Capability.DamageModifier;
        raw *= attacker.WeaponEffectiveness;

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
        Math.Max(1, (int)Math.Round(healer.EffectiveMagicAttack * MagicHealScale * healer.WeaponEffectiveness));

    public static int PoisonDamage(Combatant victim) =>
        Math.Max(1, (int)Math.Round(victim.MaxHp * 0.05));
}
