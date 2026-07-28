using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 육성 단계의 핵심 설계 가설을 검증합니다.
/// <para>
/// 가설: <b>"매년 훈련이냐 실전이냐"라는 선택이, 숨겨진 성장 타입에 따라 서로 다른 정답을 가진다.</b>
/// 이게 성립하지 않으면 선택이 무의미해지고, 정보를 얻으려는 동기도 사라집니다.
/// </para>
/// 근거: docs/04-game-design.md §5
/// </summary>
public class CareerDesignTests(ITestOutputHelper output)
{
    private const ulong Seed = 909UL;

    private static GrowthProfile Profile(
        int peakAge,
        Temperament temperament = Temperament.Balanced,
        int potential = 70,
        double bloomWidth = 3.0,
        int declineAge = 40)
        => new()
        {
            PeakAge = peakAge,
            BloomWidth = bloomWidth,
            Temperament = temperament,
            Potential = StatBlock.Uniform(potential),
            DeclineAge = declineAge
        };

    private static Adventurer Rookie(GrowthProfile growth, int judgement = 40)
        => new("A1", "테스트", StatBlock.Uniform(12), judgement, growth);

    // ---------------------------------------------------------------
    // 성장 곡선
    // ---------------------------------------------------------------

    [Fact]
    public void 능력치는_잠재력을_넘지_않는다()
    {
        var adventurer = Rookie(Profile(peakAge: 20, potential: 60));
        var rng = new DeterministicRandom(Seed);

        for (int i = 0; i < 25; i++)
        {
            CareerSimulator.ResolveTrainingYear(adventurer, rng);
        }

        Assert.True(
            adventurer.Stats.Attack <= 60,
            $"잠재력 60을 넘었습니다 ({adventurer.Stats.Attack}). 훈련만으로 상한을 넘으면 안 됩니다.");
    }

    [Fact]
    public void 개화기에는_비개화기보다_훨씬_빠르게_자란다()
    {
        var growth = Profile(peakAge: 25);

        // 같은 능력치에서 출발시켜 나이만 다르게 둡니다.
        var young = new Adventurer("Y", "15세", StatBlock.Uniform(20), 40, growth, age: 15);
        var peak = new Adventurer("P", "25세", StatBlock.Uniform(20), 40, growth, age: 25);

        var beforeYoung = young.Stats.Total;
        var beforePeak = peak.Stats.Total;

        CareerSimulator.ResolveTrainingYear(young, new DeterministicRandom(Seed));
        CareerSimulator.ResolveTrainingYear(peak, new DeterministicRandom(Seed));

        int gainYoung = young.Stats.Total - beforeYoung;
        int gainPeak = peak.Stats.Total - beforePeak;

        output.WriteLine($"개화 25세 프로필 · 15세 성장 {gainYoung} vs 25세 성장 {gainPeak}");

        Assert.True(gainPeak > gainYoung * 2,
            "개화기의 성장이 비개화기보다 뚜렷하게 크지 않으면, 개화 시기라는 개념이 무의미해집니다.");
    }

    [Fact]
    public void 비개화기에도_최소한은_자란다()
    {
        // 하한이 없으면 대기만성형을 데리고 있을 이유가 사라집니다.
        var adventurer = new Adventurer("L", "대기만성", StatBlock.Uniform(12), 40, Profile(peakAge: 26), age: 15);
        var before = adventurer.Stats.Total;

        CareerSimulator.ResolveTrainingYear(adventurer, new DeterministicRandom(Seed));

        Assert.True(adventurer.Stats.Total > before,
            "개화기에서 멀어도 최소한의 성장은 있어야 합니다.");
    }

    [Fact]
    public void 노화가_시작되면_능력치가_깎인다()
    {
        var growth = Profile(peakAge: 20, declineAge: 28);
        var veteran = new Adventurer("V", "노병", StatBlock.Uniform(65), 60, growth, age: 40);

        var before = veteran.Stats.Total;
        CareerSimulator.ResolveTrainingYear(veteran, new DeterministicRandom(Seed));

        output.WriteLine($"40세 · {before} → {veteran.Stats.Total}");

        Assert.True(veteran.Stats.Total < before,
            "노화 나이를 한참 넘겼는데도 능력치가 유지되면 세대 교체 압력이 사라집니다.");
    }

    // ---------------------------------------------------------------
    // 기질
    // ---------------------------------------------------------------

