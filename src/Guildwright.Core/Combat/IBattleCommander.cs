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
/// 다만 개입은 <b>지휘 포인트</b>를 소모하므로 무한정 조작할 수 없습니다.
/// 개입권이 제한적이라 "언제 쓸까"가 매 전투의 판단 재미가 됩니다.
/// </para>
/// <para>
/// 배치 시뮬레이션에서는 이걸 넘기지 않습니다(null). 그러면 완전 자동으로 돌아갑니다.
/// </para>
/// 근거: docs/04-game-design.md §4.3
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
    /// <param name="commandPointsLeft">남은 지휘 포인트.</param>
    /// <returns>바꿀 행동. null이면 AI 판단을 그대로 따릅니다.</returns>
    CommandOrder? Intervene(Combatant actor, ChosenAction aiChoice, BattleState state, int commandPointsLeft);
}

public static class CommandRules
{
    /// <summary>전투당 기본 지휘 포인트.</summary>
    public const int BasePoints = 3;

    /// <summary>
    /// 개입 비용. 포지션 변경은 전열 구성을 통째로 바꾸므로 더 비쌉니다.
    /// </summary>
    public static int CostOf(TacticAction action) => action switch
    {
        TacticAction.MoveBack or TacticAction.MoveFront => 2,
        _ => 1
    };
}
