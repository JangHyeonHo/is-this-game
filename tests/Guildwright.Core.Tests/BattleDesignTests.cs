using Guildwright.Core.Balance;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// <b>이 게임의 핵심 설계 가설을 검증하는 테스트입니다.</b>
/// <para>
/// 가설: "육성으로 올린 판단력과 플레이어가 편성한 전술 규칙이 전투 결과를 실제로 바꾼다."
/// 이게 성립하지 않으면 육성이 전투에서 보이지 않고, 게임의 핵심 루프가 무너집니다.
/// </para>
/// 근거: docs/04-game-design.md §4
/// </summary>
public class BattleDesignTests(ITestOutputHelper output)
{
    private const int Trials = 2_000;
    private const ulong Seed = 4242UL;

    [Fact]
    public void 같은시드_같은구성이면_전투결과가_완전히_동일하다()
    {
        var resolver = new BattleResolver();

        var first = resolver.Resolve(
            TestParty.MirrorMatch(playerJudgement: 60, enemyJudgement: 60),
            new DeterministicRandom(Seed));

        var second = resolver.Resolve(
            TestParty.MirrorMatch(playerJudgement: 60, enemyJudgement: 60),
            new DeterministicRandom(Seed));

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Rounds, second.Rounds);
    }

    [Fact]
    public void 능력치가_같으면_승률이_대략_5할이다()
    {
        // 이 테스트는 기준선입니다. 여기가 5할이 아니면
        // 턴 순서나 데미지 계산에 한쪽에 유리한 구조적 편향이 있다는 뜻입니다.
        var result = BatchSimulator.Run(
            Trials, Seed,
            _ => TestParty.MirrorMatch(playerJudgement: 60, enemyJudgement: 60));

        output.WriteLine($"동일 조건: {result}");

        Assert.InRange(result.PlayerWinRate, 0.45, 0.55);
    }

    [Fact]
    public void 판단력이_높으면_승률이_유의하게_높다()
    {
        // ★ 이 게임에서 가장 중요한 테스트.
        // 능력치는 완전히 동일하고 판단력만 다릅니다.
        // 승률 차이는 오직 의사결정 품질에서만 나옵니다.
        var result = BatchSimulator.Run(
            Trials, Seed,
            _ => TestParty.MirrorMatch(playerJudgement: 95, enemyJudgement: 15));

        output.WriteLine($"판단력 95 vs 15: {result}");

        Assert.True(
            result.PlayerWinRate > 0.58,
            $"판단력 차이가 승률에 반영되지 않았습니다 (승률 {result.PlayerWinRate:P1}). " +
            "판단력이 전투에서 체감되지 않으면 육성의 의미가 사라집니다.");
    }

    [Fact]
    public void 판단력_효과는_단조증가한다()
    {
        // 판단력이 올라갈수록 승률이 올라야 합니다. 중간에 꺾이면 밸런스 곡선이 잘못된 것입니다.
        var winRates = new List<(int Judgement, double WinRate)>();

        foreach (int judgement in new[] { 10, 40, 70, 100 })
        {
            var result = BatchSimulator.Run(
                Trials, Seed,
                _ => TestParty.MirrorMatch(playerJudgement: judgement, enemyJudgement: 50));

            winRates.Add((judgement, result.PlayerWinRate));
            output.WriteLine($"판단력 {judgement,3} vs 50: {result}");
        }

        for (int i = 1; i < winRates.Count; i++)
        {
            Assert.True(
                winRates[i].WinRate >= winRates[i - 1].WinRate - 0.02,
                $"판단력 {winRates[i].Judgement}의 승률({winRates[i].WinRate:P1})이 " +
                $"{winRates[i - 1].Judgement}({winRates[i - 1].WinRate:P1})보다 낮습니다.");
        }
    }

    [Fact]
    public void 좋은_전술규칙은_승률을_높인다()
    {
        // 플레이어의 편성(빌드)이 결과를 바꾸는지 확인합니다.
        // 판단력은 양쪽 동일 — 차이는 오직 전술 규칙뿐입니다.
        var result = BatchSimulator.Run(
            Trials, Seed,
            _ => TestParty.MirrorMatch(
                playerJudgement: 80,
                enemyJudgement: 80,
                playerTactics: TestParty.SensibleTactics,
                enemyTactics: TestParty.NaiveTactics));

        output.WriteLine($"좋은 전술 vs 단순 전술 (판단력 동일 80): {result}");

        Assert.True(
            result.PlayerWinRate > 0.55,
            $"전술 규칙 편성이 결과에 영향을 주지 않았습니다 (승률 {result.PlayerWinRate:P1}). " +
            "파티 구성이 메인인 게임에서 빌드가 무의미하다는 뜻이 됩니다.");
    }

    [Fact]
    public void 판단력이_낮으면_전술규칙의_이점이_줄어든다()
    {
        // 설계 의도: 규칙을 잘 짜도 판단력이 낮으면 제대로 실행하지 못합니다.
        // 이래야 "규칙 편성"과 "캐릭터 육성"이 둘 다 필요해집니다.
        var withHighJudgement = BatchSimulator.Run(
            Trials, Seed,
            _ => TestParty.MirrorMatch(90, 90, TestParty.SensibleTactics, TestParty.NaiveTactics));

        var withLowJudgement = BatchSimulator.Run(
            Trials, Seed,
            _ => TestParty.MirrorMatch(20, 20, TestParty.SensibleTactics, TestParty.NaiveTactics));

        output.WriteLine($"판단력 90 양쪽, 전술 우위: {withHighJudgement}");
        output.WriteLine($"판단력 20 양쪽, 전술 우위: {withLowJudgement}");

        Assert.True(
            withHighJudgement.PlayerWinRate > withLowJudgement.PlayerWinRate,
            "판단력이 낮을 때도 전술 이점이 그대로라면, 판단력 육성의 동기가 약해집니다.");
    }

    [Fact]
    public void 전투는_라운드제한안에_대부분_끝난다()
    {
        // 무승부가 잦으면 플레이어가 결과를 기다리는 시간이 낭비됩니다.
        var result = BatchSimulator.Run(
            Trials, Seed,
            _ => TestParty.MirrorMatch(playerJudgement: 60, enemyJudgement: 60));

        output.WriteLine($"무승부 {result.Draws}/{result.Trials}, 평균 {result.AverageRounds:F1}라운드");

        Assert.True(
            result.Draws < result.Trials * 0.05,
            $"무승부가 너무 많습니다 ({result.Draws}/{result.Trials}). 데미지 대비 HP가 과다할 수 있습니다.");
    }
}
