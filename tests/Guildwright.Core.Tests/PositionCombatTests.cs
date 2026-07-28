using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 전열/후열과 동적 포지션 변경을 검증합니다.
/// <para>
/// 가설: <b>전열/후열은 고정된 역할이 아니라 매 순간의 선택이며,
/// 판단력이 그 선택의 품질을 좌우한다.</b>
/// 다친 전사를 뒤로 물릴 것인가, 각오하고 남길 것인가 — 그게 화면에 보여야 합니다.
/// </para>
/// 근거: docs/04-game-design.md §4.5
/// </summary>
public class PositionCombatTests(ITestOutputHelper output)
{
    private const ulong Seed = 3131UL;

    // ---------------------------------------------------------------
    // 도달 범위
    // ---------------------------------------------------------------

    [Fact]
    public void 근접무기는_적_전열까지만_닿는다()
    {
        var state = new BattleState(
        [
            TestParty.Make("P0", Team.Player, 60, style: WeaponStyle.TwoHanded, row: Row.Front),
            TestParty.Make("E0", Team.Enemy, 60, style: WeaponStyle.SwordAndShield, row: Row.Front),
            TestParty.Make("E1", Team.Enemy, 60, style: WeaponStyle.Staff, row: Row.Back)
        ]);

        var reachable = state.ReachableTargets(state.All[0]);

        Assert.Single(reachable);
        Assert.Equal("E0", reachable[0].Id);
    }

    [Fact]
    public void 적_전열이_비면_후열이_그대로_노출된다()
    {
        // 이 규칙이 있어야 전열 유지가 방어 행위가 되고, 후퇴가 공짜가 아니게 됩니다.
        var state = new BattleState(
        [
            TestParty.Make("P0", Team.Player, 60, style: WeaponStyle.TwoHanded, row: Row.Front),
            TestParty.Make("E0", Team.Enemy, 60, style: WeaponStyle.Staff, row: Row.Back),
            TestParty.Make("E1", Team.Enemy, 60, style: WeaponStyle.Staff, row: Row.Back)
        ]);

        var reachable = state.ReachableTargets(state.All[0]);

        Assert.Equal(2, reachable.Count);
    }

    [Fact]
    public void 활과_석궁과_지팡이는_후열을_직접_노린다()
    {
        foreach (var style in new[] { WeaponStyle.Bow, WeaponStyle.Crossbow, WeaponStyle.Staff })
        {
            var state = new BattleState(
            [
                TestParty.Make("P0", Team.Player, 60, style: style, row: Row.Back),
                TestParty.Make("E0", Team.Enemy, 60, style: WeaponStyle.SwordAndShield, row: Row.Front),
                TestParty.Make("E1", Team.Enemy, 60, style: WeaponStyle.Staff, row: Row.Back)
            ]);

            var reachable = state.ReachableTargets(state.All[0]);
            Assert.Equal(2, reachable.Count);
        }
    }

    // ---------------------------------------------------------------
    // 위치가 만드는 차이
    // ---------------------------------------------------------------

    [Fact]
    public void 후열에_있으면_피해를_덜_받는다()
    {
        var attacker = TestParty.Make("A", Team.Player, 60, style: WeaponStyle.Bow);
        var front = TestParty.Make("F", Team.Enemy, 60, row: Row.Front);
        var back = TestParty.Make("B", Team.Enemy, 60, row: Row.Back);

        int toFront = DamageModel.ExpectedDamage(attacker, front);
        int toBack = DamageModel.ExpectedDamage(attacker, back);

        output.WriteLine($"전열 대상 {toFront} / 후열 대상 {toBack}");

        Assert.True(toBack < toFront,
            "후퇴에 실질적 이득이 없으면 포지션 판단이 의미를 갖지 못합니다.");
    }

    [Fact]
    public void 근접무기가_후열에서_휘두르면_위력이_떨어진다()
    {
        var target = TestParty.Make("T", Team.Enemy, 60, row: Row.Front);

        var atFront = TestParty.Make("A", Team.Player, 60, style: WeaponStyle.TwoHanded, row: Row.Front);
        var atBack = TestParty.Make("B", Team.Player, 60, style: WeaponStyle.TwoHanded, row: Row.Back);

        int fromFront = DamageModel.ExpectedDamage(atFront, target);
        int fromBack = DamageModel.ExpectedDamage(atBack, target);

        output.WriteLine($"양손무기 · 전열에서 {fromFront} / 후열에서 {fromBack}");

        Assert.True(fromBack < fromFront);
        Assert.True(fromBack > 0, "완전히 무력해지면 후퇴가 사실상 사망 선고가 되어 선택이 사라집니다.");
    }

    [Fact]
    public void 활은_후열에_있어도_위력이_온전하다()
    {
        var target = TestParty.Make("T", Team.Enemy, 60, row: Row.Front);

        var atFront = TestParty.Make("A", Team.Player, 60, style: WeaponStyle.Bow, row: Row.Front);
        var atBack = TestParty.Make("B", Team.Player, 60, style: WeaponStyle.Bow, row: Row.Back);

        Assert.Equal(
            DamageModel.ExpectedDamage(atFront, target),
            DamageModel.ExpectedDamage(atBack, target));
    }

