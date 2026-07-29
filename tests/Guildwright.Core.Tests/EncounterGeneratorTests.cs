using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 적 생성 규칙.
/// <para>
/// 예전에는 콘솔 안에서 <c>난이도/2 + 1</c>로 정했고 <b>파티 인원을 전혀 보지 않았습니다.</b>
/// 혼자 나간 신입이 난이도 2에서 적 2명을 상대하게 되어 승률 90% → 14%로 떨어졌고,
/// 실제 플레이에서 "시작하자마자 죽었다"로 나타났습니다.
/// </para>
/// 근거: docs/06-balance-log.md #33
/// </summary>
public class EncounterGeneratorTests(ITestOutputHelper output)
{
    [Fact]
    public void CountFor_적_수는_파티_인원을_따라간다()
    {
        // 난이도가 올라도 인원이 그대로면 적 수도 그대로여야 합니다.
        // 난이도는 적의 '강함'으로만 나타냅니다.
        Assert.Equal(1, EncounterGenerator.CountFor(partySize: 1, difficulty: 1));
        Assert.Equal(1, EncounterGenerator.CountFor(partySize: 1, difficulty: 2));
        Assert.Equal(1, EncounterGenerator.CountFor(partySize: 1, difficulty: 5));

        Assert.Equal(3, EncounterGenerator.CountFor(partySize: 3, difficulty: 1));
        Assert.Equal(3, EncounterGenerator.CountFor(partySize: 3, difficulty: 5));
    }

    [Fact]
    public void CountFor_높은_난이도에서만_수적_열세가_걸린다()
    {
        Assert.Equal(2, EncounterGenerator.CountFor(partySize: 1, difficulty: EncounterGenerator.OutnumberedFrom));
        Assert.Equal(4, EncounterGenerator.CountFor(partySize: 4, difficulty: 1));
        Assert.Equal(4, EncounterGenerator.CountFor(partySize: 4, difficulty: 10));   // 상한
    }

    [Fact]
    public void Generate_난이도가_오르면_적이_강해진다()
    {
        int TotalAt(int difficulty)
        {
            var foes = EncounterGenerator.Generate(
                difficulty, partySize: 1, new DeterministicRandom(77), _ => "적");

            return foes.Sum(f => f.Stats.Total);
        }

        int weak = TotalAt(1);
        int strong = TotalAt(5);

        output.WriteLine($"난이도 1 적 총합 {weak} · 난이도 5 적 총합 {strong}");
        Assert.True(strong > weak * 1.3,
            "난이도를 올렸는데 적이 뚜렷하게 강해지지 않으면 난이도가 의미가 없습니다.");
    }

    [Fact]
    public void 혼자_나간_신입이_낮은_난이도에서_대체로_이긴다()
    {
        // 1년 육성한 신입이 견습 등급으로 받을 수 있는 난이도(1~2)에서
        // 승산이 있어야 합니다. 이게 무너지면 게임 시작 직후가 곧 사망입니다.
        foreach (int difficulty in new[] { 1, 2 })
        {
            int wins = 0;
            const int Trials = 300;

            for (int t = 0; t < Trials; t++)
            {
                var root = new DeterministicRandom((ulong)(t * 131 + difficulty));

                var hero = Adventurer.Recruit("H", "신입", root.Fork($"h:{t}"));
                CareerSimulator.ResolveTrainingYear(hero, root.Fork($"ht:{t}"));

                var foes = EncounterGenerator.Generate(
                    difficulty, partySize: 1, root.Fork($"enc:{t}"), _ => "적");

                var state = CombatantFactory.FormParty([hero], foes.ToList());
                var result = new BattleResolver().Resolve(state, root.Fork($"b:{t}"));

                if (result.Outcome == BattleOutcome.PlayerVictory) wins++;
            }

            double rate = (double)wins / Trials;
            output.WriteLine($"난이도 {difficulty} — 1년 육성 신입 단독 승률 {rate:P0}");

            Assert.True(rate > 0.5,
                $"난이도 {difficulty}에서 승률이 {rate:P0}입니다. " +
                "첫 의뢰가 사실상 사형선고면 플레이어가 게임을 판단할 기회를 못 얻습니다.");
        }
    }
}
