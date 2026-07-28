using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 무기 시스템의 설계 가설을 검증합니다.
/// <para>
/// 가설 1: <b>적성은 능력치와 상관관계를 갖되 결정적이지 않다.</b>
/// 완전 독립이면 모순 캐릭터(마공 최고인데 대검 적성)가 나와 불량품이 되고,
/// 완전 종속이면 굴릴 이유가 없습니다.
/// </para>
/// <para>
/// 가설 2: <b>무기는 능력치의 중복이 아니라 파티 역할을 정한다.</b>
/// </para>
/// 근거: docs/04-game-design.md §3.5
/// </summary>
public class WeaponSystemTests(ITestOutputHelper output)
{
    private const ulong Seed = 8080UL;

    private static StatBlock MageStats => new(
        Vitality: 40, Mana: 95, Attack: 30, Defense: 35,
        MagicAttack: 95, MagicDefense: 80, Speed: 45);

    private static StatBlock WarriorStats => new(
        Vitality: 90, Mana: 25, Attack: 95, Defense: 85,
        MagicAttack: 20, MagicDefense: 30, Speed: 50);

    // ---------------------------------------------------------------
    // 적성 — 상관관계
    // ---------------------------------------------------------------

    [Fact]
    public void 마법형_능력치는_지팡이_적성이_높게_나오는_경향이_있다()
    {
        int staffBeatsTwoHanded = 0;
        const int trials = 500;

        for (int i = 0; i < trials; i++)
        {
            var aptitudes = WeaponAptitudes.Roll(MageStats, new DeterministicRandom(Seed).Fork($"m:{i}"));
            if (aptitudes[WeaponStyle.Staff] > aptitudes[WeaponStyle.TwoHanded]) staffBeatsTwoHanded++;
        }

        double rate = (double)staffBeatsTwoHanded / trials;
        output.WriteLine($"마법형 능력치 · 지팡이 적성 > 양손 적성인 비율: {rate:P1}");

        Assert.True(rate > 0.85,
            "능력치와 적성이 상관없으면 '마공 최고인데 대검 적성'같은 불량품이 양산됩니다.");
    }

    [Fact]
    public void 전사형_능력치는_지팡이_적성이_낮게_나오는_경향이_있다()
    {
        int lowStaff = 0;
        const int trials = 500;

        for (int i = 0; i < trials; i++)
        {
            var aptitudes = WeaponAptitudes.Roll(WarriorStats, new DeterministicRandom(Seed).Fork($"w:{i}"));
            if (aptitudes[WeaponStyle.Staff] <= AptitudeGrade.C) lowStaff++;
        }

        double rate = (double)lowStaff / trials;
        output.WriteLine($"전사형 능력치 · 지팡이 적성 C 이하 비율: {rate:P1}");

        Assert.True(rate > 0.7);
    }

    [Fact]
    public void 능력치가_평범하면_어떤_재능이_나올지_예측할_수_없다()
    {
        // 여기서 말하는 "의외성"은 마공 20짜리가 지팡이 S를 받는 게 아닙니다.
        // 그건 발견이 아니라 앞서 배제하기로 한 모순 캐릭터입니다.
        // 진짜 발견은 <b>비슷하게 그럴듯한 스타일들 사이에서 예상 밖이 1등을 하는 것</b>입니다.
        var wellRounded = new StatBlock(
            Vitality: 62, Mana: 55, Attack: 64, Defense: 58,
            MagicAttack: 57, MagicDefense: 56, Speed: 63);

        var bestCounts = new Dictionary<WeaponStyle, int>();
        const int trials = 500;

        for (int i = 0; i < trials; i++)
        {
            var aptitudes = WeaponAptitudes.Roll(wellRounded, new DeterministicRandom(Seed).Fork($"r:{i}"));
            var best = aptitudes.Best;
            bestCounts[best] = bestCounts.GetValueOrDefault(best) + 1;
        }

        foreach (var (style, count) in bestCounts.OrderByDescending(kv => kv.Value))
        {
            output.WriteLine($"  {style.ToKorean(),-8} 최고 적성으로 나온 횟수: {count,3} ({(double)count / trials:P1})");
        }

        Assert.True(bestCounts.Count >= 3,
            $"평범한 능력치인데 최고 적성이 {bestCounts.Count}종류밖에 안 나옵니다. " +
            "결과가 능력치만으로 정해지면 적성을 굴릴 이유가 없습니다.");
    }

