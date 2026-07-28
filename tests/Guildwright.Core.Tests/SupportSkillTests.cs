using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 비전투 역량과 의뢰 요구 조건을 검증합니다.
/// <para>
/// 가설: <b>전투력만으로 캐릭터를 평가하지 않게 된다.</b>
/// 전투는 못하지만 함정을 기가 막히게 찾는 아이를 데리고 있을 이유가 있어야 합니다.
/// </para>
/// <para>
/// 이 시스템은 <b>던전 탐험 층을 만들지 않고</b> 의뢰 해석에 흡수됩니다.
/// 스코프 방어선을 지키면서 같은 느낌을 내는 것이 목표입니다.
/// </para>
/// 근거: docs/00-charter.md §4, docs/04-game-design.md §5.8
/// </summary>
public class SupportSkillTests(ITestOutputHelper output)
{
    private const ulong Seed = 9090UL;

    private static GrowthProfile Profile(PrimaryStats? potential = null) => new()
    {
        PeakAge = 20,
        BloomWidth = 3.0,
        Temperament = Temperament.Balanced,
        Potential = potential ?? PrimaryStats.Uniform(70),
        DeclineAge = 45
    };

    private static Adventurer Veteran(PrimaryStats? stats = null)
    {
        var a = new Adventurer(
            "S", "보조병", stats ?? PrimaryStats.Uniform(40), 50, Profile(), 20,
            WeaponAptitudes.Uniform(AptitudeGrade.B), WeaponStyle.Bow, WeaponClass.Pierce);

        CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(1UL));
        return a;
    }

    private static SupportSkillSet Skilled(SupportSkill skill, int years, PrimaryStats stats)
    {
        var set = new SupportSkillSet();
        for (int i = 0; i < years; i++) set.AdvanceYear(skill, stats);
        return set;
    }

    // ---------------------------------------------------------------
    // 역량이 쌓인다
    // ---------------------------------------------------------------

    [Fact]
    public void 맡은_역할의_역량이_크게_늘고_나머지는_조금_는다()
    {
        var a = Veteran();
        var rng = new DeterministicRandom(Seed);

        for (int y = 0; y < 5; y++)
        {
            if (a.Status != AdventurerStatus.Active) break;
            CareerSimulator.ResolveDeploymentYear(a, 2, rng.Fork($"y:{y}"), supportRole: SupportSkill.TrapSense);
        }

        output.WriteLine($"함정 감지 5년: {a.Support}");

        Assert.True(a.Support[SupportSkill.TrapSense] > a.Support[SupportSkill.Gathering] * 2,
            "맡은 역할이 뚜렷하게 앞서지 않으면 역할 배정이 무의미해집니다.");

        Assert.True(a.Support[SupportSkill.Gathering] > 0,
            "어깨너머로도 전혀 안 늘면 역할 변경이 사실상 불가능해집니다.");
    }

    [Fact]
    public void 비전투_역량은_훈련이_아니라_실전에서_는다()
    {
        // 훈련장에서는 함정을 만날 일이 없습니다.
        var a = Veteran();
        var rng = new DeterministicRandom(Seed);

        for (int y = 0; y < 5; y++) CareerSimulator.ResolveTrainingYear(a, rng.Fork($"t:{y}"));

        Assert.Equal(0, a.Support[SupportSkill.TrapSense]);
    }

    [Fact]
    public void 능력치에_따라_잘_맞는_역할이_갈린다()
    {
        // 비전투 역량에 별도 적성 랜덤을 두지 않는 대신, 기존 능력치가 성장 속도를 좌우합니다.
        // 랜덤 축을 늘리지 않으면서 캐릭터마다 어울리는 역할이 생깁니다.
        var nimble = new PrimaryStats(Strength: 20, Agility: 95, Finesse: 20, Vitality: 20, Intellect: 20, Spirit: 20);
        var sturdy = new PrimaryStats(Strength: 95, Agility: 20, Finesse: 20, Vitality: 95, Intellect: 20, Spirit: 20);

        int nimbleScouting = Skilled(SupportSkill.Scouting, 4, nimble)[SupportSkill.Scouting];
        int sturdyScouting = Skilled(SupportSkill.Scouting, 4, sturdy)[SupportSkill.Scouting];

        int nimblePorter = Skilled(SupportSkill.Portering, 4, nimble)[SupportSkill.Portering];
        int sturdyPorter = Skilled(SupportSkill.Portering, 4, sturdy)[SupportSkill.Portering];

        output.WriteLine($"척후 4년 · 민첩형 {nimbleScouting} / 건장형 {sturdyScouting}");
        output.WriteLine($"운반 4년 · 민첩형 {nimblePorter} / 건장형 {sturdyPorter}");

        Assert.True(nimbleScouting > sturdyScouting);
        Assert.True(sturdyPorter > nimblePorter);
    }

    // ---------------------------------------------------------------
    // 의뢰가 역량을 요구한다
    // ---------------------------------------------------------------

    [Fact]
    public void 함정_감지가_높으면_사고_위험이_줄어든다()
    {
        var contract = new Contract("폐광 소탕", ContractKind.Combat, 5,
            new Dictionary<SupportSkill, double> { [SupportSkill.TrapSense] = 1.0 });

        var stats = PrimaryStats.Uniform(50);
        var none = ContractResolver.Evaluate(contract, [new SupportSkillSet()]);
        var expert = ContractResolver.Evaluate(contract, [Skilled(SupportSkill.TrapSense, 12, stats)]);

        output.WriteLine($"위험 배율 · 역량 없음 {none.RiskMultiplier:F2} / 숙련자 동행 {expert.RiskMultiplier:F2}");

        Assert.True(expert.RiskMultiplier < none.RiskMultiplier,
            "함정 감지가 위험을 줄이지 않으면 그 역량을 키울 이유가 없습니다.");
    }

    [Fact]
    public void 요구하지_않는_의뢰에서는_역량이_소용없다()
    {
        // 모든 의뢰에 모든 역량이 통하면 "무조건 데려가는 만능 보조병"이 생겨
        // 편성이 다시 한 줄 세우기가 됩니다.
        var plainContract = new Contract("들판 순찰", ContractKind.Combat, 3,
            new Dictionary<SupportSkill, double>());

        var stats = PrimaryStats.Uniform(50);
        var expert = ContractResolver.Evaluate(plainContract, [Skilled(SupportSkill.TrapSense, 12, stats)]);

        Assert.Equal(1.0, expert.RiskMultiplier, precision: 6);
    }

    [Fact]
    public void 채집_역량이_높으면_재료_의뢰의_보수가_오른다()
    {
        var contract = new Contract("은광맥 채굴", ContractKind.Gathering, 3,
            new Dictionary<SupportSkill, double>
            {
                [SupportSkill.Gathering] = 1.0,
                [SupportSkill.Portering] = 0.6
            });

        var stats = PrimaryStats.Uniform(50);
        var none = ContractResolver.Evaluate(contract, [new SupportSkillSet()]);
        var crew = ContractResolver.Evaluate(contract,
        [
            Skilled(SupportSkill.Gathering, 12, stats),
            Skilled(SupportSkill.Portering, 12, stats)
        ]);

        output.WriteLine($"보수 배율 · 역량 없음 {none.IncomeMultiplier:F2} / 채집단 {crew.IncomeMultiplier:F2}");

        Assert.True(crew.IncomeMultiplier > none.IncomeMultiplier * 1.15);
    }

    [Fact]
    public void 운반_역량이_높으면_회복약을_더_들고_간다()
    {
        var contract = Contract.Combat("장거리 원정", 5);
        var stats = PrimaryStats.Uniform(60);

        var solo = ContractResolver.Evaluate(contract, [new SupportSkillSet()]);
        var porters = ContractResolver.Evaluate(contract,
        [
            Skilled(SupportSkill.Portering, 12, stats),
            Skilled(SupportSkill.Portering, 12, stats),
            Skilled(SupportSkill.Portering, 12, stats)
        ]);

        output.WriteLine($"추가 회복약 · 없음 {solo.ExtraPotions} / 짐꾼 3명 {porters.ExtraPotions}");

        Assert.True(porters.ExtraPotions > solo.ExtraPotions);
    }

    [Fact]
    public void 함정_감지는_최고_숙련자_기준이고_운반은_합산이다()
    {
        // 함정을 찾는 데 다섯 명이 다 필요하진 않습니다. 짐은 나눠 들 수 있고요.
        var trapContract = new Contract("함정 지대", ContractKind.Combat, 5,
            new Dictionary<SupportSkill, double> { [SupportSkill.TrapSense] = 1.0 });
        var haulContract = new Contract("대량 운송", ContractKind.Gathering, 4,
            new Dictionary<SupportSkill, double> { [SupportSkill.Portering] = 1.0 });

        var stats = PrimaryStats.Uniform(50);
        var one = Skilled(SupportSkill.TrapSense, 12, stats);
        var oneHauler = Skilled(SupportSkill.Portering, 12, stats);
        var weakHauler = Skilled(SupportSkill.Portering, 3, stats);

        // 함정: 숙련자 1명 vs 숙련자 1명 + 초보 2명 → 차이 없어야 함
        var soloTrap = ContractResolver.Evaluate(trapContract, [one]);
        var groupTrap = ContractResolver.Evaluate(trapContract, [one, new SupportSkillSet(), new SupportSkillSet()]);

        Assert.Equal(soloTrap.RiskMultiplier, groupTrap.RiskMultiplier, precision: 6);

        // 운반: 인원이 늘면 총량이 늘어야 함
        var soloHaul = ContractResolver.Evaluate(haulContract, [oneHauler]);
        var groupHaul = ContractResolver.Evaluate(haulContract, [oneHauler, weakHauler, weakHauler]);

        output.WriteLine($"운반 보수 배율 · 1명 {soloHaul.IncomeMultiplier:F3} / 3명 {groupHaul.IncomeMultiplier:F3}");
        Assert.True(groupHaul.IncomeMultiplier > soloHaul.IncomeMultiplier);
    }

    // ---------------------------------------------------------------
    // ★ 전투력이 낮은 캐릭터의 자리
    // ---------------------------------------------------------------

    [Fact]
    public void 채집_의뢰는_전투가_약해도_해낼_수_있다()
    {
        // 이게 이 시스템의 존재 이유입니다.
        int Income(ContractKind kind)
        {
            // 전투력이 형편없는 캐릭터.
            var weak = new Adventurer(
                "W", "약골", PrimaryStats.Uniform(18), 45, Profile(), 20,
                WeaponAptitudes.Uniform(AptitudeGrade.C), WeaponStyle.Bow, WeaponClass.Pierce);
            CareerSimulator.ResolveTrainingYear(weak, new DeterministicRandom(1UL));

            var contract = new Contract("의뢰", kind, 4, new Dictionary<SupportSkill, double>());
            var record = CareerSimulator.ResolveDeploymentYear(
                weak, 4, new DeterministicRandom(Seed), contract: contract);

            return record.Income;
        }

        int combat = Income(ContractKind.Combat);
        int gathering = Income(ContractKind.Gathering);

        output.WriteLine($"전투력 126인 캐릭터의 난이도 4 의뢰 보수 · 전투형 {combat} / 채집형 {gathering}");

        Assert.True(gathering > combat,
            "전투가 약한 캐릭터가 채집 의뢰에서도 손해면, 그런 캐릭터를 키울 이유가 사라집니다.");
    }

    [Fact]
    public void 채집_의뢰는_사고_위험도_낮다()
    {
        double DeathRate(ContractKind kind)
        {
            int deaths = 0;
            const int trials = 1_500;

            for (int t = 0; t < trials; t++)
            {
                var weak = new Adventurer(
                    "W", "약골", PrimaryStats.Uniform(18), 30, Profile(), 20,
                    WeaponAptitudes.Uniform(AptitudeGrade.C), WeaponStyle.Bow, WeaponClass.Pierce);
                CareerSimulator.ResolveTrainingYear(weak, new DeterministicRandom(1UL));

                var contract = new Contract("의뢰", kind, 7, new Dictionary<SupportSkill, double>());
                CareerSimulator.ResolveDeploymentYear(
                    weak, 7, new DeterministicRandom(Seed).Fork($"t:{t}"), contract: contract);

                if (weak.Status == AdventurerStatus.Dead) deaths++;
            }

            return (double)deaths / trials;
        }

        double combat = DeathRate(ContractKind.Combat);
        double gathering = DeathRate(ContractKind.Gathering);

        output.WriteLine($"난이도 7 사망률 · 전투형 {combat:P2} / 채집형 {gathering:P2}");

        Assert.True(gathering < combat);
    }

    [Fact]
    public void 함정_감지_동행자가_있으면_실제로_덜_죽는다()
    {
        double DeathRate(bool withScout)
        {
            var contract = new Contract("함정투성이 유적", ContractKind.Combat, 8,
                new Dictionary<SupportSkill, double> { [SupportSkill.TrapSense] = 1.0 });

            var party = withScout
                ? new List<SupportSkillSet> { Skilled(SupportSkill.TrapSense, 15, PrimaryStats.Uniform(60)) }
                : [new SupportSkillSet()];

            var support = ContractResolver.Evaluate(contract, party);

            int deaths = 0;
            const int trials = 1_500;

            for (int t = 0; t < trials; t++)
            {
                var fighter = new Adventurer(
                    "F", "전사", PrimaryStats.Uniform(30), 40, Profile(), 22,
                    WeaponAptitudes.Uniform(AptitudeGrade.B), WeaponStyle.TwoHanded, WeaponClass.Axe);
                CareerSimulator.ResolveTrainingYear(fighter, new DeterministicRandom(1UL));

                CareerSimulator.ResolveDeploymentYear(
                    fighter, 8, new DeterministicRandom(Seed).Fork($"t:{t}"),
                    contract: contract, support: support);

                if (fighter.Status == AdventurerStatus.Dead) deaths++;
            }

            return (double)deaths / trials;
        }

        double without = DeathRate(false);
        double with = DeathRate(true);

        output.WriteLine($"난이도 8 사망률 · 함정 감지 없음 {without:P2} / 숙련자 동행 {with:P2}");

        Assert.True(with < without,
            "보조 역량이 목숨을 구하지 않으면, 전투 못하는 캐릭터를 파티에 넣을 이유가 없습니다.");
    }
}
