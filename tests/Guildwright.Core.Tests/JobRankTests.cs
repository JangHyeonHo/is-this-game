using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 직업 등급을 검증합니다.
/// <para>
/// 가설: <b>직업은 별도의 능력 축이 아니라 숙련도의 표현이며,
/// 등급이 바꾸는 것은 능력이 아니라 가치다.</b>
/// 견습 마법사와 대마법사가 하는 일은 같습니다. 다른 것은 급여, 수주 자격, 평판입니다.
/// </para>
/// 근거: docs/04-game-design.md §3.6
/// </summary>
public class JobRankTests(ITestOutputHelper output)
{
    private const ulong Seed = 7272UL;

    private static GrowthProfile Profile() => new()
    {
        PeakAge = 20,
        BloomWidth = 3.0,
        Temperament = Temperament.Balanced,
        Potential = PrimaryStats.Uniform(80),
        DeclineAge = 45
    };

    private static Adventurer Rookie(WeaponStyle style, AptitudeGrade aptitude = AptitudeGrade.A)
    {
        var weaponClass = WeaponStyles.AllowedClasses(style)[0];
        return new Adventurer(
            "J", "직업실험체", PrimaryStats.Uniform(20), 45, Profile(), 16,
            WeaponAptitudes.Uniform(aptitude), style, weaponClass);
    }

    // ---------------------------------------------------------------
    // 등급은 숙련도의 표현이다
    // ---------------------------------------------------------------

    [Fact]
    public void 모두_견습으로_시작한다()
    {
        foreach (var style in WeaponStyles.All)
        {
            var rookie = Rookie(style);

            Assert.Equal(JobRank.Apprentice, rookie.Rank);
            Assert.StartsWith("견습", rookie.Title);
            output.WriteLine($"  {style.ToKorean(),-8} → {rookie.Title}");
        }
    }

    [Fact]
    public void 숙련도가_쌓이면_칭호가_올라간다()
    {
        var mage = Rookie(WeaponStyle.Staff, AptitudeGrade.S);
        var rng = new DeterministicRandom(Seed);

        var seen = new List<string>();

        for (int year = 0; year < 14 && mage.Status == AdventurerStatus.Active; year++)
        {
            if (year == 0) CareerSimulator.ResolveTrainingYear(mage, rng.Fork($"y:{year}"));
            else CareerSimulator.ResolveDeploymentYear(mage, 2, rng.Fork($"y:{year}"));

            if (!seen.Contains(mage.Title))
            {
                seen.Add(mage.Title);
                output.WriteLine($"  {mage.Age}세 · 숙련 {mage.Proficiency[WeaponStyle.Staff],3} → {mage.Title} (연봉 {mage.AnnualWage})");
            }
        }

        Assert.True(seen.Count >= 3,
            $"칭호가 {seen.Count}단계밖에 오르지 않았습니다. 성장이 이름으로 드러나야 합니다.");

        Assert.True(mage.Rank >= JobRank.Master,
            $"적성 S로 12년 넘게 굴렸는데 {mage.Rank.ToKorean()} 등급에 그쳤습니다. " +
            "최고 등급이 현실적으로 도달 불가능하면 목표로 기능하지 못합니다.");
    }

    [Fact]
    public void 등급은_전투_능력을_직접_바꾸지_않는다()
    {
        // 눈덩이 방지. 강함의 차이는 숙련도 자체와 능력치가 담당합니다.
        // 등급은 그 숙련도를 부르는 이름일 뿐입니다.
        var novice = Rookie(WeaponStyle.Staff);
        var veteran = Rookie(WeaponStyle.Staff, AptitudeGrade.S);

        var rng = new DeterministicRandom(Seed);
        for (int i = 0; i < 10 && veteran.Status == AdventurerStatus.Active; i++)
        {
            CareerSimulator.ResolveTrainingYear(veteran, rng.Fork($"v:{i}"));
        }

        output.WriteLine($"  {novice.Title}: 효율 {novice.WeaponEffectiveness:F2}");
        output.WriteLine($"  {veteran.Title}: 효율 {veteran.WeaponEffectiveness:F2}");

        // 효율은 오직 숙련도 수치에서만 나와야 합니다.
        Assert.Equal(
            veteran.Proficiency.EffectivenessOf(WeaponStyle.Staff),
            veteran.WeaponEffectiveness);
    }

