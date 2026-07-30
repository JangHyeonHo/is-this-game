using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 전투 개입에 <b>횟수 제한이 없다</b>는 것을 지킵니다.
/// <para>
/// 예전에는 전투당 지휘 포인트 3점이었고 위치 변경이 2점이었습니다. 전투가 3~4라운드라
/// 사실상 한두 번밖에 못 썼고, 아끼려다 결국 안 쓰게 됐습니다.
/// 실제 플레이 피드백이 <b>"개입으로 할 수 있는 게 없다"</b>였는데
/// 원인이 기능 부재가 아니라 이 제한이었습니다.
/// </para>
/// <para>
/// 대신 유일한 제약은 <b>공포·혼란에 걸린 아군에게는 지시가 통하지 않는 것</b>입니다.
/// </para>
/// 근거: docs/08-design-revision.md §14, §18.7
/// </summary>
public class CommandInterventionTests(ITestOutputHelper output)
{
    /// <summary>매 턴 방어를 지시하고, 몇 번 지시했는지 셉니다.</summary>
    private sealed class AlwaysDefend : IBattleCommander
    {
        public Team Team => Team.Player;
        public int Attempts { get; private set; }

        public CommandOrder? Intervene(Combatant actor, ChosenAction aiChoice, BattleState state)
        {
            Attempts++;
            return new CommandOrder(TacticAction.Defend, null);
        }
    }

    private static BattleState Duel(out Combatant hero, out Combatant foe)
    {
        hero = TestParty.Make("H", Team.Player, 70);
        foe = TestParty.Make("E", Team.Enemy, 70);
        return new BattleState([hero, foe]);
    }

    [Fact]
    public void 개입_횟수에_제한이_없다()
    {
        var state = Duel(out var hero, out _);
        var commander = new AlwaysDefend();

        new BattleResolver(maxRounds: 12, recordLog: true)
            .Resolve(state, new DeterministicRandom(11), commander);

        output.WriteLine($"개입 시도 {commander.Attempts}회");

        // 옛 규칙(3점)이 살아 있으면 4회 이상 물어보지 않았을 것입니다.
        Assert.True(commander.Attempts > 3,
            $"개입 기회가 {commander.Attempts}번뿐입니다. 횟수 제한이 어딘가 남아 있습니다.");
    }

    [Fact]
    public void 위치_변경도_추가_비용_없이_지시된다()
    {
        // 옛 규칙에서는 위치 변경이 2점이라 사실상 전투당 한 번이었습니다.
        var hero = TestParty.Make("H", Team.Player, 70);
        var ally = TestParty.Make("A", Team.Player, 70);
        var foe = TestParty.Make("E", Team.Enemy, 70);
        var state = new BattleState([hero, ally, foe]);

        var moves = 0;
        var commander = new Lambda(Team.Player, (actor, _, _) =>
        {
            if (actor.Row != Row.Back)
            {
                moves++;
                return new CommandOrder(TacticAction.MoveBack, null);
            }
            return new CommandOrder(TacticAction.MoveFront, null);
        });

        new BattleResolver(maxRounds: 8, recordLog: true)
            .Resolve(state, new DeterministicRandom(13), commander);

        output.WriteLine($"위치 변경 지시 {moves}회");
        Assert.True(moves > 1, "위치 변경이 한 번밖에 안 됐습니다.");
    }

    [Fact]
    public void 공포에_걸린_아군에게는_지시가_통하지_않는다()
    {
        var state = Duel(out var hero, out _);
        hero.ApplyEffect(StatusEffects.Create(EffectName.Fear, 10));

        var commander = new AlwaysDefend();
        var result = new BattleResolver(maxRounds: 4, recordLog: true)
            .Resolve(state, new DeterministicRandom(17), commander);

        Assert.Contains(result.Log, line => line.Contains("지시가 통하지 않음"));
    }

    [Fact]
    public void 마비는_지시를_막지_않지만_행동을_막는다()
    {
        // 지시 불통과 다릅니다 — 듣고도 몸이 안 움직이는 경우입니다.
        var state = Duel(out var hero, out _);
        hero.ApplyEffect(StatusEffects.Create(EffectName.Petrify, 10));   // 확률 1.0

        var result = new BattleResolver(maxRounds: 3, recordLog: true)
            .Resolve(state, new DeterministicRandom(19));

        Assert.Contains(result.Log, line => line.Contains("움직이지 못함"));
    }

    [Fact]
    public void 관전은_결과를_바꾸지_않는다()
    {
        // 지휘를 넘기지 않으면 완전 자동입니다. 배치 시뮬레이션과 실제 플레이가
        // 같은 결과를 내야 밸런싱이 의미를 가집니다.
        static BattleResult Run(bool withLog)
        {
            var hero = TestParty.Make("H", Team.Player, 70);
            var foe = TestParty.Make("E", Team.Enemy, 70);
            return new BattleResolver(maxRounds: 20, recordLog: withLog)
                .Resolve(new BattleState([hero, foe]), new DeterministicRandom(23));
        }

        var quiet = Run(false);
        var loud = Run(true);

        Assert.Equal(quiet.Outcome, loud.Outcome);
        Assert.Equal(quiet.Rounds, loud.Rounds);
    }

    [Fact]
    public void 전투가_끝나면_상태는_풀리고_상처는_남는다()
    {
        var state = Duel(out var hero, out _);

        hero.ApplyEffect(StatusEffects.Create(EffectName.Poison, 3));      // 상처
        hero.ApplyEffect(StatusEffects.Create(EffectName.PowerUp, 3));     // 상태

        new BattleResolver(maxRounds: 2).Resolve(state, new DeterministicRandom(29));

        Assert.True(hero.HasEffect(EffectName.Poison), "중독이 전투가 끝나자 사라졌습니다.");
        Assert.False(hero.HasEffect(EffectName.PowerUp), "강화가 전투 뒤에도 남았습니다.");
    }

    /// <summary>테스트용 지휘자. 넘긴 함수를 그대로 씁니다.</summary>
    private sealed class Lambda(
        Team team,
        Func<Combatant, ChosenAction, BattleState, CommandOrder?> decide) : IBattleCommander
    {
        public Team Team => team;

        public CommandOrder? Intervene(Combatant actor, ChosenAction aiChoice, BattleState state) =>
            decide(actor, aiChoice, state);
    }
}