    [Fact]
    public void 능력치가_극단적이면_적성이_사실상_정해진다()
    {
        // 위 테스트의 반대쪽. 마공 20짜리 전사가 지팡이 재능을 갖는 일은 없어야 합니다.
        // 발견의 재미와 모순 캐릭터 방지는 양립해야 하고, 그 경계가 능력치의 극단성입니다.
        var bestCounts = new HashSet<WeaponStyle>();

        for (int i = 0; i < 500; i++)
        {
            bestCounts.Add(WeaponAptitudes.Roll(MageStats, new DeterministicRandom(Seed).Fork($"m:{i}")).Best);
        }

        output.WriteLine($"마법형 능력치의 최고 적성 종류: {string.Join(", ", bestCounts.Select(s => s.ToKorean()))}");

        Assert.True(bestCounts.Count <= 2);
        Assert.Contains(WeaponStyle.Staff, bestCounts);
    }

    [Fact]
    public void 모든_캐릭터는_최소_하나의_무기에_재능이_있다()
    {
        // ★ 안전장치. 5년 키워서 알아낸 게 "얘는 못 쓴다"면 긴장이 아니라 처벌입니다.
        var mediocre = StatBlock.Uniform(30);

        for (int i = 0; i < 1_000; i++)
        {
            var aptitudes = WeaponAptitudes.Roll(mediocre, new DeterministicRandom(Seed).Fork($"g:{i}"));
            var best = aptitudes.All.Max(kv => kv.Value);

            Assert.True(best >= AptitudeGrade.B,
                $"{i}번째 굴림에서 모든 적성이 B 미만입니다 ({aptitudes}). 완전한 하자품이 나오면 안 됩니다.");
        }
    }

    // ---------------------------------------------------------------
    // 숙련도 — 랜덤이 아니라 이력
    // ---------------------------------------------------------------

    [Fact]
    public void 무기를_들고_보낸_햇수만큼_숙련도가_쌓인다()
    {
        var adventurer = Recruit(WarriorStats, WeaponStyle.TwoHanded);
        var rng = new DeterministicRandom(Seed);

        Assert.Equal(0, adventurer.Proficiency[WeaponStyle.TwoHanded]);

        for (int i = 0; i < 5; i++)
        {
            CareerSimulator.ResolveTrainingYear(adventurer, rng.Fork($"y:{i}"));
        }

        output.WriteLine($"양손 5년: 숙련도 {adventurer.Proficiency[WeaponStyle.TwoHanded]}");
        Assert.True(adventurer.Proficiency[WeaponStyle.TwoHanded] > 0);
    }

    [Fact]
    public void 적성이_높으면_숙련도가_빨리_오른다()
    {
        int ProficiencyAfter(AptitudeGrade grade)
        {
            var adventurer = new Adventurer(
                "P", "실험체", StatBlock.Uniform(30), 40, BasicGrowth(), 18,
                WeaponAptitudes.Uniform(grade), WeaponStyle.Bow, WeaponClass.Pierce);

            var rng = new DeterministicRandom(Seed);
            for (int i = 0; i < 5; i++) CareerSimulator.ResolveTrainingYear(adventurer, rng.Fork($"y:{i}"));

            return adventurer.Proficiency[WeaponStyle.Bow];
        }

        int low = ProficiencyAfter(AptitudeGrade.E);
        int high = ProficiencyAfter(AptitudeGrade.S);

        output.WriteLine($"활 5년 숙련도 · 적성 E: {low} / 적성 S: {high}");

        Assert.True(high > low * 2,
            "적성이 숙련 속도를 뚜렷하게 바꾸지 않으면 적성 시스템이 장식이 됩니다.");
    }