    // ---------------------------------------------------------------
    // ★ 동적 포지션 — 판단력이 눈에 보이는 지점
    // ---------------------------------------------------------------

    [Fact]
    public void 판단력이_높으면_위험할_때_물러날_줄_안다()
    {
        // 낮은 판단력은 죽어가면서도 전열에 남습니다. 그게 신입이라는 것의 의미입니다.
        double RetreatRate(int judgement)
        {
            int retreated = 0;
            const int trials = 400;

            for (int t = 0; t < trials; t++)
            {
                // 빈사 상태의 창병. 후열에서도 제 몫을 하므로 물러나는 게 합리적입니다.
                var wounded = TestParty.Make(
                    "P0", Team.Player, judgement, TestParty.RetreatingTactics,
                    style: WeaponStyle.Polearm, row: Row.Front);
                wounded.TakeDamage((int)(wounded.MaxHp * 0.85));

                var state = new BattleState(
                [
                    wounded,
                    TestParty.Make("P1", Team.Player, judgement, style: WeaponStyle.SwordAndShield, row: Row.Front),
                    TestParty.Make("P2", Team.Player, judgement, style: WeaponStyle.SwordAndShield, row: Row.Front),
                    TestParty.Make("E0", Team.Enemy, 50, row: Row.Front),
                    TestParty.Make("E1", Team.Enemy, 50, row: Row.Front)
                ]);

                var choice = TacticalBrain.Decide(wounded, state, new DeterministicRandom(Seed).Fork($"t:{t}"));
                if (choice.Action == TacticAction.MoveBack) retreated++;
            }

            return (double)retreated / trials;
        }

        double rookie = RetreatRate(10);
        double veteran = RetreatRate(95);

        output.WriteLine($"HP 15%에서 물러나는 비율 · 판단력 10: {rookie:P1} / 판단력 95: {veteran:P1}");

        Assert.True(veteran > rookie,
            "판단력이 높아도 물러설 줄 모르면, '물러설 때를 안다'는 설계가 화면에 드러나지 않습니다.");
    }

    [Fact]
    public void 전열에_혼자_남으면_함부로_물러나지_않는다()
    {
        // 내가 빠지면 아군 후열이 통째로 노출됩니다. 후퇴가 공짜가 아니어야 합니다.
        double RetreatRate(int frontLineCount)
        {
            int retreated = 0;
            const int trials = 400;

            for (int t = 0; t < trials; t++)
            {
                var members = new List<Combatant>();

                var wounded = TestParty.Make(
                    "P0", Team.Player, 95, TestParty.RetreatingTactics,
                    style: WeaponStyle.Polearm, row: Row.Front);
                wounded.TakeDamage((int)(wounded.MaxHp * 0.85));
                members.Add(wounded);

                for (int i = 1; i < frontLineCount; i++)
                {
                    members.Add(TestParty.Make($"P{i}", Team.Player, 95, row: Row.Front));
                }

                members.Add(TestParty.Make("PB", Team.Player, 95, style: WeaponStyle.Staff, row: Row.Back));
                members.Add(TestParty.Make("E0", Team.Enemy, 50, row: Row.Front));

                var state = new BattleState(members);
                var choice = TacticalBrain.Decide(wounded, state, new DeterministicRandom(Seed).Fork($"t:{t}"));
                if (choice.Action == TacticAction.MoveBack) retreated++;
            }

            return (double)retreated / trials;
        }

        double alone = RetreatRate(1);
        double withSupport = RetreatRate(3);

        output.WriteLine($"HP 15% 창병이 물러나는 비율 · 전열에 혼자: {alone:P1} / 전열에 셋: {withSupport:P1}");

        Assert.True(alone < withSupport,
            "전열을 비우는 대가가 없으면 후퇴가 공짜가 되고, 포지션이 판단이 아니게 됩니다.");
    }

    // ---------------------------------------------------------------
    // 육성 → 전투 연결
    // ---------------------------------------------------------------

    [Fact]
    public void 육성한_모험가로_실제_전투를_치를_수_있다()
    {
        // ★ 이게 이번 작업의 핵심입니다. 육성 파트와 전투 파트가 한 줄로 이어집니다.
        var rng = new DeterministicRandom(Seed);

        var party = new List<Adventurer>();
        for (int i = 0; i < 4; i++)
        {
            var adventurer = Adventurer.Recruit($"A{i}", $"모험가{i}", rng.Fork($"recruit:{i}"));

            // 4년 육성
            for (int y = 0; y < 4; y++)
            {
                CareerSimulator.ResolveTrainingYear(adventurer, rng.Fork($"train:{i}:{y}"));
            }

            party.Add(adventurer);
            output.WriteLine($"  {adventurer}");
        }

        var enemies = new List<Adventurer>();
        for (int i = 0; i < 4; i++)
        {
            var enemy = Adventurer.Recruit($"B{i}", $"적{i}", rng.Fork($"enemy:{i}"));
            CareerSimulator.ResolveTrainingYear(enemy, rng.Fork($"etrain:{i}"));
            enemies.Add(enemy);
        }

        var state = CombatantFactory.FormParty(party, enemies);
        var result = new BattleResolver(recordLog: true).Resolve(state, rng.Fork("battle"));

        output.WriteLine($"결과: {result.Outcome}, {result.Rounds}라운드");
        foreach (var line in result.Log.Take(12)) output.WriteLine($"  {line}");

        Assert.NotEqual(BattleOutcome.Draw, result.Outcome);

        // 4년 육성한 쪽이 1년만 육성한 쪽을 이겨야 합니다.
        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
    }

