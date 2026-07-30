using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 회피·치명타·방어를 검증합니다.
/// <para>
/// 가설 1: <b>속도(민첩)가 행동 순서 말고도 할 일이 있어야 한다.</b>
/// 7스탯 중 하나만 얇으면 육성에서 그 축을 고를 이유가 약해집니다.
/// </para>
/// <para>
/// 가설 2: <b>자동 전투에는 분산이 필요하다.</b> 능력치 차이가 곧 결과면
/// 약자가 강자를 이기는 순간이 없고, 지켜볼 이유가 사라집니다.
/// </para>
/// 근거: docs/04-game-design.md §4.7
/// </summary>
public class CritAndEvasionTests(ITestOutputHelper output)
{
    private const ulong Seed = 1212UL;

    private static PrimaryStats Stats(int agility = 30, int finesse = 30) => new(
        Strength: 40, Agility: agility, Finesse: finesse,
        Vitality: 40, Intellect: 20, Spirit: 25);

    private static Combatant Make(
        string id, Team team, WeaponKind weapon = WeaponKind.Sword,
        int agility = 30, int finesse = 30, DerivedBonuses? bonuses = null)
        => new(id, id, team, Stats(agility, finesse), 60, Loadout.Single(weapon), 1.0, Row.Front,
               TestParty.NaiveTactics, potions: 0, bonuses: bonuses);

    /// <summary>공격을 여러 번 해결하고 회피·치명타 빈도를 셉니다.</summary>
    private static (double EvasionRate, double CritRate, double AverageDamage) Sample(
        Combatant attacker, Combatant defender, int trials = 20_000)
    {
        var rng = new DeterministicRandom(Seed);
        int evaded = 0, crits = 0;
        long damage = 0;

        for (int i = 0; i < trials; i++)
        {
            var result = DamageModel.ResolveAttack(attacker, defender, rng);
            if (result.Evaded) evaded++;
            if (result.Critical) crits++;
            damage += result.Damage;
        }

        return ((double)evaded / trials, (double)crits / trials, (double)damage / trials);
    }

    // ---------------------------------------------------------------
    // 회피 — 민첩이 하는 두 번째 일
    // ---------------------------------------------------------------

    [Fact]
    public void 민첩이_높으면_더_잘_피한다()
    {
        var attacker = Make("A", Team.Player);
        var slow = Make("S", Team.Enemy, agility: 10);
        var quick = Make("Q", Team.Enemy, agility: 95);

        double slowRate = Sample(attacker, slow).EvasionRate;
        double quickRate = Sample(attacker, quick).EvasionRate;

        output.WriteLine($"회피율 · 민첩 10: {slowRate:P1} / 민첩 95: {quickRate:P1}");

        Assert.True(quickRate > slowRate * 1.5,
            "민첩이 회피로 이어지지 않으면, 민첩은 행동 순서만 바꾸는 얇은 스탯으로 남습니다.");
    }

    [Fact]
    public void 상대보다_빠를수록_더_잘_피한다()
    {
        // 절대 회피율이 아니라 속도 차이가 좌우해야 자연스럽습니다.
        // 느린 석궁병이 재빠른 검객을 맞히기 어려운 게 당연합니다.
        var defender = Make("D", Team.Enemy, agility: 60);

        double vsSlow = Sample(Make("Slow", Team.Player, WeaponKind.Crossbow, agility: 15), defender).EvasionRate;
        double vsFast = Sample(Make("Fast", Team.Player, WeaponKind.Sword, agility: 90), defender).EvasionRate;

        output.WriteLine($"민첩 60 대상의 회피율 · 느린 공격자 상대 {vsSlow:P1} / 빠른 공격자 상대 {vsFast:P1}");

        Assert.True(vsSlow > vsFast);
    }

    [Fact]
    public void 회피율에는_상한이_있다()
    {
        // 자동 전투에서 "빗나감"이 연달아 뜨면 답답합니다.
        // 회피는 가끔 터지는 반전이어야지 일상이면 안 됩니다.
        var attacker = Make("A", Team.Player, WeaponKind.Crossbow, agility: 5);
        var evasive = Make("E", Team.Enemy, WeaponKind.Sword, agility: 100);
        evasive.BeginDefending();

        double rate = Sample(attacker, evasive).EvasionRate;
        output.WriteLine($"극단적으로 유리한 조건의 회피율: {rate:P1}");

        Assert.True(rate <= DamageModel.MaxEvasionChance + 0.01);
    }

