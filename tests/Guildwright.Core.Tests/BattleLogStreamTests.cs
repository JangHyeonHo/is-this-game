using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 전투 기록을 실시간으로 흘려보내는 기능이 전투 결과를 바꾸지 않는지 확인합니다.
/// <para>
/// 수동 전투에서는 지시를 내리기 전에 직전 라운드를 봐야 하므로 기록을 즉시 출력합니다.
/// 하지만 <b>보고 있다는 사실이 결과를 바꾸면</b> 배치 시뮬레이션으로 잰 승률이
/// 실제 플레이와 달라집니다. 그래서 관전 여부는 순수하게 출력 문제여야 합니다.
/// </para>
/// </summary>
public class BattleLogStreamTests
{
    [Fact]
    public void Resolve_관전콜백을_넣어도_결과와_기록이_동일하다()
    {
        var silent = new BattleResolver(recordLog: true)
            .Resolve(TestParty.MirrorMatch(70, 70), new DeterministicRandom(4242));

        var watched = new List<string>();
        var streamed = new BattleResolver(recordLog: true)
            .Resolve(TestParty.MirrorMatch(70, 70), new DeterministicRandom(4242),
                     commander: null, onLine: watched.Add);

        Assert.Equal(silent.Outcome, streamed.Outcome);
        Assert.Equal(silent.Rounds, streamed.Rounds);
        Assert.Equal(silent.Log, streamed.Log);
        Assert.Equal(silent.Log, watched);
    }

    [Fact]
    public void Resolve_관전콜백은_기록이_생기는_즉시_호출된다()
    {
        // 전투가 끝난 뒤 한꺼번에 부르는 게 아니라, 진행 중에 이미 여러 줄이 와 있어야 합니다.
        // 첫 줄은 "--- 1라운드 ---"이므로, 라운드 표시 다음 줄이 오기 전에는 아직 전투 중입니다.
        int linesSeenBeforeFirstAction = -1;
        var seen = new List<string>();

        new BattleResolver(recordLog: true).Resolve(
            TestParty.MirrorMatch(70, 70),
            new DeterministicRandom(4242),
            commander: null,
            onLine: line =>
            {
                seen.Add(line);
                if (linesSeenBeforeFirstAction < 0 && !line.StartsWith("---"))
                {
                    linesSeenBeforeFirstAction = seen.Count;
                }
            });

        Assert.Equal(2, linesSeenBeforeFirstAction);   // 라운드 표시 + 첫 행동
        Assert.True(seen.Count > linesSeenBeforeFirstAction, "전투가 끝나기 전에 이미 줄이 흘렀어야 합니다.");
    }

    [Fact]
    public void Resolve_기록을_끄면_콜백도_호출되지_않는다()
    {
        int calls = 0;

        new BattleResolver(recordLog: false).Resolve(
            TestParty.MirrorMatch(70, 70), new DeterministicRandom(4242),
            commander: null, onLine: _ => calls++);

        // 배치 시뮬레이션은 기록을 끄고 돕니다. 그때 콜백까지 돌면 2000판의 문자열이 생깁니다.
        Assert.Equal(0, calls);
    }

    [Fact]
    public void GenerateBoard_한_게시판에_같은_이름의_의뢰가_두_번_뜨지_않는다()
    {
        // 이름이 겹치면 "1번과 2번이 뭐가 다른가"를 알 수 없어 고르는 행위가 무의미해집니다.
        for (int seed = 0; seed < 200; seed++)
        {
            var board = ContractGenerator.GenerateBoard(new DeterministicRandom((ulong)seed), 4, 6);
            var names = board.Select(c => c.Name).ToList();

            Assert.Equal(names.Count, names.Distinct().Count());
        }
    }
}