    [Fact]
    public void 파티_배치는_전열을_비운_채_시작하지_않는다()
    {
        // 전열이 빈 채로 시작하면 첫 라운드에 후열이 통째로 노출됩니다.
        var rng = new DeterministicRandom(Seed);

        var allRanged = Enumerable.Range(0, 3).Select(i =>
        {
            var a = Adventurer.Recruit($"R{i}", $"궁수{i}", rng.Fork($"r:{i}"));
            a.Equip(WeaponStyle.Bow, WeaponClass.Pierce);
            return a;
        }).ToList();

        var enemy = new[] { Adventurer.Recruit("E0", "적", rng.Fork("e")) };

        var state = CombatantFactory.FormParty(allRanged, enemy);

        Assert.False(state.IsFrontRowEmpty(Team.Player),
            "전원이 원거리라도 누군가는 앞에 서야 합니다.");
    }

    [Fact]
    public void 숙련도가_높은_무기를_들면_더_강하다()
    {
        var target = TestParty.Make("T", Team.Enemy, 60);

        var novice = TestParty.Make("N", Team.Player, 60, weaponEffectiveness: 0.75);
        var master = TestParty.Make("M", Team.Player, 60, weaponEffectiveness: 1.30);

        int noviceDamage = DamageModel.ExpectedDamage(novice, target);
        int masterDamage = DamageModel.ExpectedDamage(master, target);

        output.WriteLine($"같은 능력치 · 숙련 0: {noviceDamage} / 숙련 100: {masterDamage}");

        Assert.True(masterDamage > noviceDamage,
            "숙련도가 전투에 반영되지 않으면 무기를 오래 쓰는 의미가 사라집니다.");
    }

    // ---------------------------------------------------------------
    // 스타일별 역할
    // ---------------------------------------------------------------

    [Fact]
    public void 지팡이만_회복할_수_있다()
    {
        var healer = TestParty.Make("H", Team.Player, 90,
            [TacticRule.AllyHpBelow(0.5, TacticAction.HealAlly), TacticRule.Always(TacticAction.AttackNearest)],
            style: WeaponStyle.Staff, row: Row.Back);

        var wounded = TestParty.Make("W", Team.Player, 60, row: Row.Front);
        wounded.TakeDamage((int)(wounded.MaxHp * 0.7));

        var state = new BattleState([healer, wounded, TestParty.Make("E0", Team.Enemy, 50)]);
        var choice = TacticalBrain.Decide(healer, state, new DeterministicRandom(Seed));

        Assert.Equal(TacticAction.HealAlly, choice.Action);

        // 같은 상황에서 검사는 회복할 수 없습니다.
        var swordsman = TestParty.Make("S", Team.Player, 90,
            [TacticRule.AllyHpBelow(0.5, TacticAction.HealAlly), TacticRule.Always(TacticAction.AttackNearest)],
            style: WeaponStyle.SwordAndShield);

        var state2 = new BattleState([swordsman, wounded, TestParty.Make("E1", Team.Enemy, 50)]);
        var choice2 = TacticalBrain.Decide(swordsman, state2, new DeterministicRandom(Seed));

        Assert.NotEqual(TacticAction.HealAlly, choice2.Action);
    }

    [Fact]
    public void 상태효과는_지속시간이_지나면_사라진다()
    {
        var target = TestParty.Make("T", Team.Player, 60);
        target.ApplyEffect(new StatusEffect(StatusEffectKind.Empowered, 2, 0.3, "X"));

        int boosted = target.EffectiveAttack;
        Assert.True(boosted > target.Stats.Attack);

        target.TickEffects();
        Assert.True(target.HasEffect(StatusEffectKind.Empowered));

        target.TickEffects();
        Assert.False(target.HasEffect(StatusEffectKind.Empowered));
        Assert.Equal(target.Stats.Attack, target.EffectiveAttack);
    }

    [Fact]
    public void 같은_효과는_중첩되지_않는다()
    {
        // 중첩을 허용하면 조합 폭발로 밸런싱이 불가능해집니다.
        var target = TestParty.Make("T", Team.Player, 60);

        target.ApplyEffect(new StatusEffect(StatusEffectKind.Empowered, 3, 0.3, "X"));
        target.ApplyEffect(new StatusEffect(StatusEffectKind.Empowered, 3, 0.3, "Y"));

        Assert.Single(target.Effects);
    }
}