    [Fact]
    public void 방어_태세는_피해도_줄이고_회피도_돕는다()
    {
        var attacker = Make("A", Team.Player);

        var plain = Make("P", Team.Enemy);
        var guarded = Make("G", Team.Enemy);
        guarded.BeginDefending();

        var plainSample = Sample(attacker, plain);
        var guardedSample = Sample(attacker, guarded);

        output.WriteLine($"평균 피해 · 평상시 {plainSample.AverageDamage:F1} / 방어 태세 {guardedSample.AverageDamage:F1}");
        output.WriteLine($"회피율   · 평상시 {plainSample.EvasionRate:P1} / 방어 태세 {guardedSample.EvasionRate:P1}");

        Assert.True(guardedSample.AverageDamage < plainSample.AverageDamage * 0.7);
        Assert.True(guardedSample.EvasionRate > plainSample.EvasionRate);
    }

    // ---------------------------------------------------------------
    // 치명타 — 기교가 하는 일, 그리고 스타일 차별화
    // ---------------------------------------------------------------

    [Fact]
    public void 기교가_높으면_치명타가_자주_난다()
    {
        var defender = Make("D", Team.Enemy);

        double clumsy = Sample(Make("C", Team.Player, finesse: 5), defender).CritRate;
        double deft = Sample(Make("F", Team.Player, finesse: 100), defender).CritRate;

        output.WriteLine($"치명타율 · 기교 5: {clumsy:P1} / 기교 100: {deft:P1}");

        Assert.True(deft > clumsy * 1.5);
    }

    [Fact]
    public void 치명타_특성은_숙련_패시브에서_갈린다()
    {
        // ⚠️ 예전에는 무기 스타일이 치명타 확률·배율을 물고 있었습니다
        // (쌍수는 자주, 양손은 크게). 지금은 그게 무기가 아니라 숙련 패시브입니다 —
        // 초보가 쌍수를 들었다고 바로 자잘하게 잘 터지는 건 이상하기 때문입니다.
        // 근거: docs/08-design-revision.md §16.2
        var plain = Make("P", Team.Player);

        var twin = new Combatant("TW", "쌍수 숙련자", Team.Player, Stats(30, 30), 60,
            Loadout.Pair(WeaponKind.Sword, WeaponKind.Sword), 1.0, Row.Front,
            TestParty.NaiveTactics, potions: 0,
            passives: [Guildwright.Core.Skills.SkillId.TwinStrike]);

        var heavy = new Combatant("HV", "양손 숙련자", Team.Player, Stats(30, 30), 60,
            Loadout.Single(WeaponKind.Greatsword), 1.0, Row.Front,
            TestParty.NaiveTactics, potions: 0,
            passives: [Guildwright.Core.Skills.SkillId.HeavyBlow]);

        output.WriteLine($"기본 확률 {plain.CritChance:P1} 배율 {plain.CritMultiplier:F2}");
        output.WriteLine($"쌍수 숙달 확률 {twin.CritChance:P1} 배율 {twin.CritMultiplier:F2}");
        output.WriteLine($"양손 숙달 확률 {heavy.CritChance:P1} 배율 {heavy.CritMultiplier:F2}");

        // 쌍수 숙달은 확률을, 양손 숙달은 배율을 올립니다.
        Assert.True(twin.CritChance > plain.CritChance, "쌍수 숙달이 확률을 안 올립니다.");
        Assert.True(heavy.CritMultiplier > twin.CritMultiplier, "양손 숙달이 배율을 안 올립니다.");
        Assert.True(twin.CritChance > heavy.CritChance, "확률과 배율이 반대로 배치되지 않았습니다.");
    }

    [Fact]
    public void 치명타는_실제로_피해를_키운다()
    {
        var attacker = Make("A", Team.Player, WeaponKind.Greatsword, finesse: 100);
        var defender = Make("D", Team.Enemy, agility: 5);

        var rng = new DeterministicRandom(Seed);
        long normal = 0, critical = 0;
        int normalCount = 0, criticalCount = 0;

        for (int i = 0; i < 20_000; i++)
        {
            var r = DamageModel.ResolveAttack(attacker, defender, rng);
            if (r.Evaded) continue;
            if (r.Critical) { critical += r.Damage; criticalCount++; }
            else { normal += r.Damage; normalCount++; }
        }

        double avgNormal = (double)normal / normalCount;
        double avgCrit = (double)critical / criticalCount;

        output.WriteLine($"양손 평균 피해 · 일반 {avgNormal:F1} / 치명타 {avgCrit:F1} (실측 배율 {avgCrit / avgNormal:F2})");

        Assert.True(avgCrit > avgNormal * 1.6);
    }