    [Fact]
    public void 실전이_훈련보다_무기를_빨리_가르친다()
    {
        int Proficiency(YearActivity activity)
        {
            var a = Recruit(WarriorStats, WeaponStyle.Polearm);
            var rng = new DeterministicRandom(Seed);

            CareerSimulator.ResolveTrainingYear(a, rng.Fork("warm"));
            int before = a.Proficiency[WeaponStyle.Polearm];

            if (activity == YearActivity.Training) CareerSimulator.ResolveTrainingYear(a, rng.Fork("y"));
            else CareerSimulator.ResolveDeploymentYear(a, 1, rng.Fork("y"));

            return a.Proficiency[WeaponStyle.Polearm] - before;
        }

        int training = Proficiency(YearActivity.Training);
        int deployment = Proficiency(YearActivity.Deployment);

        output.WriteLine($"창 숙련도 1년 상승 · 훈련 {training} / 실전 {deployment}");

        Assert.True(deployment > training,
            "실전이 무기를 더 빨리 가르치지 않으면, 위험을 감수할 이유가 하나 줄어듭니다.");
    }

    [Fact]
    public void 무기를_바꿔도_예전_숙련도는_사라지지_않는다()
    {
        var adventurer = Recruit(WarriorStats, WeaponStyle.TwoHanded);
        var rng = new DeterministicRandom(Seed);

        for (int i = 0; i < 4; i++) CareerSimulator.ResolveTrainingYear(adventurer, rng.Fork($"a:{i}"));
        int twoHanded = adventurer.Proficiency[WeaponStyle.TwoHanded];

        adventurer.Equip(WeaponStyle.SwordAndShield, WeaponClass.Blade);
        for (int i = 0; i < 2; i++) CareerSimulator.ResolveTrainingYear(adventurer, rng.Fork($"b:{i}"));

        output.WriteLine($"양손 4년 → 한손+방패 2년: {adventurer.Proficiency}");

        Assert.Equal(twoHanded, adventurer.Proficiency[WeaponStyle.TwoHanded]);
        Assert.True(adventurer.Proficiency[WeaponStyle.SwordAndShield] > 0);
        Assert.True(adventurer.Proficiency[WeaponStyle.SwordAndShield] < twoHanded,
            "무기를 바꾸면 시간이라는 기회비용이 있어야 합니다.");
    }

    [Fact]
    public void 숙련도가_전투_효율을_바꾸되_0으로_떨어뜨리지는_않는다()
    {
        // 하한이 0이면 무기를 바꾼 캐릭터가 완전히 쓸모없어져 아무도 안 바꿉니다.
        var adventurer = Recruit(WarriorStats, WeaponStyle.DualWield);

        double fresh = adventurer.WeaponEffectiveness;
        output.WriteLine($"숙련도 0일 때 전투 효율: {fresh:F2}");

        Assert.InRange(fresh, 0.7, 0.85);

        var rng = new DeterministicRandom(Seed);
        for (int i = 0; i < 15; i++)
        {
            if (adventurer.Status != AdventurerStatus.Active) break;
            CareerSimulator.ResolveTrainingYear(adventurer, rng.Fork($"y:{i}"));
        }

        output.WriteLine($"숙련도 {adventurer.Proficiency[WeaponStyle.DualWield]}일 때: {adventurer.WeaponEffectiveness:F2}");
        Assert.True(adventurer.WeaponEffectiveness > fresh);
    }

    // ---------------------------------------------------------------
    // 무기는 능력치의 중복이 아니라 역할
    // ---------------------------------------------------------------

    [Fact]
    public void 후열을_때릴_수_있는_스타일이_존재하고_제한적이다()
    {
        var canStrikeBack = WeaponStyles.All
            .Where(s => WeaponStyles.CapabilityOf(s).CanStrikeBackRow)
            .ToList();

        output.WriteLine($"후열 타격 가능: {string.Join(", ", canStrikeBack.Select(s => s.ToKorean()))}");

        Assert.NotEmpty(canStrikeBack);
        Assert.True(canStrikeBack.Count < WeaponStyles.All.Count,
            "모든 스타일이 후열을 때릴 수 있으면 '후열을 못 때린다'는 구멍이 안 생기고, 파티 편성이 퍼즐이 아니게 됩니다.");
    }