    // ---------------------------------------------------------------
    // 등급이 바꾸는 것은 가치다
    // ---------------------------------------------------------------

    [Fact]
    public void 등급이_오르면_급여도_오른다()
    {
        // ★ 이게 "훈련만 시키기"를 막는 압력의 출발점입니다.
        //   잘 키운 모험가일수록 놀리는 비용이 비싸집니다.
        int previous = 0;
        foreach (var rank in Enum.GetValues<JobRank>())
        {
            int wage = JobRanks.AnnualWage(rank);
            output.WriteLine($"  {rank.ToKorean(),-4} 연봉 {wage,5} · 수주 상한 난이도 {JobRanks.MaxContractDifficulty(rank)}");

            Assert.True(wage > previous, "등급이 올랐는데 급여가 오르지 않으면 유지 압력이 생기지 않습니다.");
            previous = wage;
        }
    }

    [Fact]
    public void 등급이_오르면_수주할_수_있는_의뢰가_커진다()
    {
        Assert.True(
            JobRanks.MaxContractDifficulty(JobRank.Grandmaster) >
            JobRanks.MaxContractDifficulty(JobRank.Apprentice));

        var rookie = Rookie(WeaponStyle.TwoHanded);
        Assert.Equal(2, rookie.MaxContractDifficulty);
    }

    [Fact]
    public void 급여_상승폭이_수주_보상_상승폭을_앞지르지_않는다()
    {
        // 급여가 보상보다 빨리 오르면 캐릭터를 키울수록 손해가 되어,
        // 아무도 육성하지 않는 게 최적해가 됩니다.
        foreach (var rank in Enum.GetValues<JobRank>())
        {
            int wage = JobRanks.AnnualWage(rank);
            int maxIncome = JobRanks.MaxContractDifficulty(rank) * CareerRules.IncomePerDifficulty;

            output.WriteLine($"  {rank.ToKorean(),-4} 연봉 {wage,5} vs 최대 보수 {maxIncome,5} (배율 {(double)maxIncome / wage:F2})");

            Assert.True(maxIncome > wage,
                $"{rank.ToKorean()} 등급은 최대 보수({maxIncome})가 연봉({wage})보다 낮습니다. " +
                "키울수록 손해가 되면 육성 자체가 무의미해집니다.");
        }
    }

    // ---------------------------------------------------------------
    // 전직
    // ---------------------------------------------------------------

    [Fact]
    public void 전직하면_등급이_떨어지지만_예전_숙련도는_남는다()
    {
        // "대마법사가 대검을 잡으면 견습 전사입니다."
        // 그게 전직의 대가이고, 그래서 결심이 필요합니다.
        var mage = Rookie(WeaponStyle.Staff, AptitudeGrade.S);
        var rng = new DeterministicRandom(Seed);

        for (int i = 0; i < 12 && mage.Status == AdventurerStatus.Active; i++)
        {
            if (i == 0) CareerSimulator.ResolveTrainingYear(mage, rng.Fork($"y:{i}"));
            else CareerSimulator.ResolveDeploymentYear(mage, 2, rng.Fork($"y:{i}"));
        }

        var peakTitle = mage.Title;
        int staffProficiency = mage.Proficiency[WeaponStyle.Staff];
        var peakRank = mage.Rank;

        output.WriteLine($"전직 전: {peakTitle} (지팡이 숙련 {staffProficiency}, 연봉 {mage.AnnualWage})");

        mage.Equip(WeaponStyle.TwoHanded, WeaponClass.Axe);

        output.WriteLine($"전직 후: {mage.Title} (양손 숙련 {mage.Proficiency[WeaponStyle.TwoHanded]}, 연봉 {mage.AnnualWage})");
        output.WriteLine($"        최고 도달 등급은 {mage.PeakRank.ToKorean()}으로 남습니다");

        Assert.Equal(JobRank.Apprentice, mage.Rank);
        Assert.True(mage.AnnualWage < JobRanks.AnnualWage(peakRank));

        // 예전 숙련도는 사라지지 않으므로 돌아갈 수 있습니다.
        Assert.Equal(staffProficiency, mage.Proficiency[WeaponStyle.Staff]);
        Assert.Equal(peakRank, mage.PeakRank);

        mage.Equip(WeaponStyle.Staff, WeaponClass.Blunt);
        Assert.Equal(peakTitle, mage.Title);
    }