    [Fact]
    public void 수련형은_훈련에서_실전형은_실전에서_더_자란다()
    {
        int TrainGain(Temperament temperament, YearActivity activity)
        {
            var adventurer = new Adventurer(
                "T", temperament.ToString(), StatBlock.Uniform(20), 60, Profile(20, temperament), age: 20);

            var rng = new DeterministicRandom(Seed);
            int before = adventurer.Stats.Total;

            if (activity == YearActivity.Training)
            {
                CareerSimulator.ResolveTrainingYear(adventurer, rng);
            }
            else
            {
                // 난이도를 아주 낮춰 부상 노이즈를 배제하고 성장만 봅니다.
                CareerSimulator.ResolveDeploymentYear(WithOneYear(adventurer), 1, rng);
            }

            return adventurer.Stats.Total - before;
        }

        int studiousTraining = TrainGain(Temperament.Studious, YearActivity.Training);
        int battlebornTraining = TrainGain(Temperament.Battleborn, YearActivity.Training);

        output.WriteLine($"훈련 1년 성장 · 수련형 {studiousTraining} vs 실전형 {battlebornTraining}");

        Assert.True(studiousTraining > battlebornTraining,
            "수련형이 훈련에서 더 자라지 않으면 기질이 선택에 영향을 주지 못합니다.");
    }

    /// <summary>등록 첫 해 제약을 우회하기 위해 훈련 1년을 미리 소화시킵니다.</summary>
    private static Adventurer WithOneYear(Adventurer adventurer)
    {
        if (!adventurer.CanDeploy)
        {
            CareerSimulator.ResolveTrainingYear(adventurer, new DeterministicRandom(1UL));
        }
        return adventurer;
    }

    // ---------------------------------------------------------------
    // 실전 리스크
    // ---------------------------------------------------------------

    [Fact]
    public void 등록_첫해에는_실전에_나갈_수_없다()
    {
        var rookie = Rookie(Profile(20));

        Assert.False(rookie.CanDeploy);
        Assert.Throws<InvalidOperationException>(
            () => CareerSimulator.ResolveDeploymentYear(rookie, 3, new DeterministicRandom(Seed)));

        CareerSimulator.ResolveTrainingYear(rookie, new DeterministicRandom(Seed));
        Assert.True(rookie.CanDeploy);
    }

    [Fact]
    public void 난이도가_높을수록_사망률이_오른다()
    {
        double DeathRate(int difficulty)
        {
            int deaths = 0;
            const int trials = 3_000;

            for (int i = 0; i < trials; i++)
            {
                var a = new Adventurer("D", "실험체", StatBlock.Uniform(30), 40, Profile(20), age: 20);
                CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(Seed).Fork($"warm:{i}"));
                CareerSimulator.ResolveDeploymentYear(a, difficulty, new DeterministicRandom(Seed).Fork($"run:{i}"));
                if (a.Status == AdventurerStatus.Dead) deaths++;
            }

            return (double)deaths / trials;
        }

        double easy = DeathRate(2);
        double hard = DeathRate(8);

        output.WriteLine($"능력치 총합 약 210 · 난이도 2 사망률 {easy:P2} / 난이도 8 사망률 {hard:P2}");

