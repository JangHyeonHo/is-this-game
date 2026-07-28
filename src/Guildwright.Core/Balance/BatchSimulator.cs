using Guildwright.Core.Combat;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Balance;

/// <param name="Trials">시행 횟수.</param>
/// <param name="PlayerWins">플레이어 팀 승리 수.</param>
/// <param name="EnemyWins">적 팀 승리 수.</param>
/// <param name="Draws">무승부 수.</param>
/// <param name="AverageRounds">평균 소요 라운드.</param>
public sealed record BatchResult(
    int Trials,
    int PlayerWins,
    int EnemyWins,
    int Draws,
    double AverageRounds)
{
    public double PlayerWinRate => Trials == 0 ? 0.0 : (double)PlayerWins / Trials;

    public override string ToString() =>
        $"{Trials}회 · 승률 {PlayerWinRate:P1} (승 {PlayerWins} / 패 {EnemyWins} / 무 {Draws}) · 평균 {AverageRounds:F1}라운드";
}

/// <summary>
/// 같은 조건의 전투를 반복 실행해 승률 분포를 냅니다.
/// <para>
/// <b>이 게임의 밸런싱은 감이 아니라 이걸로 합니다.</b> 자동 전투 게임에서 사람이
/// 손으로 수십 판 돌려 판단하는 것은 표본이 너무 적습니다.
/// 이게 가능한 이유는 코어가 엔진에 의존하지 않고 결정론적이기 때문입니다.
/// </para>
/// </summary>
public static class BatchSimulator
{
    /// <param name="trials">시행 횟수.</param>
    /// <param name="seed">기준 시드. 같은 시드는 항상 같은 결과를 냅니다.</param>
    /// <param name="createBattle">
    /// 매 시행마다 새 <see cref="BattleState"/>를 만드는 함수.
    /// 전투는 전투원 상태를 변경하므로 반드시 매번 새로 만들어야 합니다.
    /// </param>
    /// <param name="resolver">사용할 해석기. 생략 시 기본값.</param>
    public static BatchResult Run(
        int trials,
        ulong seed,
        Func<int, BattleState> createBattle,
        BattleResolver? resolver = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trials);

        resolver ??= new BattleResolver();

        int playerWins = 0, enemyWins = 0, draws = 0;
        long totalRounds = 0;

        for (int i = 0; i < trials; i++)
        {
            // 시행마다 독립 스트림을 씁니다. 한 시행의 난수 소비량이 달라져도
            // 다른 시행의 결과가 흔들리지 않습니다.
            var rng = new DeterministicRandom(seed).Fork($"trial:{i}");
            var result = resolver.Resolve(createBattle(i), rng);

            switch (result.Outcome)
            {
                case BattleOutcome.PlayerVictory: playerWins++; break;
                case BattleOutcome.EnemyVictory: enemyWins++; break;
                default: draws++; break;
            }

            totalRounds += result.Rounds;
        }

        return new BatchResult(trials, playerWins, enemyWins, draws, (double)totalRounds / trials);
    }
}
