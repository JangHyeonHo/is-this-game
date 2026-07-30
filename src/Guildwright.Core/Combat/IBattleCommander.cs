namespace Guildwright.Core.Combat;

/// <summary>
/// 플레이어의 전투 개입 요청.
/// </summary>
/// <param name="Action">대신 시킬 행동.</param>
/// <param name="Target">대상. 필요 없으면 null.</param>
public readonly record struct CommandOrder(TacticAction Action, Combatant? Target);

/// <summary>
/// 전투 중 플레이어가 끼어들 수 있게 하는 통로.
/// <para>
/// <b>이게 있어야 자동 전투가 "지켜보기만 하는 것"이 아니게 됩니다.</b>
/// </para>
/// <para>
/// ⚠️ <b>개입에 횟수 제한은 없습니다.</b> 예전에는 전투당 지휘 포인트 3점을 두고
/// 위치 변경에 2점을 매겼는데, 전투가 3~4라운드라 사실상 한두 번밖에 못 썼고
/// 아끼려다 결국 안 쓰게 됐습니다. 실제 플레이 피드백이
/// <b>"개입으로 할 수 있는 게 없다"</b>였는데 원인이 기능 부재가 아니라 이 제한이었습니다.
/// </para>
/// <para>
/// 대신 <b>유일한 제약은 지시가 통하지 않는 상태</b>입니다 —
/// 공포나 혼란에 걸린 아군은 말을 듣지 않습니다
/// (<see cref="Combatant.AcceptsOrders"/>). 마비·빙결·석화는 지시를 듣고도
/// 몸이 안 움직이는 경우라 성격이 다릅니다.
/// </para>
/// <para>
/// 배치 시뮬레이션에서는 이걸 넘기지 않습니다(null). 그러면 완전 자동으로 돌아갑니다.
/// </para>
/// 근거: docs/07-decisions.md §14, §18.7
/// </summary>
public interface IBattleCommander
{
    /// <summary>이 팀의 행동에만 개입할 수 있습니다.</summary>
    Team Team { get; }

    /// <summary>
    /// AI가 정한 행동을 보여주고, 바꿀지 묻습니다.
    /// </summary>
    /// <param name="actor">행동할 전투원.</param>
    /// <param name="aiChoice">AI가 고른 행동.</param>
    /// <param name="state">현재 전황.</param>
    /// <returns>바꿀 행동. null이면 AI 판단을 그대로 따릅니다.</returns>
    CommandOrder? Intervene(Combatant actor, ChosenAction aiChoice, BattleState state);
}
