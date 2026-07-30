using Guildwright.Core.Combat;

namespace Guildwright.Core.Careers;

/// <summary>
/// 그 해에 실제로 치른 전투가 어떻게 끝났는지, 그리고 이 사람이 그 안에서 쓰러졌는지.
/// <para>
/// 전투 시스템과 경력 시스템을 잇는 다리입니다. 이게 없으면 전투 결과와 연말 결산이
/// 따로 놀아서, <b>전멸하고도 보수를 받고 승급하는</b> 일이 생깁니다. 실제로 겪었습니다.
/// </para>
/// 근거: docs/08-balance-log.md #23
/// </summary>
/// <param name="Outcome">전투 결과.</param>
/// <param name="Downed">이 사람이 전투 중 쓰러졌는지. 죽지는 않았어도 몸에 남습니다.</param>
public sealed record BattleReport(BattleOutcome Outcome, bool Downed = false)
{
    /// <summary>전투를 따로 돌리지 않은 경우 (배치 시뮬레이션, 요약 진행).</summary>
    public static BattleReport NotFought { get; } = new(BattleOutcome.PlayerVictory);

    /// <summary>
    /// 파견 한 건의 결과를 연말 결산이 읽을 수 있는 형태로 옮깁니다.
    /// <para>
    /// <b>성공/실패 이분법이므로 무승부가 없습니다</b> (§17.4). 실패는 사유가 무엇이든
    /// 보수가 0이고 위험이 오릅니다 — 손절도 실패는 실패입니다 (§17.7).
    /// </para>
    /// </summary>
    /// <param name="result">파견 결과.</param>
    /// <param name="downed">이 사람이 그 파견에서 쓰러졌는지.</param>
    public static BattleReport From(DeploymentResult result, bool downed = false) =>
        new(result.Succeeded ? BattleOutcome.PlayerVictory : BattleOutcome.EnemyVictory, downed);

    public bool Failed => Outcome == BattleOutcome.EnemyVictory;

    public bool Inconclusive => Outcome == BattleOutcome.Draw;

    /// <summary>사고 위험 배율.</summary>
    public double RiskMultiplier
    {
        get
        {
            double risk = Outcome switch
            {
                BattleOutcome.EnemyVictory => CareerRules.DefeatRiskMultiplier,
                BattleOutcome.Draw => CareerRules.DrawRiskMultiplier,
                _ => 1.0
            };

            return Downed ? risk * CareerRules.DownedRiskMultiplier : risk;
        }
    }

    /// <summary>보수 비율. 패배하면 보수가 없습니다 — 의뢰를 못 해낸 것이니까.</summary>
    public double IncomeRatio => Outcome switch
    {
        BattleOutcome.EnemyVictory => 0.0,
        BattleOutcome.Draw => CareerRules.DrawIncomeRatio,
        _ => 1.0
    };
}
