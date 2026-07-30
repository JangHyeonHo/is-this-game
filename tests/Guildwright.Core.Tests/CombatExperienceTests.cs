using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 실전 성장이 "무엇을 겪었는가"를 반영하는지 검증합니다.
/// <para>
/// 가설: <b>훈련은 무엇을 단련할지 고르지만, 실전은 무엇을 겪었는지가 정한다.</b>
/// 이게 성립해야 파티 편성과 전술 편성이 육성에도 영향을 미치고, 시스템이 한 바퀴 닫힙니다.
/// </para>
/// 근거: docs/04-game-design.md §5.7
/// </summary>
public class CombatExperienceTests(ITestOutputHelper output)
{
    private const ulong Seed = 6161UL;

    private static GrowthProfile Profile(int potential = 90) => new()
    {
        PeakAge = 20,
        BloomWidth = 3.0,
        Temperament = Temperament.Battleborn,
        Potential = PrimaryStats.Uniform(potential),
        DeclineAge = 40
    };

    private static Adventurer Veteran(WeaponKind kind)
    {
        var a = new Adventurer(
            "V", "실험체", PrimaryStats.Uniform(20), 50, Profile(), 20,
            WeaponAptitudes.Uniform(AptitudeGrade.B), Loadout.Single(kind));

        // 등록 첫 해 제약을 소화합니다.
        CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(1UL));
        return a;
    }

    // ---------------------------------------------------------------
    // 총량은 그대로, 방향만 바뀐다
    // ---------------------------------------------------------------

    [Fact]
    public void 균등하게_겪으면_가중치가_모두_1이다()
    {
        foreach (var kind in PrimaryStats.AllStats)
        {
            Assert.Equal(1.0, CombatExperience.Uniform.WeightOf(kind), precision: 6);
        }
    }

    [Fact]
    public void 경험은_총_성장량이_아니라_방향을_바꾼다()
    {
        // 가중치 합이 대체로 보존되어야, "어떻게 싸웠느냐"가 총량 이득이 되지 않습니다.
        var tank = CombatExperience.FromRole(WeaponKind.Shield, Row.Front);
        var mage = CombatExperience.FromRole(WeaponKind.Staff, Row.Back);

        double tankSum = PrimaryStats.AllStats.Sum(tank.WeightOf);
        double mageSum = PrimaryStats.AllStats.Sum(mage.WeightOf);

        output.WriteLine($"탱커 가중치 합 {tankSum:F2} / 마법사 가중치 합 {mageSum:F2} (균등은 6.00)");

        Assert.InRange(tankSum, 4.5, 7.5);
        Assert.InRange(mageSum, 4.5, 7.5);
    }

    // ---------------------------------------------------------------
    // 역할이 성장 방향을 정한다
    // ---------------------------------------------------------------

    [Fact]
    public void 앞에서_맞은_캐릭터는_체력과_방어가_자란다()
    {
        var tank = CombatExperience.FromRole(WeaponKind.Shield, Row.Front);

        output.WriteLine($"한손+방패 전열: {tank}");

        Assert.True(tank.WeightOf(PrimaryStat.Vitality) > 1.0);
        Assert.True(tank.WeightOf(PrimaryStat.Vitality) > 1.0);
        Assert.True(tank.WeightOf(PrimaryStat.Intellect) < 1.0);
    }

    [Fact]
    public void 마법을_쓴_캐릭터는_마력과_마공이_자란다()
    {
        var mage = CombatExperience.FromRole(WeaponKind.Staff, Row.Back);

        output.WriteLine($"지팡이 후열: {mage}");

        Assert.True(mage.WeightOf(PrimaryStat.Intellect) > 1.0);
        Assert.True(mage.WeightOf(PrimaryStat.Spirit) > 1.0);
        Assert.True(mage.WeightOf(PrimaryStat.Strength) < 1.0);
    }

    [Fact]
    public void 쓰지_않은_능력치도_최소한은_자란다()
    {
        // 0으로 두면 한 역할만 시킨 캐릭터가 다른 방면으로 영영 자라지 못해,
        // 전직이나 역할 변경이 사실상 불가능해집니다.
        var mage = CombatExperience.FromRole(WeaponKind.Staff, Row.Back);

        Assert.True(mage.WeightOf(PrimaryStat.Strength) > 0.2);
    }

    // ---------------------------------------------------------------
    // 실제 실전 연도에 반영되는가
    // ---------------------------------------------------------------

    [Fact]
    public void 역할이_다르면_실전_5년_후_전혀_다른_캐릭터가_된다()
    {
        // ★ 이게 이 시스템의 존재 이유입니다.
        // 능력치도 잠재력도 완전히 동일한 둘이, 어떻게 싸웠느냐만으로 갈라져야 합니다.
        PrimaryStats After(WeaponKind kind)
        {
            var a = Veteran(kind);
            var rng = new DeterministicRandom(Seed);

            for (int y = 0; y < 5; y++)
            {
                if (a.Status != AdventurerStatus.Active) break;
                CareerSimulator.ResolveDeploymentYear(a, 2, rng.Fork($"y:{y}"));
            }
            return a.Stats;
        }

        var tank = After(WeaponKind.Shield);
        var mage = After(WeaponKind.Staff);

        output.WriteLine($"한손+방패 실전 5년: {tank}");
        output.WriteLine($"지팡이   실전 5년: {mage}");

        Assert.True(tank.Vitality > mage.Vitality,
            "앞에서 맞은 쪽의 방어가 더 높아야 합니다.");
        Assert.True(mage.Intellect > tank.Intellect,
            "마법을 쓴 쪽의 마공이 더 높아야 합니다.");
        Assert.True(mage.Spirit > tank.Spirit);
    }

    [Fact]
    public void 실전_기록을_직접_넘기면_그대로_반영된다()
    {
        // 실제 전투를 돌린 경우, 근사가 아니라 진짜 기록으로 성장시킵니다.
        var a = Veteran(WeaponKind.Spear);

        // 이 해에는 마법만 잔뜩 썼다고 가정합니다(장비와 무관하게).
        var contribution = new CombatContribution();
        typeof(CombatContribution)
            .GetMethod("RecordDamageDealt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(contribution, [5000, true]);

        var experience = CombatExperience.From(contribution);
        output.WriteLine($"마법만 쓴 기록: {experience}");

        var before = a.Stats;
        CareerSimulator.ResolveDeploymentYear(a, 2, new DeterministicRandom(Seed), experience);
        var gain = a.Stats - before;

        output.WriteLine($"성장: {gain}");

        Assert.True(gain.Intellect > gain.Strength,
            "넘겨준 전투 기록이 성장에 반영되지 않았습니다.");
    }

    // ---------------------------------------------------------------
    // 전투가 기록을 실제로 남기는가
    // ---------------------------------------------------------------

    [Fact]
    public void 전투는_누가_무엇을_했는지_기록한다()
    {
        var rng = new DeterministicRandom(Seed);

        var party = new List<Adventurer>();
        foreach (var (kind, name) in new[]
                 {
                     (WeaponKind.Shield, "탱커"),
                     (WeaponKind.Staff, "힐러"),
                     (WeaponKind.Greatsword, "딜러")
                 })
        {
            var a = Adventurer.Recruit($"P_{name}", name, rng.Fork($"r:{name}"));
            a.Equip(WeaponSet.Primary, Hand.Right, kind);
            // 전열이 중간에 무너지면 후열이 노출되어 "전열이 더 맞는다"를 잴 수 없습니다.
            // 파티를 충분히 강하게 만들어 전열이 버티게 합니다.
            for (int y = 0; y < 7; y++) CareerSimulator.ResolveTrainingYear(a, rng.Fork($"t:{name}:{y}"));
            party.Add(a);
        }

        // 적을 전부 근접으로 고정합니다. 무작위 영입에 맡기면 적이 전원 원거리로 나와
        // 후열까지 직접 때리는 바람에 "전열이 더 맞는다"를 잴 수 없습니다.
        var enemies = Enumerable.Range(0, 3)
            .Select(i =>
            {
                var e = Adventurer.Recruit($"E{i}", $"적{i}", rng.Fork($"e:{i}"));
                e.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Greatsword);
                for (int y = 0; y < 2; y++) CareerSimulator.ResolveTrainingYear(e, rng.Fork($"et:{i}:{y}"));
                return e;
            })
            .ToList();

        var state = CombatantFactory.FormParty(party, enemies);

        // 짧게 끊어 전열이 살아있는 동안만 측정합니다.
        new BattleResolver(maxRounds: 6).Resolve(state, rng.Fork("battle"));

        Assert.True(state.LivingIn(Team.Player, Row.Front).Count > 0,
            "전열이 무너진 뒤의 피해 분포는 이 테스트의 관심사가 아닙니다.");

        foreach (var c in state.All.Where(c => c.Team == Team.Player))
        {
            output.WriteLine($"  {c.Name}: {c.Contribution}");
        }

        var tank = state.All.First(c => c.Name == "탱커");
        var healer = state.All.First(c => c.Name == "힐러");

        Assert.True(tank.Contribution.Actions > 0);
        Assert.True(tank.Contribution.TotalDamageTaken > healer.Contribution.TotalDamageTaken,
            "전열의 탱커가 후열의 힐러보다 더 맞아야 합니다.");
        Assert.True(state.All.Where(c => c.Team == Team.Player).Sum(c => c.Contribution.TotalDamageDealt) > 0);
    }

    [Fact]
    public void 전투_기록에서_만든_경험이_역할을_반영한다()
    {
        var rng = new DeterministicRandom(Seed);

        var party = new List<Adventurer>();
        foreach (var kind in new[] { WeaponKind.Shield, WeaponKind.Staff })
        {
            var a = Adventurer.Recruit($"P{kind}", kind.ToKorean(), rng.Fork($"r:{kind}"));
            a.Equip(WeaponSet.Primary, Hand.Right, kind);
            for (int y = 0; y < 3; y++) CareerSimulator.ResolveTrainingYear(a, rng.Fork($"t:{kind}:{y}"));
            party.Add(a);
        }

        var enemies = Enumerable.Range(0, 2)
            .Select(i => Adventurer.Recruit($"E{i}", $"적{i}", rng.Fork($"e:{i}")))
            .ToList();
        foreach (var e in enemies) CareerSimulator.ResolveTrainingYear(e, rng.Fork($"et:{e.Id}"));

        var state = CombatantFactory.FormParty(party, enemies);
        new BattleResolver().Resolve(state, rng.Fork("battle"));

        var tankExp = CombatExperience.From(state.All.First(c => c.Loadout.Holding(WeaponKind.Shield)).Contribution);
        var mageExp = CombatExperience.From(state.All.First(c => c.Loadout.Holding(WeaponKind.Staff)).Contribution);

        output.WriteLine($"탱커  실제 경험: {tankExp}");
        output.WriteLine($"마법사 실제 경험: {mageExp}");

        // 물리 방어만 콕 집어 비교하면 안 됩니다 — 적이 마법사뿐이면 물리 피해를 아예 안 맞습니다.
        // 중요한 건 "방어 계열이 자라는가"이지 어느 방어인가가 아닙니다.
        double Defensive(CombatExperience e) =>
            e.WeightOf(PrimaryStat.Vitality) + e.WeightOf(PrimaryStat.Vitality) + e.WeightOf(PrimaryStat.Spirit);

        output.WriteLine($"방어 계열 가중치 합 · 탱커 {Defensive(tankExp):F2} / 마법사 {Defensive(mageExp):F2}");

        Assert.True(Defensive(tankExp) > Defensive(mageExp),
            "전열에서 맞아준 쪽이 방어 계열로 자라지 않으면, 역할이 육성에 반영되지 않는 것입니다.");

        Assert.True(mageExp.WeightOf(PrimaryStat.Intellect) > tankExp.WeightOf(PrimaryStat.Intellect));
    }
}
