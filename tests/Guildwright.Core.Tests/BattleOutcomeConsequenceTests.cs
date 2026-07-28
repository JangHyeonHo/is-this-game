using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 전투 결과가 그 해의 결산에 실제로 반영되는지 확인합니다.
/// <para>
/// 콘솔을 직접 돌려보다가 <b>전투에서 쓰러진 캐릭터가 보수 144를 받고 승급하는</b> 장면을 봤습니다.
/// 전투와 연말 결산이 서로 모르는 상태였기 때문입니다. 그 순간 전투를 보는 의미가 사라집니다.
/// </para>
/// 근거: docs/06-balance-log.md #23
/// </summary>
public class BattleOutcomeConsequenceTests
{
    /// <param name="years">훈련 연차. 많이 시킬수록 난이도 대비 사고 확률이 낮아집니다.</param>
    private static Adventurer Veteran(ulong seed = 7, int years = 1)
    {
        var a = Adventurer.Recruit("T", "테스트", new DeterministicRandom(seed));
        for (int y = 0; y < years; y++)
        {
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(seed + (ulong)y + 1));
        }
        return a;
    }

    /// <summary>보수를 비교하려면 먼저 사고 없이 돌아와야 합니다.</summary>
    private static YearRecord SafeDeployment(BattleReport battle)
    {
        var a = Veteran(years: 6);
        var record = CareerSimulator.ResolveDeploymentYear(
            a, difficulty: 2, new DeterministicRandom(100), battle: battle);

        Assert.Equal(DeploymentOutcome.Unharmed, record.Outcome);   // 전제 확인
        return record;
    }

    [Fact]
    public void ResolveDeploymentYear_전투에서_지면_보수가_없다()
    {
        var record = SafeDeployment(new BattleReport(BattleOutcome.EnemyVictory));

        Assert.Equal(0, record.Income);
        Assert.Contains("실패", record.Note);
    }

    [Fact]
    public void ResolveDeploymentYear_이기면_보수가_있다()
    {
        var record = SafeDeployment(new BattleReport(BattleOutcome.PlayerVictory));

        Assert.True(record.Income > 0, "이겼는데 보수가 0입니다.");
    }

    [Fact]
    public void ResolveDeploymentYear_무승부는_이긴_경우보다_보수가_적다()
    {
        int won = SafeDeployment(new BattleReport(BattleOutcome.PlayerVictory)).Income;
        int drew = SafeDeployment(new BattleReport(BattleOutcome.Draw)).Income;

        Assert.True(drew < won, $"무승부 {drew} < 승리 {won} 이어야 합니다.");
        Assert.True(drew > 0, "무승부인데 보수가 아예 없으면 승패 구분이 사라집니다.");
    }

    [Fact]
    public void ResolveDeploymentYear_전투를_생략하면_이긴_것과_같게_처리된다()
    {
        // 배치 시뮬레이션은 전투를 따로 돌리지 않고 요약으로 진행합니다.
        // 이 경로가 갑자기 불리해지면 그동안 잰 밸런스 수치가 전부 어긋납니다.
        var withoutBattle = CareerSimulator.ResolveDeploymentYear(
            Veteran(years: 6), 2, new DeterministicRandom(100));

        var withVictory = SafeDeployment(new BattleReport(BattleOutcome.PlayerVictory));

        Assert.Equal(withVictory.Income, withoutBattle.Income);
        Assert.Equal(withVictory.Outcome, withoutBattle.Outcome);
    }

    [Fact]
    public void ResolveDeploymentYear_패배하면_사고율이_눈에_띄게_오른다()
    {
        // 개별 판정은 난수이므로 분포로 확인합니다.
        double wonMishaps = MishapRate(new BattleReport(BattleOutcome.PlayerVictory));
        double lostMishaps = MishapRate(new BattleReport(BattleOutcome.EnemyVictory, Downed: true));

        Assert.True(lostMishaps > wonMishaps * 1.5,
            $"패배+전투불능 사고율 {lostMishaps:P1} 이 승리 {wonMishaps:P1} 보다 충분히 높지 않습니다.");
    }

    private static double MishapRate(BattleReport battle)
    {
        const int Trials = 2_000;
        int mishaps = 0;

        for (int i = 0; i < Trials; i++)
        {
            // 난이도를 실력보다 높게 잡아야 기본 사고율이 바닥에 깔리지 않아 차이가 보입니다.
            var a = Veteran((ulong)(i + 1));
            var record = CareerSimulator.ResolveDeploymentYear(
                a, difficulty: 4, new DeterministicRandom((ulong)(i * 31 + 7)), battle: battle);

            if (record.Outcome != DeploymentOutcome.Unharmed) mishaps++;
        }

        return (double)mishaps / Trials;
    }
}
