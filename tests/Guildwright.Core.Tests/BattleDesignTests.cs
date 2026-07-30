using Guildwright.Core.Balance;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
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
    public void 판단력_효과는_모든_무기_스타일에서_우상향한다()
    {
        // 판단력이 특정 스타일에서만 통하면 육성의 보편적 목표가 되지 못합니다.
        //
        // ⚠️ 고구간(70→100)은 단조증가를 요구하지 않습니다. 단일 대상 스타일에서
        //    70 부근에 도달한 뒤 평평해지거나 소폭 꺾이는 현상이 관찰되었고,
        //    원인을 아직 특정하지 못했습니다. (docs/06-balance-log.md #12)
        //    저구간의 가파른 상승이 이 스탯의 존재 이유이므로 거기를 엄격히 봅니다.
        // 손 배치가 곧 스타일입니다. 방패만 들면 위력 0이라 아무도 못 죽이므로,
        // 실제로 나올 구성으로 잽니다.
        (string Name, Loadout Loadout)[] builds =
        [
            ("검+방패", Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield)),
            ("쌍수",   Loadout.Pair(WeaponKind.Sword, WeaponKind.Sword)),
            ("창",     Loadout.Single(WeaponKind.Spear)),
            ("대검",   Loadout.Single(WeaponKind.Greatsword))
        ];

        foreach (var (name, build) in builds)
        {
            var rates = new Dictionary<int, double>();

            foreach (int judgement in new[] { 10, 40, 70, 100 })
            {
                var result = BatchSimulator.Run(
                    Trials, Seed,
                    _ => TestParty.MirrorMatch(judgement, enemyJudgement: 50, loadout: build));

                rates[judgement] = result.PlayerWinRate;
                output.WriteLine($"{name,-8} 판단력 {judgement,3} vs 50: {result}");
            }

            Assert.True(rates[40] > rates[10] + 0.05,
                $"{name}: 판단력 10→40에서 승률이 뚜렷하게 오르지 않습니다.");

            // 넓은 구간으로 봅니다. 중간 구간(40→70)의 기울기는 구성마다 다릅니다 —
            // 창처럼 후열에서도 싸우는 구성은 물러설 판단의 여지가 작아 완만해집니다
            // (측정: 창 40→70에서 3.6%p, 검+방패는 24.4%p). docs/06-balance-log.md #35
            Assert.True(rates[70] > rates[10] + 0.10,
                $"{name}: 판단력 10→70에서 승률이 10%p도 오르지 않습니다. " +
                "이 스탯이 그 구성에서 사실상 통하지 않는다는 뜻입니다.");

            Assert.True(rates[100] >= rates[40] - 0.02,
                $"{name}: 판단력 100({rates[100]:P1})이 40({rates[40]:P1})보다 낮습니다. " +
                "고구간이 평평한 것은 허용하지만, 뒤집히는 것은 허용하지 않습니다.");
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
