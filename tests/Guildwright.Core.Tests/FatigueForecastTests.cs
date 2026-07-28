using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 계획 화면이 보여줘야 하는 것들 — 예상 피로, 예상 전투 수치, 실패 확률.
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

    private static List<TrainingActivity> Plan(params TrainingActivity[] months)
    {
        var plan = new List<TrainingActivity>(months);
        while (plan.Count < TrainingRules.MonthsPerYear) plan.Add(TrainingActivity.Rest);
        return plan;
    }

    /// <summary>실패 확률이 0인 계획. 예보와 실제가 정확히 일치해야 하는 조건입니다.</summary>
    private static List<TrainingActivity> SafePlan() => Plan(
        TrainingActivity.Strength, TrainingActivity.Strength, TrainingActivity.Rest,
        TrainingActivity.Endurance, TrainingActivity.Strength, TrainingActivity.Rest);

    [Fact]
    public void Forecast_예상_피로가_실제_진행과_일치한다()
    {
        // 피로는 계획만으로 정해집니다 — 실패하지만 않으면 난수가 끼지 않습니다.
        // 예보와 실제가 다르면 계획 화면이 거짓말을 하는 셈입니다.
        var (a, report) = Subject();
        var plan = SafePlan();

        var forecast = TrainingForecaster.Forecast(a, report, plan);

        var session = new TrainingYearSession(a, new DeterministicRandom(99));
        var actual = new List<int>();
        foreach (var activity in plan)
        {
            var outcome = session.AdvanceMonth(activity);
            actual.Add(outcome.FatigueAfter);

            // 실패하면 피로가 +25가 되어 예보와 갈라집니다.
            // 이 계획은 실패선을 넘지 않으므로 그럴 일이 없어야 합니다.
            Assert.False(outcome.Failed);
        }

        Assert.Equal(actual, forecast.FatigueByMonth);
    }

    [Fact]
    public void Forecast_피로_예보는_12개월치가_전부_나온다()
    {
        var (a, report) = Subject();
        var forecast = TrainingForecaster.Forecast(a, report, Plan(TrainingActivity.Strength));

        Assert.Equal(TrainingRules.MonthsPerYear, forecast.FatigueByMonth.Count);
        Assert.Equal(TrainingRules.MonthsPerYear, forecast.FailureChanceByMonth.Count);
    }

    [Fact]
    public void Forecast_쉬지_않고_밀어붙이면_실패_위험을_경고한다()
    {
        var (a, report) = Subject();
        var reckless = Enumerable.Repeat(TrainingActivity.Strength, TrainingRules.MonthsPerYear).ToList();

        var risky = TrainingForecaster.Forecast(a, report, reckless);
        var safe = TrainingForecaster.Forecast(a, report, SafePlan());

        Assert.True(risky.MonthsAtRisk > 0, "12개월 연속 훈련인데 실패 위험 경고가 없습니다.");
        Assert.True(risky.ExpectedFailedMonths > 0.5,
            $"기대 실패 개월이 {risky.ExpectedFailedMonths:F1}로 너무 낮습니다 — 무모함에 대가가 없습니다.");

        Assert.Equal(0, safe.MonthsAtRisk);
        Assert.Equal(0.0, safe.WorstFailureChance);
    }

    [Fact]
    public void Forecast_예보한_실패_확률이_세션과_같다()
    {
        // 화면에 보여준 확률과 실제로 굴리는 확률이 다르면 판단 재료가 아니라 거짓말입니다.
        var (a, report) = Subject();
        var plan = Enumerable.Repeat(TrainingActivity.Strength, TrainingRules.MonthsPerYear).ToList();

        var forecast = TrainingForecaster.Forecast(a, report, plan);

        var session = new TrainingYearSession(a, new DeterministicRandom(7));
        for (int i = 0; i < plan.Count; i++)
        {
            // 세션이 실제로 쓰는 확률은 "그 달을 시작할 때"의 값입니다.
            Assert.Equal(forecast.FailureChanceByMonth[i], session.FailureChance, precision: 10);

            var outcome = session.AdvanceMonth(plan[i]);
            if (outcome.Failed) break;   // 실패하면 피로가 어긋나므로 여기까지만 비교
        }
    }

    [Fact]
    public void Forecast_근력_훈련은_최대HP_증가로_이어진다()
    {
        // 근력 훈련은 힘●●● + 활력●● 이므로 최대 HP가 올라야 합니다.
        // 파생 예보가 원천 예보를 실제로 따라가는지 확인합니다.
        var (a, report) = Subject();
        var plan = Plan(Enumerable.Repeat(TrainingActivity.Strength, 6).ToArray());

        var forecast = TrainingForecaster.Forecast(a, report, plan);
        var hp = forecast.Derived.Single(d => d.Stat == DerivedStat.MaxHp);

        Assert.True(hp.Max > 0, "근력을 6개월 훈련했는데 최대 HP 예상 증가가 0입니다.");
        Assert.True(hp.Min <= hp.Max);
    }

    [Fact]
    public void Forecast_기술_훈련만_무기_숙련도를_올린다()
    {
        var (a, report) = Subject();

        double technique = TrainingForecaster
            .Forecast(a, report, Plan(Enumerable.Repeat(TrainingActivity.Technique, 6).ToArray()))
            .ProficiencyGain;

        double study = TrainingForecaster
            .Forecast(a, report, Plan(Enumerable.Repeat(TrainingActivity.Study, 6).ToArray()))
            .ProficiencyGain;

        Assert.True(technique > 0, "기술 훈련 6개월인데 숙련도 예보가 0입니다.");
        Assert.Equal(0.0, study);
    }

    [Fact]
    public void Forecast_전부_휴식이면_아무것도_움직이지_않는다()
    {
        var (a, report) = Subject();
        var forecast = TrainingForecaster.Forecast(a, report, Plan());

        Assert.All(forecast.Stats, s => Assert.Equal(0, s.Max));
        Assert.DoesNotContain(forecast.Derived, d => d.Moves);
        Assert.All(forecast.FatigueByMonth, f => Assert.Equal(0, f));
        Assert.Equal(0.0, forecast.ProficiencyGain);
    }

    [Fact]
    public void ForecastYear_기존_호출은_그대로_원천_예보만_돌려준다()
    {
        var (a, report) = Subject();
        var plan = Plan(TrainingActivity.Strength, TrainingActivity.Endurance);

        Assert.Equal(
            TrainingForecaster.Forecast(a, report, plan).Stats,
            TrainingForecaster.ForecastYear(a, report, plan));
    }
}