    // ---------------------------------------------------------------
    // 파생 보정 — 겪은 것이 직접 붙는다
    // ---------------------------------------------------------------

    [Fact]
    public void 파생_보정은_원천_능력치_없이도_수치를_올린다()
    {
        var stats = Stats();

        var bonuses = new DerivedBonuses();
        typeof(DerivedBonuses)
            .GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(bonuses, [DerivedStat.CritChance, 0.05]);

        double plain = DerivedStats.CritChance(stats);
        double boosted = DerivedStats.CritChance(stats, bonuses);

        output.WriteLine($"치명타율 · 보정 없음 {plain:P1} / 보정 +5%p {boosted:P1}");

        Assert.True(boosted > plain);
    }

    [Fact]
    public void 실전에서_많이_맞으면_물리_방어_보정이_붙는다()
    {
        // "계속 맞다 보면 몸이 단단해진다"
        var growth = new GrowthProfile
        {
            PeakAge = 20,
            BloomWidth = 3.0,
            Temperament = Temperament.Balanced,
            Potential = PrimaryStats.Uniform(80),
            DeclineAge = 45
        };

        var tank = new Adventurer(
            "T", "탱커", PrimaryStats.Uniform(30), 50, growth, 20,
            WeaponAptitudes.Uniform(AptitudeGrade.B));

        var rng = new DeterministicRandom(Seed);
        CareerSimulator.ResolveTrainingYear(tank, rng.Fork("warm"));

        Assert.Equal(0.0, tank.Bonuses[DerivedStat.PhysicalGuard], precision: 6);

        for (int y = 0; y < 5 && tank.Status == AdventurerStatus.Active; y++)
        {
            CareerSimulator.ResolveDeploymentYear(tank, 2, rng.Fork($"y:{y}"));
        }

        output.WriteLine($"전열 5년 후 파생 보정: {tank.Bonuses}");

        Assert.True(tank.Bonuses[DerivedStat.PhysicalGuard] > 0.0,
            "앞에서 계속 맞았는데 몸이 단단해지지 않으면, 파생 보정이라는 개념이 무의미해집니다.");
    }

    [Fact]
    public void 파생_보정에는_상한이_있다()
    {
        // 보정만으로 캐릭터가 완성되면 원천 능력치를 키울 이유가 사라집니다.
        var bonuses = new DerivedBonuses();
        var add = typeof(DerivedBonuses)
            .GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        for (int i = 0; i < 1_000; i++) add.Invoke(bonuses, [DerivedStat.CritChance, 1.0]);

        output.WriteLine($"1000회 누적 후 치명타 보정: {bonuses[DerivedStat.CritChance]:P1}");

        Assert.True(bonuses[DerivedStat.CritChance] <= 0.12 + 1e-9);
    }

    // ---------------------------------------------------------------
    // 자동 전투의 분산
    // ---------------------------------------------------------------

    [Fact]
    public void 회피와_치명타가_전투_결과에_분산을_만든다()
    {
        // 능력치 차이가 곧 결과면 약자가 강자를 이기는 순간이 없어
        // 자동 전투를 지켜볼 이유가 사라집니다.
        var results = new List<int>();

        for (int t = 0; t < 300; t++)
        {
            var attacker = Make("A", Team.Player, WeaponKind.Sword, agility: 60, finesse: 70);
            var defender = Make("D", Team.Enemy, agility: 60, finesse: 30);

            var rng = new DeterministicRandom(Seed).Fork($"t:{t}");
            int total = 0;
            for (int i = 0; i < 10; i++) total += DamageModel.ResolveAttack(attacker, defender, rng).Damage;
            results.Add(total);
        }

        results.Sort();
        int low = results[14], high = results[^15];

        output.WriteLine($"10회 공격 누적 피해 · 하위5% {low} / 중앙 {results[150]} / 상위5% {high}");

        Assert.True(high > low * 1.25,
            "결과가 거의 고정되면 자동 전투를 지켜볼 이유가 없습니다.");
    }
}
