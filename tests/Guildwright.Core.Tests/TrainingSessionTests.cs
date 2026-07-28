using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 월 단위 훈련의 설계 가설을 검증합니다.
/// <para>
/// 가설: <b>매달의 선택에 대가가 있어서, 12개월이 "12번 클릭"이 아니라 판단의 연속이 된다.</b>
/// 그 대가가 피로도이고, 변수가 컨디션입니다.
/// </para>
/// 근거: docs/04-game-design.md §5.5
/// </summary>
public class TrainingSessionTests(ITestOutputHelper output)
{
    private const ulong Seed = 5150UL;

    private static GrowthProfile Profile(
        int peakAge = 20,
        Temperament temperament = Temperament.Balanced,
        int potential = 80)
        => new()
        {
            PeakAge = peakAge,
            BloomWidth = 3.0,
            Temperament = temperament,
            Potential = PrimaryStats.Uniform(potential),
            DeclineAge = 40
        };

    private static Adventurer Rookie(int age = 18, int potential = 80)
        => new("T1", "훈련생", PrimaryStats.Uniform(15), 40, Profile(potential: potential), age: age);

    // ---------------------------------------------------------------
    // 기본 진행
    // ---------------------------------------------------------------

    [Fact]
    public void 세션은_12개월로_끝난다()
    {
        var session = new TrainingYearSession(Rookie(), new DeterministicRandom(Seed));

        for (int i = 0; i < 12; i++)
        {
            Assert.False(session.IsComplete);
            session.AdvanceMonth(TrainingFocus.Rest);
        }

        Assert.True(session.IsComplete);
        Assert.Equal(12, session.MonthsCompleted);
        Assert.Throws<InvalidOperationException>(() => session.AdvanceMonth(TrainingFocus.Rest));
    }

    [Fact]
    public void 열두달을_마치기_전에는_결과를_확정할_수_없다()
    {
        var session = new TrainingYearSession(Rookie(), new DeterministicRandom(Seed));
        session.AdvanceMonth(TrainingFocus.Strength);

        Assert.Throws<InvalidOperationException>(() => session.Complete());
    }

    [Fact]
    public void 완료하면_나이를_먹고_능력치가_반영된다()
    {
        var adventurer = Rookie(age: 18);
        var session = new TrainingYearSession(adventurer, new DeterministicRandom(Seed));

        for (int i = 0; i < 12; i++) session.AdvanceMonth(TrainingFocus.Strength);
        session.Complete();

        Assert.Equal(19, adventurer.Age);
        Assert.Equal(1, adventurer.CompletedYears);
        Assert.True(adventurer.Stats.Strength > 15);
    }

    // ---------------------------------------------------------------
    // ★ 집중 훈련 = 특화
    // ---------------------------------------------------------------

    [Fact]
    public void 한_능력치에_집중하면_그_능력치가_크게_자란다()
    {
        // 이게 성립해야 "캐릭터별 특화"를 플레이어가 유도할 수 있습니다.
        var adventurer = Rookie();
        var session = new TrainingYearSession(adventurer, new DeterministicRandom(Seed));

        for (int i = 0; i < 12; i++)
        {
            session.AdvanceMonth(session.Fatigue >= 48 ? TrainingFocus.Rest : TrainingFocus.Strength);
        }
        session.Complete();

        output.WriteLine($"공격 집중 1년: {adventurer.Stats}");

        Assert.True(adventurer.Stats.Strength > adventurer.Stats.Vitality * 2,
            "집중한 능력치가 뚜렷하게 앞서지 않으면, 매달 무엇을 훈련할지 고르는 의미가 없습니다.");
    }

    [Fact]
    public void 집중하지_않은_능력치도_조금은_자란다()
    {
        // 파급이 0이면 한 능력치만 미는 게 항상 정답이 되어 선택이 사라집니다.
        var adventurer = Rookie();
        var session = new TrainingYearSession(adventurer, new DeterministicRandom(Seed));

        for (int i = 0; i < 12; i++)
        {
            session.AdvanceMonth(session.Fatigue >= 48 ? TrainingFocus.Rest : TrainingFocus.Strength);
        }
        session.Complete();

        Assert.True(adventurer.Stats.Vitality > 15);
    }

    // ---------------------------------------------------------------
    // ★ 피로도 — 월 단위 선택에 대가를 만드는 장치
    // ---------------------------------------------------------------

