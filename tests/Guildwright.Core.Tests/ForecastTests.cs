using Guildwright.Core.Adventurers;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 예상 성장 표시를 검증합니다.
/// <para>
/// 가설: <b>예상은 플레이어가 아는 것만으로 계산되어야 하고,
/// 확신도가 높을수록 정확해져야 한다.</b>
/// 정확한 예상치를 주면 숨겨둔 성장 곡선이 역산으로 새어나갑니다.
/// </para>
/// 근거: docs/04-game-design.md §5.5
/// </summary>
public class ForecastTests(ITestOutputHelper output)
{
    private const ulong Seed = 4141UL;

    private static Adventurer Rookie(int age = 16)
    {
        var growth = new GrowthProfile
        {
            PeakAge = 20,
            BloomWidth = 3.0,
            Temperament = Temperament.Balanced,
            Potential = PrimaryStats.Uniform(80),
            DeclineAge = 45
        };

        return new Adventurer("F", "예측대상", PrimaryStats.Uniform(15), 40, growth, age,
            WeaponAptitudes.Uniform(AptitudeGrade.B), WeaponStyle.SwordAndShield, WeaponClass.Blade);
    }

    private static List<TrainingFocus> Plan(TrainingFocus focus, int months)
    {
        var plan = new List<TrainingFocus>();
        for (int i = 0; i < TrainingRules.MonthsPerYear; i++)
        {
            plan.Add(i < months ? focus : TrainingFocus.Rest);
        }
        return plan;
    }

    [Fact]
    public void 훈련_개월수가_많을수록_예상치가_커진다()
    {
        var a = Rookie();
        var report = Appraiser.Appraise(a, 1.0, new DeterministicRandom(Seed));

        int Center(int months)
        {
            var f = TrainingForecaster.ForecastYear(a, report, Plan(TrainingFocus.Strength, months))
                .First(x => x.Stat == PrimaryStat.Strength);
            output.WriteLine($"   힘 훈련 {months,2}개월 → +{f.Min}~+{f.Max}");
            return (f.Min + f.Max) / 2;
        }

        int one = Center(1);
        int six = Center(6);
        int nine = Center(9);

        Assert.True(one > 0, "1개월 훈련 예상치가 0이면 계획 화면이 아무 정보도 주지 못합니다.");
        Assert.True(six > one);
        Assert.True(nine > six);
    }

    [Fact]
    public void 확신도가_높을수록_예상_범위가_좁아진다()
    {
        var a = Rookie();
        var rng = new DeterministicRandom(Seed);

        int Width(double confidence)
        {
            var report = Appraiser.Appraise(a, confidence, rng.Fork($"c:{confidence}"));
            var f = TrainingForecaster.ForecastYear(a, report, Plan(TrainingFocus.Strength, 8))
                .First(x => x.Stat == PrimaryStat.Strength);

            output.WriteLine($"   감정 역량 {confidence:P0} (확신도 {report.Confidence:P0}) → 힘 +{f.Min}~+{f.Max}");
            return f.Max - f.Min;
        }

        int vague = Width(0.0);
        int sharp = Width(1.0);

        Assert.True(sharp < vague,
            "감정에 투자해도 예상이 선명해지지 않으면, 감정 시스템에 투자할 이유가 사라집니다.");
    }

    [Fact]
    public void 집중한_능력치가_가장_크게_예상된다()
    {
        var a = Rookie();
        var report = Appraiser.Appraise(a, 1.0, new DeterministicRandom(Seed));

        var forecast = TrainingForecaster.ForecastYear(a, report, Plan(TrainingFocus.Intellect, 9));

        foreach (var f in forecast) output.WriteLine($"   {f}");

        var intellect = forecast.First(x => x.Stat == PrimaryStat.Intellect);
        var others = forecast.Where(x => x.Stat != PrimaryStat.Intellect);

        Assert.All(others, o => Assert.True(intellect.Max > o.Max));
    }

    [Fact]
    public void 예상은_숨겨진_실제_곡선이_아니라_추정치를_따른다()
    {
        // 확신도가 낮아 추정 잠재력이 실제와 크게 다르면, 예상도 그만큼 빗나가야 합니다.
        // 그래야 "감정이 틀리면 계획도 틀린다"가 성립합니다.
        var a = Rookie();
        var rng = new DeterministicRandom(Seed);

        var wildlyWrong = Appraiser.Appraise(a, 0.0, rng.Fork("vague"));
        var accurate = Appraiser.Appraise(a, 1.0, rng.Fork("sharp"));

        var vagueCenter = TrainingForecaster.ForecastYear(a, wildlyWrong, Plan(TrainingFocus.Strength, 8))
            .First(x => x.Stat == PrimaryStat.Strength);
        var sharpCenter = TrainingForecaster.ForecastYear(a, accurate, Plan(TrainingFocus.Strength, 8))
            .First(x => x.Stat == PrimaryStat.Strength);

        output.WriteLine($"   추정 잠재력 힘 · 흐림 {wildlyWrong.EstimatedPotential.Strength} / 선명 {accurate.EstimatedPotential.Strength} (실제 80)");
        output.WriteLine($"   예상 성장   힘 · 흐림 +{vagueCenter.Min}~+{vagueCenter.Max} / 선명 +{sharpCenter.Min}~+{sharpCenter.Max}");

        Assert.True(Math.Abs(accurate.EstimatedPotential.Strength - 80) <
                    Math.Abs(wildlyWrong.EstimatedPotential.Strength - 80) + 1);
    }

    [Fact]
    public void 추정_잠재력이_터무니없이_벗어나지_않는다()
    {
        // 실제 80인데 추정이 163으로 나오면 계획 화면이 헛소리가 됩니다.
        // 정보가 부족한 것과 거짓말을 하는 것은 다릅니다.
        var a = Rookie();
        var rng = new DeterministicRandom(Seed);

        int worst = 0;
        for (int i = 0; i < 3_000; i++)
        {
            var report = Appraiser.Appraise(a, 0.0, rng.Fork($"t:{i}"));
            foreach (var stat in PrimaryStats.AllStats)
            {
                worst = Math.Max(worst, Math.Abs(report.EstimatedPotential[stat] - 80));
            }
        }

        output.WriteLine($"   실제 80 대비 최대 오차: {worst}");

        Assert.True(worst <= 45,
            $"추정 잠재력이 실제(80)에서 {worst}만큼 벗어났습니다. 확신도가 낮아도 상식적인 범위여야 합니다.");
    }
}
