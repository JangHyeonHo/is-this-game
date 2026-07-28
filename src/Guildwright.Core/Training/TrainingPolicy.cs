using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Training;

/// <summary>
/// 훈련 방침. 플레이어가 매달 직접 고르는 대신 맡길 수 있습니다.
/// <para>
/// <b>이 시스템은 선택 사항이 아니라 필수입니다.</b> 길드에 모험가가 여럿이면
/// 12개월 × 인원 × 연차만큼 클릭이 생기는데, 그걸 전부 손으로 시키면 게임이 노동이 됩니다.
/// 애착 있는 캐릭터는 손으로, 나머지는 방침을 맡기고 넘기게 합니다.
/// </para>
/// <para>
/// 그리고 배치 시뮬레이션에는 자동 경로가 반드시 필요합니다 — 사람이 10,000번 클릭할 수는 없습니다.
/// </para>
/// </summary>
/// <param name="Priorities">우선순위 순서로 순환하며 훈련할 능력치.</param>
/// <param name="RestFatigueThreshold">이 피로도 이상이면 쉽니다.</param>
/// <param name="Name">표시용 이름.</param>
/// <param name="OpportunisticBonus">
/// 컨디션이 양호 이상일 때 <paramref name="RestFatigueThreshold"/>를 얼마나 올릴지.
/// <para>
/// <b>이게 이 게임 육성의 실제 실력 축입니다.</b> 무모하게 밀어붙이는 것(높은 기본 임계값)은
/// 어느 방향으로도 이득이 아니지만, <b>절호조인 달을 놓치지 않는 것</b>은 확실한 이득입니다.
/// 컨디션 배율이 1.30인데 그 달에 쉬면 그냥 버리는 셈이기 때문입니다.
/// </para>
/// </param>
public sealed record TrainingPolicy(
    IReadOnlyList<TrainingFocus> Priorities,
    int RestFatigueThreshold,
    string Name,
    int OpportunisticBonus = 0)
{
    /// <summary>7 능력치를 골고루. 특화가 없는 대신 안전합니다.</summary>
    public static TrainingPolicy Balanced { get; } = new(
    [
        TrainingFocus.Vitality, TrainingFocus.Attack, TrainingFocus.Defense, TrainingFocus.Speed,
        TrainingFocus.Mana, TrainingFocus.MagicAttack, TrainingFocus.MagicDefense
    ], RestFatigueThreshold: 48, "균형");

    /// <summary>전위형. 체력·공격·방어 중심.</summary>
    public static TrainingPolicy Vanguard { get; } = new(
        [TrainingFocus.Attack, TrainingFocus.Vitality, TrainingFocus.Defense],
        RestFatigueThreshold: 48, "전위");

    /// <summary>마법사형. 마력·마공 중심.</summary>
    public static TrainingPolicy Mage { get; } = new(
        [TrainingFocus.MagicAttack, TrainingFocus.Mana, TrainingFocus.MagicDefense],
        RestFatigueThreshold: 48, "마법");

    /// <summary>유격형. 속도·공격 중심.</summary>
    public static TrainingPolicy Skirmisher { get; } = new(
        [TrainingFocus.Speed, TrainingFocus.Attack, TrainingFocus.Vitality],
        RestFatigueThreshold: 48, "유격");

    /// <summary>무리하지 않는 방침. 성장은 느리지만 부상 위험이 사실상 0입니다.</summary>
    public TrainingPolicy Cautious() => this with { RestFatigueThreshold = 34, Name = $"{Name}(신중)" };

    /// <summary>
    /// 부상을 감수하고 밀어붙이는 방침.
    /// <para>
    /// ⚠️ 배치 시뮬레이션 기준 <b>기대 성장이 신중 방침보다 낮습니다.</b>
    /// 피로 페널티와 부상 손실이 늘어난 훈련 횟수보다 크기 때문입니다.
    /// 그럼에도 남겨두는 이유는 "지금 당장 이 아이를 써야 한다"는 상황이 존재하기 때문입니다.
    /// 기대값이 아니라 <b>시간</b>을 사는 선택지입니다. (docs/06-balance-log.md #8)
    /// </para>
    /// </summary>
    public TrainingPolicy Aggressive() => this with { RestFatigueThreshold = 72, Name = $"{Name}(강행)" };

    /// <summary>컨디션이 좋은 달을 놓치지 않는 방침. 평상시에는 신중합니다.</summary>
    public TrainingPolicy Opportunistic() =>
        this with { OpportunisticBonus = 22, Name = $"{Name}(호기포착)" };

    /// <summary>이번 달에 무엇을 할지 정합니다.</summary>
    public TrainingFocus ChooseFor(TrainingYearSession session)
    {
        if (Priorities.Count == 0) return TrainingFocus.Rest;

        // 컨디션이 좋은 달은 성장 배율이 높으므로, 피로를 조금 더 감수할 가치가 있습니다.
        int threshold = session.Condition >= Condition.Good
            ? RestFatigueThreshold + OpportunisticBonus
            : RestFatigueThreshold;

        if (session.Fatigue >= threshold) return TrainingFocus.Rest;

        // 우선순위를 순환합니다. 훈련한 달만 세어야 순환이 고르게 돕니다.
        int trained = session.Months.Count(m => m.Focus != TrainingFocus.Rest);
        return Priorities[trained % Priorities.Count];
    }
}

/// <summary>방침에 따라 훈련 1년을 자동으로 진행합니다.</summary>
public static class AutoTrainer
{
    public static YearRecord RunYear(
        Adventurer adventurer,
        TrainingPolicy policy,
        IRandomSource rng,
        Mentorship? mentorship = null,
        int startingFatigue = 0)
    {
        var session = new TrainingYearSession(adventurer, rng, mentorship, startingFatigue);

        while (!session.IsComplete)
        {
            session.AdvanceMonth(policy.ChooseFor(session));
        }

        return session.Complete();
    }
}