    [Fact]
    public void 회복과_도발은_각각_특정_스타일만_가능하다()
    {
        var healers = WeaponStyles.All.Where(s => WeaponStyles.CapabilityOf(s).CanHeal).ToList();
        var taunters = WeaponStyles.All.Where(s => WeaponStyles.CapabilityOf(s).CanTaunt).ToList();

        output.WriteLine($"회복 가능: {string.Join(", ", healers.Select(s => s.ToKorean()))}");
        output.WriteLine($"도발 가능: {string.Join(", ", taunters.Select(s => s.ToKorean()))}");

        Assert.NotEmpty(healers);
        Assert.NotEmpty(taunters);
        Assert.True(healers.Count <= 2, "회복이 흔해지면 파티 구성의 제약이 사라집니다.");
    }

    [Fact]
    public void 스타일마다_장착_가능한_무기종이_다르다()
    {
        Assert.Contains(WeaponClass.Axe, WeaponStyles.AllowedClasses(WeaponStyle.TwoHanded));
        Assert.DoesNotContain(WeaponClass.Axe, WeaponStyles.AllowedClasses(WeaponStyle.Staff));

        var adventurer = Recruit(MageStats, WeaponStyle.Staff);
        Assert.Throws<ArgumentException>(() => adventurer.Equip(WeaponStyle.Staff, WeaponClass.Axe));

        adventurer.Equip(WeaponStyle.Staff, WeaponClass.Blunt);
        Assert.Equal(WeaponClass.Blunt, adventurer.EquippedClass);
    }

    // ---------------------------------------------------------------
    // 감정
    // ---------------------------------------------------------------

    [Fact]
    public void 적성도_확신도에_따라_틀리게_보인다()
    {
        var adventurer = Recruit(MageStats, WeaponStyle.Staff);
        var rng = new DeterministicRandom(Seed);

        int correct = 0;
        const int trials = 1_000;
        for (int i = 0; i < trials; i++)
        {
            var report = Appraiser.Appraise(adventurer, appraisalSkill: 0.0, rng.Fork($"t:{i}"));
            if (report.AptitudeHints[WeaponStyle.Staff] == adventurer.Aptitudes[WeaponStyle.Staff]) correct++;
        }

        double accuracy = (double)correct / trials;
        output.WriteLine($"신입 · 감정역량 0 · 지팡이 적성 적중률 {accuracy:P1}");

        Assert.InRange(accuracy, 0.25, 0.55);
    }

    [Fact]
    public void 적성_오류는_인접_등급까지만_난다()
    {
        // S를 E로 보는 극단적 오류가 나오면 감정이라는 행위 자체가 무의미해 보입니다.
        var adventurer = Recruit(MageStats, WeaponStyle.Staff);
        var rng = new DeterministicRandom(Seed);

        for (int i = 0; i < 2_000; i++)
        {
            var report = Appraiser.Appraise(adventurer, appraisalSkill: 0.0, rng.Fork($"t:{i}"));

            foreach (var style in WeaponStyles.All)
            {
                int gap = Math.Abs((int)report.AptitudeHints[style] - (int)adventurer.Aptitudes[style]);
                Assert.True(gap <= 1, $"{style.ToKorean()} 적성 오차가 {gap}등급입니다.");
            }
        }
    }

    // ---------------------------------------------------------------
    // 헬퍼
    // ---------------------------------------------------------------

    private static GrowthProfile BasicGrowth(StatBlock? potential = null) => new()
    {
        PeakAge = 20,
        BloomWidth = 3.0,
        Temperament = Temperament.Balanced,
        Potential = potential ?? StatBlock.Uniform(80),
        DeclineAge = 40
    };

    private static Adventurer Recruit(StatBlock potential, WeaponStyle style)
    {
        var growth = BasicGrowth(potential);
        var aptitudes = WeaponAptitudes.Roll(potential, new DeterministicRandom(Seed));
        var weaponClass = WeaponStyles.AllowedClasses(style)[0];

        return new Adventurer(
            "W1", "무기실험체", StatBlock.Uniform(15), 40, growth, 18, aptitudes, style, weaponClass);
    }
}
