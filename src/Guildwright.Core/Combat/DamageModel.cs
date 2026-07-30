using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

/// <summary>한 번의 공격이 어떻게 끝났는지.</summary>
/// <param name="Damage">실제로 들어간 피해. 회피했으면 0.</param>
/// <param name="Evaded">회피당했는지.</param>
/// <param name="Critical">치명타였는지.</param>
/// <param name="Detail">
/// 계산 과정. <c>explain</c>을 켰을 때만 채워집니다.
/// <para>
/// <b>숫자가 어디서 왔는지 보이지 않으면 전술 판단이 감이 됩니다.</b>
/// 27 피해가 왜 27인지 알 수 없으면 "후열로 뺄까"를 계산할 수 없습니다.
/// </para>
/// </param>
public readonly record struct AttackResult(int Damage, bool Evaded, bool Critical, string? Detail = null);

/// <summary>
/// 데미지·회복 계산.
/// <para>
/// ⚠️ 여기 있는 수치는 <b>전부 임시값</b>입니다. 배치 시뮬레이션으로 검증한 뒤
/// 데이터 파일로 분리할 예정입니다. 감으로 고치지 말고 근거를 docs/08-balance-log.md에 남기세요.
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

    /// <summary>
    /// 치명타 기본 배율. <b>숙련 패시브가 여기에 더합니다</b> — 무기가 아닙니다.
    /// </summary>
    public const double BaseCritMultiplier = 1.6;

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
    /// <param name="attacker">공격자.</param>
    /// <param name="defender">대상.</param>
    /// <param name="rng">난수원.</param>
    /// <param name="area">광역 공격인지.</param>
    /// <param name="explain">
    /// 계산 과정을 <see cref="AttackResult.Detail"/>에 남길지.
    /// <b>난수 소비 순서와 결과는 이 값과 무관하게 완전히 동일합니다.</b>
    /// (달라지면 배치 시뮬레이션으로 잰 밸런스가 실제 플레이와 어긋납니다.)
    /// </param>
    public static AttackResult ResolveAttack(
        Combatant attacker,
        Combatant defender,
        IRandomSource rng,
        bool area = false,
        bool explain = false)
    {
        double evasionChance = EvasionChanceOf(attacker, defender, area);

        if (rng.Chance(evasionChance))
        {
            return new AttackResult(0, Evaded: true, Critical: false,
                Detail: explain ? $"회피 판정 {evasionChance * 100:F1}% 성공 — 빗나감" : null);
        }

        bool critical = rng.Chance(attacker.CritChance);
        double critMultiplier = critical ? attacker.CritMultiplier : 1.0;

        double variance = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * Variance;

        var steps = explain ? new List<string>() : null;
        int damage = ComputeDamage(attacker, defender, area, variance, critMultiplier, steps);

        string? detail = null;
        if (steps is not null)
        {
            detail =
                $"회피 {evasionChance * 100:F1}% 실패 · " +
                $"치명타 {attacker.CritChance * 100:F1}% {(critical ? "적중" : "실패")}\n" +
                string.Join(" ", steps) + $"  ⇒ {damage} 피해";
        }

        return new AttackResult(damage, Evaded: false, Critical: critical, detail);
    }

    /// <summary>
    /// 회피 판정.
    /// <para>
    /// 절대 회피율이 아니라 <b>속도 차이</b>가 좌우합니다.
    /// 느린 석궁병이 재빠른 검객을 맞히기 어려운 게 자연스럽습니다.
    /// </para>
    /// </summary>
    /// <summary>회피 확률만 계산합니다 (굴리지는 않습니다). 화면에 그대로 보여줄 수 있습니다.</summary>
    public static double EvasionChanceOf(Combatant attacker, Combatant defender, bool area = false)
    {
        double chance = defender.EvasionChance;

        // 상대보다 빠르면 더 잘 피하고, 느리면 덜 피합니다.
        double speedRatio = defender.EffectiveSpeed / Math.Max(1.0, attacker.EffectiveSpeed);
        chance *= Math.Clamp(speedRatio, 0.5, 1.8);

        if (defender.IsDefending) chance += DefendEvasionBonus;
        if (area) chance *= AreaEvasionPenalty;

        // 후열에서 근접 무기를 휘두르면 제대로 닿지 않으니 더 잘 피합니다.
        if (attacker.Row == Row.Back && !attacker.CanActFromBackRow) chance *= 1.4;

        return Math.Clamp(chance, 0.0, MaxEvasionChance);
    }

    private static int ComputeDamage(
        Combatant attacker,
        Combatant defender,
        bool area,
        double variance,
        double critMultiplier,
        List<string>? steps = null)
    {
        bool magic = attacker.UsesMagicPower;

        double offense = attacker.EffectiveOffense;
        double guard = magic ? defender.EffectiveMagicGuard : defender.EffectivePhysicalGuard;

        double raw = offense - guard * 0.5;
        steps?.Add($"{(magic ? "마법" : "물리")}위력 {offense} − 방어 {guard}×0.5 = {raw:F1}");

        raw = Step(raw, attacker.Loadout.Power, "무기", steps);
        raw = Step(raw, attacker.WeaponEffectiveness, "숙련", steps);
        raw = Step(raw, critMultiplier, "치명타", steps);

        // 근접 무기가 후열에서 휘두르면 제대로 닿지 않습니다.
        if (attacker.Row == Row.Back && !attacker.CanActFromBackRow)
        {
            raw = Step(raw, MeleeFromBackRowPenalty, "후열에서 근접", steps);
        }

        if (area) raw = Step(raw, AreaAttackMultiplier, "광역", steps);
        if (defender.Row == Row.Back) raw = Step(raw, BackRowDefenseBonus, "대상 후열", steps);
        if (defender.IsDefending) raw = Step(raw, DefendMultiplier, "대상 방어", steps);

        raw = Step(raw, variance, "변동", steps);

        return Math.Max(1, (int)Math.Round(raw));
    }

    /// <summary>배율을 한 번 적용하고, 설명이 필요하면 그 단계를 기록합니다.</summary>
    private static double Step(double value, double multiplier, string label, List<string>? steps)
    {
        double next = value * multiplier;

        // 배율 1.0은 아무것도 안 한 것이므로 굳이 줄을 늘리지 않습니다.
        if (steps is not null && Math.Abs(multiplier - 1.0) > 0.0005)
        {
            steps.Add($"→ {label} ×{multiplier:F2} = {next:F1}");
        }

        return next;
    }

    public static int PotionHealAmount(Combatant target) =>
        Math.Max(1, (int)Math.Round(target.MaxHp * PotionHealRatio));

    public static int MagicHealAmount(Combatant healer) =>
        Math.Max(1, (int)Math.Round(healer.EffectiveMagicPower * MagicHealScale * healer.WeaponEffectiveness));

    /// <summary>
    /// 지속 피해 한 번. <b>세기 × 스택</b>이 최대 HP 비율로 들어갑니다.
    /// <para>
    /// 화상은 세기가 크고 안 쌓이며, 중독은 작지만 쌓이고, 출혈은 행동할 때마다 쌓입니다.
    /// 그 차이가 전부 <see cref="StatusEffect"/>의 설정에서 나옵니다.
    /// </para>
    /// </summary>
    public static int OverTimeDamage(Combatant victim, StatusEffect effect)
    {
        // 스택이 피해를 키우는지는 이름이 아니라 표의 한 칸입니다 — 동상은 스택을
        // 쌓지만 피해는 안 커집니다(스택은 빙결로 넘어가는 임계에만 쓰입니다).
        int stacks = StatusEffects.ProfileOf(effect.Name).StacksScaleDamage ? effect.Stacks : 1;

        double ratio = StatusEffects.DamageOverTimeScale * effect.Magnitude * stacks;
        return Math.Max(1, (int)Math.Round(victim.MaxHp * ratio));
    }
}