    [Fact]
    public void 훈련하면_피로가_쌓이고_휴식하면_줄어든다()
    {
        var session = new TrainingYearSession(Rookie(), new DeterministicRandom(Seed));

        session.AdvanceMonth(TrainingFocus.Strength);
        int afterTraining = session.Fatigue;
        Assert.True(afterTraining > 0);

        session.AdvanceMonth(TrainingFocus.Rest);
        Assert.True(session.Fatigue < afterTraining);
    }

    [Fact]
    public void 쉬지_않고_밀어붙이면_부상_위험이_생긴다()
    {
        int InjuredRuns(int restThreshold)
        {
            int injured = 0;
            const int trials = 400;

            for (int t = 0; t < trials; t++)
            {
                var adventurer = Rookie();
                var rng = new DeterministicRandom(Seed).Fork($"run:{t}");
                var session = new TrainingYearSession(adventurer, rng);

                while (!session.IsComplete)
                {
                    session.AdvanceMonth(session.Fatigue >= restThreshold ? TrainingFocus.Rest : TrainingFocus.Strength);
                }
                session.Complete();

                if (session.Months.Any(m => m.GotInjured)) injured++;
            }

            return injured;
        }

        int cautious = InjuredRuns(34);
        int reckless = InjuredRuns(95);

        output.WriteLine($"400회 중 부상 발생 · 신중(34) {cautious}건 / 강행(95) {reckless}건");

        Assert.Equal(0, cautious);
        Assert.True(reckless > cautious,
            "밀어붙여도 대가가 없으면 피로도가 장식이 되고, 매달의 선택이 사라집니다.");
    }

    [Fact]
    public void 부상당하면_몇달간_훈련이_불가능하다()
    {
        // 강행하다 부상당한 케이스를 하나 찾습니다.
        for (int t = 0; t < 400; t++)
        {
            var adventurer = Rookie();
            var rng = new DeterministicRandom(Seed).Fork($"inj:{t}");
            var session = new TrainingYearSession(adventurer, rng);

            while (!session.IsComplete) session.AdvanceMonth(TrainingFocus.Strength);

            var injury = session.Months.FirstOrDefault(m => m.GotInjured);
            if (injury is null) continue;

            // 부상 다음 달부터는 선택과 무관하게 요양이어야 합니다.
            var next = session.Months.FirstOrDefault(m => m.Month == injury.Month + 1);
            if (next is null) continue;

            output.WriteLine($"{injury.Note} → 다음 달: {next.Note}");

            Assert.True(next.WasRecovering);
            Assert.Equal(TrainingFocus.Rest, next.Focus);
            return;
        }

        Assert.Fail("400회 중 부상 사례를 찾지 못했습니다. 부상 확률이 지나치게 낮을 수 있습니다.");
    }

    [Fact]
    public void 피로가_높으면_같은_훈련의_성장이_떨어진다()
    {
        int GainAtFatigue(int startingFatigue)
        {
            var adventurer = Rookie();
            var session = new TrainingYearSession(adventurer, new DeterministicRandom(Seed), startingFatigue: startingFatigue);
            var outcome = session.AdvanceMonth(TrainingFocus.Strength);
            return outcome.StatGain.Strength;
        }

        int fresh = GainAtFatigue(0);
        int tired = GainAtFatigue(60);

        output.WriteLine($"공격 훈련 1회 성장 · 피로 0: {fresh} / 피로 60: {tired}");

        Assert.True(tired < fresh,
            "피로가 성장을 깎지 않으면 휴식을 고를 이유가 사라집니다.");
    }

    // ---------------------------------------------------------------
    // 자동 진행
    // ---------------------------------------------------------------

    [Fact]
    public void 방침에_따라_자동으로_1년을_진행할_수_있다()
    {
        var adventurer = Rookie();
        AutoTrainer.RunYear(adventurer, TrainingPolicy.Mage, new DeterministicRandom(Seed));

        output.WriteLine($"마법 방침 1년: {adventurer.Stats}");

        Assert.Equal(1, adventurer.CompletedYears);
        Assert.True(adventurer.Stats.Intellect > adventurer.Stats.Strength,
            "마법 방침을 맡겼는데 물리 능력치가 더 자라면 방침이 무의미합니다.");
    }

