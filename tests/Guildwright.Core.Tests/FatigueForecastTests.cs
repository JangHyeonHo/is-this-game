using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 계획 화면이 보여줘야 하는 것들 — 예상 피로와 예상 전투 수치.
/// <para>
/// 성장만 보여주면 계획이 반쪽입니다. 피로는 매달 선택의 대가 그 자체인데
/// 실행에 들어가서야 알게 되면 미리 짜는 의미가 없고,
/// 원천 능력치만 보여주면 "힘 +12"가 전투에서 뭘 바꾸는지 알 수 없습니다.
/// </para>
/// </summary>
public class FatigueForecastTests
{
    private static (Adventurer, ScoutingReport) Subject(ulong seed = 11)
    {
        var a = Adventurer.Recruit("F", "예보", new DeterministicRandom(seed));
        var report = Appraiser.Appraise(a, 1.0, new DeterministicRandom(seed + 1));
        return (a, report);
    }

    private static List<TrainingFocus> Plan(params TrainingFocus[] months)
    {
        var plan = new List<TrainingFocus>(months);
        while (plan.Count < TrainingRules.MonthsPerYear) plan.Add(TrainingFocus.Rest);
        return plan;
    }

    [Fact]
    public void Forecast_예상_피로가_실제_진행과_일치한다()
    {
        // 피로는 계획만으로 정해집니다 — 난수가 끼지 않습니다.
        // 예보와 실제가 다르면 계획 화면이 거짓말을 하는 셈입니다.
        var (a, report) = Subject();
        var plan = Plan(
            TrainingFocus.Strength, TrainingFocus.Strength, TrainingFocus.Rest,
            TrainingFocus.Vitality, TrainingFocus.Strength, TrainingFocus.Agility);

        var forecast = TrainingForecaster.Forecast(a, report, plan);

        var session = new TrainingYearSession(a, new DeterministicRandom(99));
        var actual = new List<int>();
        foreach (var focus in plan)
        {
            var outcome = session.AdvanceMonth(focus);
            actual.Add(outcome.FatigueAfter);

            // 부상이 나면 피로가 0으로 초기화되고 요양이 붙어 예보와 갈라집니다.
            // 이 계획은 위험선을 넘지 않으므로 그럴 일이 없어야 합니다.
            Assert.False(outcome.GotInjured);
        }

        Assert.Equal(actual, forecast.FatigueByMonth);
    }

    [Fact]
    public void Forecast_피로_예보는_12개월치가_전부_나온다()
    {
        var (a, report) = Subject();
        var forecast = TrainingForecaster.Forecast(a, report, Plan(TrainingFocus.Strength));

        Assert.Equal(TrainingRules.MonthsPerYear, forecast.FatigueByMonth.Count);
    }

    [Fact]
    public void Forecast_쉬지_않고_밀어붙이면_부상_위험을_경고한다()
    {
        var (a, report) = Subject();
        var reckless = Enumerable.Repeat(TrainingFocus.Strength, TrainingRules.MonthsPerYear).ToList();
        var careful = Plan(
            TrainingFocus.Strength, TrainingFocus.Strength, TrainingFocus.Rest,
            TrainingFocus.Strength, TrainingFocus.Strength, TrainingFocus.Rest);

        Assert.True(TrainingForecaster.Forecast(a, report, reckless).MonthsAtInjuryRisk > 0,
            "12개월 연속 훈련인데 부상 위험 경고가 없습니다.");
        Assert.Equal(0, TrainingForecaster.Forecast(a, report, careful).MonthsAtInjuryRisk);
    }

    [Fact]
    public void Forecast_활력_훈련은_최대HP_증가로_이어진다()
    {
        // 파생 예보가 원천 예보를 실제로 따라가는지.
        var (a, report) = Subject();
        var plan = Plan(Enumerable.Repeat(TrainingFocus.Vitality, 6).ToArray());

        var forecast = TrainingForecaster.Forecast(a, report, plan);
        var hp = forecast.Derived.Single(d => d.Stat == DerivedStat.MaxHp);

        Assert.True(hp.Max > 0, "활력을 6개월 훈련했는데 최대 HP 예상 증가가 0입니다.");
        Assert.True(hp.Min <= hp.Max);
    }

    [Fact]
    public void Forecast_전부_휴식이면_파생도_움직이지_않는다()
    {
        var (a, report) = Subject();
        var forecast = TrainingForecaster.Forecast(a, report, Plan());

        Assert.All(forecast.Stats, s => Assert.Equal(0, s.Max));
        Assert.DoesNotContain(forecast.Derived, d => d.Moves);
        Assert.All(forecast.FatigueByMonth, f => Assert.Equal(0, f));
    }

    [Fact]
    public void ForecastYear_기존_호출은_그대로_원천_예보만_돌려준다()
    {
        var (a, report) = Subject();
        var plan = Plan(TrainingFocus.Strength, TrainingFocus.Agility);

        Assert.Equal(
            TrainingForecaster.Forecast(a, report, plan).Stats,
            TrainingForecaster.ForecastYear(a, report, plan));
    }
}