    [Fact]
    public void 적성이_낮은_무기로_전직하면_회복이_훨씬_느리다()
    {
        // 전직 자체는 자유롭되, 적성이 그 선택의 현실성을 정합니다.
        int ProficiencyAfter(AptitudeGrade grade)
        {
            var a = new Adventurer(
                "R", "전직자", PrimaryStats.Uniform(40), 60, Profile(), 22,
                WeaponAptitudes.Of(new Dictionary<WeaponStyle, AptitudeGrade>
                {
                    [WeaponStyle.TwoHanded] = grade
                }),
                WeaponStyle.TwoHanded, WeaponClass.Axe);

            var rng = new DeterministicRandom(Seed);
            for (int i = 0; i < 5; i++) CareerSimulator.ResolveTrainingYear(a, rng.Fork($"y:{i}"));

            return a.Proficiency[WeaponStyle.TwoHanded];
        }

        int poor = ProficiencyAfter(AptitudeGrade.E);
        int great = ProficiencyAfter(AptitudeGrade.S);

        output.WriteLine($"양손 5년 숙련도 · 적성 E: {poor} → {JobRanks.FromProficiency(poor).ToKorean()} / " +
                         $"적성 S: {great} → {JobRanks.FromProficiency(great).ToKorean()}");

        Assert.True(great > poor * 2);
    }

    [Fact]
    public void 대검_적성이_높은_마법사는_전직할_가치가_있다()
    {
        // 사용자가 예로 든 상황입니다.
        // "자기는 마법사가 좋아서 했는데 키워봤더니 얘는 전직해야 한다."
        var aptitudes = WeaponAptitudes.Of(new Dictionary<WeaponStyle, AptitudeGrade>
        {
            [WeaponStyle.Staff] = AptitudeGrade.D,
            [WeaponStyle.TwoHanded] = AptitudeGrade.S
        });

        int ProficiencyWith(WeaponStyle style)
        {
            var a = new Adventurer(
                "M", "늦게 안 재능", PrimaryStats.Uniform(25), 50, Profile(), 17,
                aptitudes, style, WeaponStyles.AllowedClasses(style)[0]);

            var rng = new DeterministicRandom(Seed);
            for (int i = 0; i < 8 && a.Status == AdventurerStatus.Active; i++)
            {
                CareerSimulator.ResolveTrainingYear(a, rng.Fork($"y:{i}"));
            }

            output.WriteLine($"  {style.ToKorean()} 8년 → {a.Title} (숙련 {a.Proficiency[style]}, 연봉 {a.AnnualWage})");
            return a.Proficiency[style];
        }

        int asMage = ProficiencyWith(WeaponStyle.Staff);
        int asWarrior = ProficiencyWith(WeaponStyle.TwoHanded);

        Assert.True(asWarrior > asMage,
            "적성이 확실히 높은 쪽으로 전직할 이유가 없으면, 감정 정보에 투자할 이유도 없어집니다.");
    }
}