    [Fact]
    public void 방침에_따라_다른_캐릭터가_만들어진다()
    {
        PrimaryStats Run(TrainingPolicy policy)
        {
            var a = Rookie();
            for (int y = 0; y < 3; y++)
            {
                AutoTrainer.RunYear(a, policy, new DeterministicRandom(Seed).Fork($"y:{y}"));
            }
            return a.Stats;
        }

        var vanguard = Run(TrainingPolicy.Vanguard);
        var mage = Run(TrainingPolicy.Mage);

        output.WriteLine($"전위 3년: {vanguard}");
        output.WriteLine($"마법 3년: {mage}");

        Assert.True(vanguard.Strength > mage.Strength);
        Assert.True(mage.Intellect > vanguard.Intellect);
    }

    private int[] Distribution(TrainingPolicy policy, int trials = 300)
    {
        var totals = new List<int>(trials);
        for (int t = 0; t < trials; t++)
        {
            var a = Rookie();
            AutoTrainer.RunYear(a, policy, new DeterministicRandom(Seed).Fork($"p:{t}"));
            totals.Add(a.Stats.Total);
        }
        totals.Sort();
        return totals.ToArray();
    }

    [Fact]
    public void 무모하게_밀어붙이는_것은_이득이_아니다()
    {
        // 이건 "밸런스가 틀렸다"가 아니라 의도한 결과입니다.
        // 무모함이 보상받으면 육성의 실력 축이 "얼마나 무리하느냐"가 되어버리는데,
        // 그건 판단이 아니라 그냥 손해 감수입니다.
        var cautious = Distribution(TrainingPolicy.Vanguard.Cautious());
        var aggressive = Distribution(TrainingPolicy.Vanguard.Aggressive());

        output.WriteLine($"신중 · 중앙 {cautious[150]} / 상위5% {cautious[^15]}");
        output.WriteLine($"강행 · 중앙 {aggressive[150]} / 상위5% {aggressive[^15]}");

        Assert.True(aggressive[150] < cautious[150],
            "무모한 강행이 기대값에서 이기면, 피로 관리라는 시스템 자체가 무의미해집니다.");
    }

    [Fact]
    public void 컨디션이_좋은_달을_놓치지_않으면_이득이다()
    {
        // ★ 이게 이 게임 육성의 실제 실력 축입니다.
        // "무리하느냐"가 아니라 "좋은 달을 알아보느냐"가 성과를 가릅니다.
        // 컨디션 배율이 1.30인데 그 달에 쉬면 그냥 버리는 셈입니다.
        var plain = Distribution(TrainingPolicy.Vanguard.Cautious());
        var opportunistic = Distribution(TrainingPolicy.Vanguard.Cautious().Opportunistic());

        output.WriteLine($"신중        · 중앙 {plain[150]}");
        output.WriteLine($"신중+호기포착 · 중앙 {opportunistic[150]}");

        Assert.True(opportunistic[150] > plain[150],
            "컨디션을 보고 판단하는 것이 이득이 아니면, 컨디션 시스템이 장식이 됩니다.");
    }

    // ---------------------------------------------------------------
    // 빠른 경로와 상세 경로는 같은 모델이어야 한다
    // ---------------------------------------------------------------

    [Fact]
    public void 연단위_해석은_월단위_세션을_그대로_사용한다()
    {
        // 배치 시뮬레이션용 빠른 경로와 플레이어가 만지는 경로가 다른 모델이면,
        // 밸런싱해서 맞춘 값이 실제 플레이에서 어긋납니다. 실제로 그 버그를 겪었습니다.
        // 이제 두 경로는 같은 코드이므로, 같은 시드에서 결과가 완전히 일치해야 합니다.
        int Yearly()
        {
            var a = Rookie();
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(Seed));
            return a.Stats.Total;
        }

        int Monthly()
        {
            var a = Rookie();
            AutoTrainer.RunYear(a, TrainingPolicy.Balanced, new DeterministicRandom(Seed));
            return a.Stats.Total;
        }

        output.WriteLine($"연단위 {Yearly()} / 월단위(균형 방침) {Monthly()}");

        Assert.Equal(Yearly(), Monthly());
    }

    // ---------------------------------------------------------------
    // 결정론
    // ---------------------------------------------------------------

    [Fact]
    public void 같은시드_같은선택이면_결과가_동일하다()
    {
        string Run()
        {
            var a = Rookie();
            var session = new TrainingYearSession(a, new DeterministicRandom(Seed));
            for (int i = 0; i < 12; i++)
            {
                session.AdvanceMonth(i % 4 == 3 ? TrainingFocus.Rest : TrainingFocus.Agility);
            }
            session.Complete();
            return $"{a.Stats}|{a.Age}";
        }

        Assert.Equal(Run(), Run());
    }
}
