using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 정보 비대칭 설계를 검증합니다.
/// <para>
/// 가설: <b>성장 타입은 숨겨져 있되, 관찰과 투자로 점점 알 수 있어야 한다.</b>
/// 완전히 숨기면 선택이 주사위가 되고, 완전히 공개하면 도박이 사라집니다.
/// </para>
/// 근거: docs/04-game-design.md §3.4
/// </summary>
public class AppraiserTests(ITestOutputHelper output)
{
    private static Adventurer WithYears(int years, IRandomSource rng)
    {
        var growth = new GrowthProfile
        {
            PeakAge = 26,
            BloomWidth = 3.0,
            Temperament = Temperament.Battleborn,
            Potential = PrimaryStats.Uniform(70),
            DeclineAge = 33
        };

        var a = new Adventurer("A", "관찰대상", PrimaryStats.Uniform(12), 40, growth);
        for (int i = 0; i < years; i++)
        {
            CareerSimulator.ResolveTrainingYear(a, rng.Fork($"year:{i}"));
        }
        return a;
    }

    [Fact]
    public void 확신도는_관찰연차와_감정역량에_따라_오른다()
    {
        double none = Appraiser.ComputeConfidence(0, 0.0);
        double observed = Appraiser.ComputeConfidence(5, 0.0);
        double skilled = Appraiser.ComputeConfidence(0, 1.0);
        double both = Appraiser.ComputeConfidence(10, 1.0);

        output.WriteLine($"관찰0/역량0 {none:P0} · 관찰5 {observed:P0} · 역량1.0 {skilled:P0} · 둘다 {both:P0}");

        Assert.Equal(0.0, none, precision: 3);
        Assert.True(observed > none);
        Assert.True(skilled > none);
        Assert.True(both > observed && both > skilled);
        Assert.InRange(both, 0.9, 1.0);
    }

    [Fact]
    public void 확신도가_낮으면_평가가_자주_틀린다()
    {
        // 틀린 정보가 나온다는 것 자체가 설계 의도입니다.
        // "확신도 20%"라는 표시가 플레이어에게 실제 의미를 가져야 합니다.
        var rng = new DeterministicRandom(31UL);
        var rookie = WithYears(0, rng);

        int correct = 0;
        const int trials = 2_000;
        for (int i = 0; i < trials; i++)
        {
            var report = Appraiser.Appraise(rookie, appraisalSkill: 0.0, rng.Fork($"t:{i}"));
            if (report.TimingHint == rookie.Growth.Timing) correct++;
        }

        double accuracy = (double)correct / trials;
        output.WriteLine($"신입 · 감정역량 0 · 개화시기 적중률 {accuracy:P1}");

        Assert.InRange(accuracy, 0.25, 0.50);
    }

    [Fact]
    public void 오래_지켜보고_감정역량이_높으면_거의_정확해진다()
    {
        var rng = new DeterministicRandom(31UL);
        var veteran = WithYears(10, rng);

        int correct = 0;
        const int trials = 2_000;
        for (int i = 0; i < trials; i++)
        {
            var report = Appraiser.Appraise(veteran, appraisalSkill: 1.0, rng.Fork($"t:{i}"));
            if (report.TimingHint == veteran.Growth.Timing) correct++;
        }

        double accuracy = (double)correct / trials;
        output.WriteLine($"10년 관찰 · 감정역량 1.0 · 개화시기 적중률 {accuracy:P1}");

        Assert.True(accuracy > 0.90,
            "충분히 투자했는데도 정보가 부정확하면, 정보에 투자할 이유가 사라집니다.");
    }

    [Fact]
    public void 잠재력_추정오차는_확신도가_오를수록_줄어든다()
    {
        var rng = new DeterministicRandom(77UL);

        double MeanError(Adventurer a, double skill)
        {
            double sum = 0.0;
            const int trials = 1_000;
            for (int i = 0; i < trials; i++)
            {
                var report = Appraiser.Appraise(a, skill, rng.Fork($"e:{i}"));
                sum += Math.Abs(report.EstimatedPotential.Total - a.Growth.Potential.Total);
            }
            return sum / trials;
        }

        var rookie = WithYears(0, rng);
        var veteran = WithYears(10, rng);

        double rookieError = MeanError(rookie, 0.0);
        double veteranError = MeanError(veteran, 1.0);

        output.WriteLine($"잠재력 총합 추정 평균오차 · 신입 {rookieError:F1} vs 베테랑 {veteranError:F1}");

        Assert.True(veteranError < rookieError * 0.3);
    }

    [Fact]
    public void 멘토는_감정_정확도를_올려준다()
    {
        // 실전을 오래 살아남은 멘토가 사람 보는 눈을 준다는 설계.
        var rng = new DeterministicRandom(5UL);
        var mentor = new Adventurer("M", "노병", PrimaryStats.Uniform(80), 75, new GrowthProfile
        {
            PeakAge = 22,
            BloomWidth = 3.0,
            Temperament = Temperament.Balanced,
            Potential = PrimaryStats.Uniform(85),
            DeclineAge = 30
        }, age: 24);

        CareerSimulator.ResolveTrainingYear(mentor, rng);
        for (int i = 0; i < 6; i++)
        {
            if (mentor.Status != AdventurerStatus.Active) break;
            CareerSimulator.ResolveDeploymentYear(mentor, 2, rng.Fork($"d:{i}"));
        }
        mentor.Retire();

        var mentorship = Mentorship.From(mentor);
        output.WriteLine($"실전 {mentor.DeploymentYears}년 멘토 · 감정 보너스 {mentorship.AppraisalBonus:F2}");

        double withoutMentor = Appraiser.ComputeConfidence(2, 0.0);
        double withMentor = Appraiser.ComputeConfidence(2, mentorship.AppraisalBonus);

        Assert.True(withMentor > withoutMentor);
    }
}