        Assert.True(hard > easy * 3,
            "난이도가 위험에 뚜렷하게 반영되지 않으면 의뢰 선택이 무의미해집니다.");
    }

    [Fact]
    public void 판단력이_높으면_실전에서_더_잘_살아남는다()
    {
        double SurvivalRate(int judgement)
        {
            int survived = 0;
            const int trials = 3_000;

            for (int i = 0; i < trials; i++)
            {
                var a = new Adventurer("S", "실험체", StatBlock.Uniform(25), judgement, Profile(20), age: 20);
                CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(Seed).Fork($"warm:{i}"));
                CareerSimulator.ResolveDeploymentYear(a, 7, new DeterministicRandom(Seed).Fork($"run:{i}"));
                if (a.Status != AdventurerStatus.Dead) survived++;
            }

            return (double)survived / trials;
        }

        double dull = SurvivalRate(10);
        double sharp = SurvivalRate(95);

        output.WriteLine($"난이도 7 생존율 · 판단력 10: {dull:P1} / 판단력 95: {sharp:P1}");

        Assert.True(sharp > dull,
            "판단력이 생존에 기여하지 않으면, 전투와 육성을 잇는 중심 스탯이라는 설계가 무너집니다.");
    }

    /// <summary>능력치에 비해 터무니없이 어려운 의뢰를 반복시켜 특정 결말을 뽑아냅니다.</summary>
    private static Adventurer? RunUntil(AdventurerStatus target, int attempts = 500)
    {
        for (int i = 0; i < attempts; i++)
        {
            var a = new Adventurer($"X{i}", "희생자", StatBlock.Uniform(5), 0, Profile(20), age: 20);
            var rng = new DeterministicRandom(Seed).Fork($"doom:{i}");

            CareerSimulator.ResolveTrainingYear(a, rng);
            CareerSimulator.ResolveDeploymentYear(a, 10, rng);

            if (a.Status == target) return a;
        }
        return null;
    }

    [Fact]
    public void 사망하면_되살아나지_않는다()
    {
        var dead = RunUntil(AdventurerStatus.Dead);

        Assert.NotNull(dead);
        Assert.False(dead!.IsAlive);
        Assert.False(dead.CanDeploy);
        Assert.False(dead.CanMentor);
        Assert.Throws<InvalidOperationException>(
            () => CareerSimulator.ResolveTrainingYear(dead, new DeterministicRandom(1UL)));
    }

    [Fact]
    public void 불구가_되면_현역에서_빠지지만_멘토는_될_수_있다()
    {
        // 사고 결과를 생존/사망 둘로만 두면 너무 거칩니다.
        // 불구는 "캐릭터를 잃되 완전히 잃지는 않는" 중간 결말입니다.
        var crippled = RunUntil(AdventurerStatus.Crippled);

        Assert.NotNull(crippled);
        Assert.True(crippled!.IsAlive);
        Assert.False(crippled.CanDeploy);
        Assert.True(crippled.CanMentor);
        Assert.Throws<InvalidOperationException>(
            () => CareerSimulator.ResolveTrainingYear(crippled, new DeterministicRandom(1UL)));
    }

    // ---------------------------------------------------------------
    // ★ 핵심 가설: 성장 타입에 따라 최적 전략이 다르다
    // ---------------------------------------------------------------

    [Fact]
    public void 대기만성형은_오래_키울수록_강해지고_조숙형은_그렇지_않다()
    {
        // 이 테스트가 이 게임의 육성 파트가 존재하는 이유입니다.
        // 모든 캐릭터에게 같은 전략이 통하면 매년의 선택은 그냥 버튼 누르기가 됩니다.
        int PowerAfterTraining(GrowthProfile growth, int untilAge)
        {
            var a = new Adventurer("C", "실험체", StatBlock.Uniform(12), 40, growth, age: 15);
            var rng = new DeterministicRandom(Seed);
            while (a.Age < untilAge)
            {
                CareerSimulator.ResolveTrainingYear(a, rng);
            }
            return a.Stats.Total;
        }

        var early = Profile(peakAge: 17, declineAge: 24);
        var late = Profile(peakAge: 26, declineAge: 33);

        int earlyAt19 = PowerAfterTraining(early, 19);
        int earlyAt27 = PowerAfterTraining(early, 27);
        int lateAt19 = PowerAfterTraining(late, 19);
        int lateAt27 = PowerAfterTraining(late, 27);

        output.WriteLine($"조숙형   19세 {earlyAt19} → 27세 {earlyAt27} (증가 {earlyAt27 - earlyAt19})");
        output.WriteLine($"대기만성 19세 {lateAt19} → 27세 {lateAt27} (증가 {lateAt27 - lateAt19})");

        Assert.True(earlyAt19 > lateAt19,
            "19세 시점에는 조숙형이 앞서야 합니다. 그래야 '지금 내보낼까'라는 유혹이 생깁니다.");

        Assert.True(lateAt27 - lateAt19 > earlyAt27 - earlyAt19,
            "대기만성형이 늦게 더 크게 자라지 않으면, 오래 기다릴 이유가 없어집니다.");
    }

    // ---------------------------------------------------------------
    // 멘토
    // ---------------------------------------------------------------

    [Fact]
    public void 멘토가_있으면_훈련_성장이_커진다()
    {
        var veteran = new Adventurer("M", "노병", StatBlock.Uniform(90), 80, Profile(20), age: 34);
        for (int i = 0; i < 8; i++)
        {
            CareerSimulator.ResolveTrainingYear(veteran, new DeterministicRandom(Seed).Fork($"v:{i}"));
        }
        veteran.Retire();

        var mentorship = Mentorship.From(veteran);
        output.WriteLine($"멘토 훈련 배율 {mentorship.TrainingMultiplier:F3}, 감정 보너스 {mentorship.AppraisalBonus:F2}");

        int GainWith(Mentorship? m)
        {
            var a = new Adventurer("R", "신입", StatBlock.Uniform(15), 40, Profile(20), age: 18);
            int before = a.Stats.Total;
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(Seed), m);
            return a.Stats.Total - before;
        }

        Assert.True(GainWith(mentorship) > GainWith(null),
            "멘토가 성장에 기여하지 않으면 은퇴한 캐릭터를 남겨둘 이유가 없습니다.");
    }

    [Fact]
    public void 현역이나_사망자는_멘토가_될_수_없다()
    {
        var active = Rookie(Profile(20));
        Assert.Throws<ArgumentException>(() => Mentorship.From(active));
    }

    // ---------------------------------------------------------------
    // 결정론
    // ---------------------------------------------------------------

    [Fact]
    public void 같은시드면_경력이_완전히_동일하게_재현된다()
    {
        string RunCareer(ulong seed)
        {
            var rng = new DeterministicRandom(seed);
            var a = Adventurer.Recruit("R1", "재현", rng.Fork("recruit"));

            CareerSimulator.ResolveTrainingYear(a, rng);
            for (int year = 0; year < 10 && a.Status == AdventurerStatus.Active; year++)
            {
                if (year % 2 == 0) CareerSimulator.ResolveTrainingYear(a, rng);
                else CareerSimulator.ResolveDeploymentYear(a, 4, rng);
            }

            return $"{a.Age}|{a.Stats}|{a.Judgement}|{a.Status}";
        }

        Assert.Equal(RunCareer(777UL), RunCareer(777UL));
        Assert.NotEqual(RunCareer(777UL), RunCareer(778UL));
    }
}
