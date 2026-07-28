using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 파견 1년을 월 단위로 진행하는 구조.
/// <para>
/// 예전에는 파견 = <b>전투 한 판</b>이었습니다. 한 해의 성패가 3~4라운드에 결정되고,
/// 회복약도 짐꾼도 쓸 일이 없으며, 개입할 순간이 한두 번뿐이었습니다.
/// 실제 플레이 피드백이 <b>"개입으로 할 수 있는 게 없다"</b>였는데 원인이 여기였습니다.
/// </para>
/// 근거: docs/06-balance-log.md #34
/// </summary>
public class FieldYearSessionTests(ITestOutputHelper output)
{
    private static Adventurer Veteran(ulong seed = 5, int years = 3)
    {
        var a = Adventurer.Recruit($"H{seed}", "용사", new DeterministicRandom(seed));
        for (int y = 0; y < years; y++)
        {
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(seed * 100 + (ulong)y));
        }
        return a;
    }

    private static FieldYearSession Session(Adventurer hero, ulong seed = 42, int quota = 10) =>
        new([hero], Contract.Combat("가도 정리", 1), quota, new DeterministicRandom(seed));

    [Fact]
    public void 야영은_조우가_드물고_HP를_회복시킨다()
    {
        var hero = Veteran();
        var session = Session(hero);

        // 먼저 다치게 만듭니다 — 수색해서 싸웁니다.
        while (!session.IsComplete && session.Hp[hero.Id] == hero.MaxHp)
        {
            var enc = session.StartMonth(FieldAction.Search);
            if (enc is not null) session.Fight(new DeterministicRandom(7));
        }

        int wounded = session.Hp[hero.Id];
        if (wounded <= 0 || session.IsComplete) return;   // 표본을 못 만들면 통과

        session.StartMonth(FieldAction.Camp);

        output.WriteLine($"야영 전 {wounded} → 후 {session.Hp[hero.Id]}");
        Assert.True(session.Hp[hero.Id] > wounded, "야영했는데 HP가 안 올랐습니다.");
    }

    [Fact]
    public void HP가_전투_사이에_저절로_회복되지_않는다()
    {
        // 이게 이 시스템의 핵심입니다. 회복되면 "지금 싸울까 피할까"가 판단이 아니게 됩니다.
        var hero = Veteran();
        var session = Session(hero);

        int? afterFirstFight = null;

        while (!session.IsComplete)
        {
            var enc = session.StartMonth(FieldAction.Search);
            if (enc is null) continue;

            session.Fight(new DeterministicRandom(11));

            if (afterFirstFight is null)
            {
                afterFirstFight = session.Hp[hero.Id];
                continue;
            }

            // 두 번째 전투 직전의 HP는 첫 전투 직후와 같아야 합니다 (야영을 안 했으므로).
            Assert.True(session.Hp[hero.Id] <= afterFirstFight,
                "야영도 안 했는데 HP가 늘었습니다. 전투 사이 자동 회복이 생기면 자원 판단이 사라집니다.");
            return;
        }
    }

    [Fact]
    public void 회복약은_한_해_내내_같은_것을_쓴다()
    {
        // 짐꾼이 의미를 갖는 지점입니다. 전투마다 리필되면 운반 역량이 죽은 기능이 됩니다.
        var hero = Veteran();
        var session = Session(hero);

        int start = session.Potions[hero.Id];
        Assert.Equal(2, start);

        while (!session.IsComplete)
        {
            var enc = session.StartMonth(FieldAction.Search);
            if (enc is not null) session.Fight(new DeterministicRandom(13));

            Assert.True(session.Potions[hero.Id] <= start,
                "회복약이 늘었습니다. 전투마다 리필되면 한 해의 자원 관리가 사라집니다.");
        }
    }

    [Fact]
    public void 목표를_채우면_그_자리에서_끝난다()
    {
        var hero = Veteran(seed: 9, years: 6);
        var session = Session(hero, seed: 3, quota: 2);

        while (!session.IsComplete)
        {
            var enc = session.StartMonth(FieldAction.Search);
            if (enc is not null) session.Fight(new DeterministicRandom(17));
        }

        var result = session.Complete();
        output.WriteLine($"{result.Killed}/{result.Quota} · {result.Months.Count}개월 · 달성 {result.Achieved}");

        if (result.Achieved)
        {
            Assert.True(result.Killed >= result.Quota);
            Assert.True(result.Months.Count <= TrainingRules.MonthsPerYear);
        }
    }

    [Fact]
    public void 열두달을_넘기지_않는다()
    {
        var hero = Veteran();
        var session = Session(hero, quota: 999);   // 절대 못 채우는 목표

        while (!session.IsComplete)
        {
            var enc = session.StartMonth(FieldAction.Patrol);
            if (enc is not null) session.Fight(new DeterministicRandom(19));
        }

        var result = session.Complete();
        Assert.True(result.Months.Count <= TrainingRules.MonthsPerYear,
            $"{result.Months.Count}개월이 진행됐습니다. 1년을 넘으면 안 됩니다.");
        Assert.False(result.Achieved);
    }

    [Fact]
    public void 수색이_순찰보다_자주_조우한다()
    {
        int Encounters(FieldAction action)
        {
            int found = 0;
            for (ulong s = 0; s < 60; s++)
            {
                var hero = Veteran(seed: s + 1, years: 3);
                var session = new FieldYearSession(
                    [hero], Contract.Combat("가도 정리", 1), 999, new DeterministicRandom(s * 7 + 1));

                // 피로 영향을 없애려고 첫 달만 봅니다.
                if (session.StartMonth(action) is not null) found++;
            }
            return found;
        }

        int search = Encounters(FieldAction.Search);
        int patrol = Encounters(FieldAction.Patrol);
        int camp = Encounters(FieldAction.Camp);

        output.WriteLine($"60회 중 조우 — 수색 {search} · 순찰 {patrol} · 야영 {camp}");

        Assert.True(search > patrol, "수색이 순찰보다 자주 마주쳐야 선택이 갈립니다.");
        Assert.True(patrol > camp, "야영이 순찰만큼 마주치면 쉬는 선택이 위험해집니다.");
    }

    [Fact]
    public void 지치면_조우_확률이_떨어진다()
    {
        // 이게 없으면 12개월 내내 수색만 하는 게 언제나 정답이 됩니다.
        Assert.Equal(1.0, FieldRules.Alertness(0));
        Assert.Equal(1.0, FieldRules.Alertness(TrainingRules.FatigueSoftCap));
        Assert.True(FieldRules.Alertness(100) < 0.7,
            "피로 100에서도 조우율이 그대로면 무작정 수색이 최적해가 됩니다.");
    }

    [Fact]
    public void 조우를_처리하기_전에는_다음_달로_못_넘어간다()
    {
        var hero = Veteran();
        var session = Session(hero);

        while (!session.IsComplete)
        {
            if (session.StartMonth(FieldAction.Search) is null) continue;

            // 조우한 채로 다음 달을 시작하려 하면 막혀야 합니다.
            Assert.True(session.HasPendingEncounter);
            Assert.Throws<InvalidOperationException>(() => session.StartMonth(FieldAction.Camp));

            session.Fight(new DeterministicRandom(23));
            Assert.False(session.HasPendingEncounter);
            return;
        }
    }
}
