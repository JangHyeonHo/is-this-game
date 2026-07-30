using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Training;

/// <summary>
/// 한 달의 성과 등급.
/// <para>
/// 대성공·성공·부진은 <b>이미 나온 결과에 이름을 붙이는 것</b>입니다 —
/// 컨디션 배율과 피로가 만든 차이를 이산 등급으로 보여줄 뿐입니다.
/// 반면 <see cref="Failure"/>는 <b>실제로 굴리는 주사위</b>입니다.
/// </para>
/// </summary>
public enum MonthGrade
{
    /// <summary>실패. 성장이 거의 없고 피로가 더 쌓입니다.</summary>
    Failure,
    /// <summary>부진. 피로가 쌓였거나 컨디션이 나빴습니다.</summary>
    Poor,
    Success,
    /// <summary>대성공. 절호조와 좋은 변동이 겹쳤습니다.</summary>
    GreatSuccess
}

public static class MonthGrades
{
    public static string ToKorean(this MonthGrade grade) => grade switch
    {
        MonthGrade.Failure => "실패",
        MonthGrade.Poor => "부진",
        MonthGrade.Success => "성공",
        MonthGrade.GreatSuccess => "대성공!",
        _ => "?"
    };
}

/// <param name="Month">1~12.</param>
/// <param name="Activity">그 달에 한 활동.</param>
/// <param name="StatGain">그 달의 능력치 변화.</param>
/// <param name="ProficiencyGain">그 달에 오른 장착 무기 숙련도.</param>
/// <param name="JudgementGain">그 달에 오른 판단력.</param>
/// <param name="FatigueAfter">행동 후 피로도.</param>
/// <param name="ConditionAfter">행동 후 컨디션.</param>
/// <param name="Note">표시용 설명.</param>
/// <param name="Grade">그 달의 성과 등급.</param>
public sealed record MonthOutcome(
    int Month,
    TrainingActivity Activity,
    PrimaryStats StatGain,
    double ProficiencyGain,
    double JudgementGain,
    int FatigueAfter,
    Condition ConditionAfter,
    string Note,
    MonthGrade Grade = MonthGrade.Success)
{
    public bool Failed => Grade == MonthGrade.Failure;
}

/// <summary>
/// 훈련 1년을 월 단위로 진행하는 세션.
/// <para>
/// 월 단위가 "12번 클릭"이 되지 않으려면 매달의 선택에 대가가 있어야 합니다.
/// 그 대가가 <b>피로도</b>입니다. 훈련하면 쌓이고, 쌓이면 성장이 떨어지고,
/// 더 쌓이면 <b>실패해서 그 달을 잃습니다</b>. 휴식은 안전하지만 한 달을 버립니다.
/// </para>
/// <para>
/// 여기에 <b>컨디션</b>이 매달 변동해서, "이번 달은 절호조니까 밀어붙일까"라는
/// 판단이 생깁니다. 이게 육성의 묘미를 만드는 최소 장치입니다.
/// </para>
/// 근거: docs/01-game-design.md §5.5, docs/07-decisions.md §2·§3
/// </summary>
public sealed class TrainingYearSession
{
    private readonly Adventurer _adventurer;
    private readonly Mentorship _mentorship;
    private readonly IRandomSource _rng;
    private readonly List<MonthOutcome> _months = [];

    /// <summary>
    /// 연중 성장 누적. <b>반드시 실수로 누적합니다.</b>
    /// <para>
    /// 월 단위로 쪼개면 한 달 성장이 3~4 정도의 작은 값이 되는데, 여기서 정수로 반올림하면
    /// 미세한 차이가 통째로 사라집니다. 실제로 그 버그를 겪었습니다.
    /// 반올림은 <see cref="Complete"/>에서 딱 한 번만 합니다. (docs/08 #8)
    /// </para>
    /// </summary>
    private readonly double[] _accumulated = new double[7];

    private double _proficiency;
    private double _judgement;
    private int _failedMonths;

    public TrainingYearSession(Adventurer adventurer, IRandomSource rng, Mentorship? mentorship = null, int startingFatigue = 0)
    {
        if (adventurer.Status != AdventurerStatus.Active)
        {
            throw new InvalidOperationException($"{adventurer.Name}은(는) 현역이 아닙니다 (상태: {adventurer.Status}).");
        }

        _adventurer = adventurer;
        _mentorship = mentorship ?? Mentorship.None;
        _rng = rng;
        Fatigue = Math.Clamp(startingFatigue, 0, TrainingRules.MaxFatigue);
        Condition = Condition.Normal;
    }

    /// <summary>다음에 진행할 달 (1~12).</summary>
    public int CurrentMonth => _months.Count + 1;

    public int MonthsCompleted => _months.Count;

    public bool IsComplete => _months.Count >= TrainingRules.MonthsPerYear;

    public int Fatigue { get; private set; }

    public Condition Condition { get; private set; }

    public IReadOnlyList<MonthOutcome> Months => _months;

    /// <summary>
    /// 지금 훈련하면 실패할 확률.
    /// <para>
    /// <b>화면에 그대로 보여줍니다.</b> 확률을 숨기면 "무리할까 말까"가 판단이 아니라 감이 됩니다.
    /// </para>
    /// </summary>
    public double FailureChance => FailureChanceAt(Fatigue);

    /// <summary>피로도가 주어졌을 때의 실패 확률. 예보에서도 씁니다.</summary>
    public static double FailureChanceAt(int fatigue)
    {
        if (fatigue <= TrainingRules.FailureThreshold) return 0.0;

        return Math.Clamp(
            (fatigue - TrainingRules.FailureThreshold) * TrainingRules.FailureChancePerFatiguePoint,
            0.0, 1.0);
    }

    /// <summary>한 달을 진행합니다.</summary>
    public MonthOutcome AdvanceMonth(TrainingActivity activity)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("이미 12개월을 모두 진행했습니다. Complete()를 호출하세요.");
        }

        int month = CurrentMonth;

        var outcome = activity == TrainingActivity.Rest
            ? DoRest(month)
            : DoTraining(month, TrainingActivities.Of(activity));

        _months.Add(outcome);
        return outcome;
    }

    /// <summary>본체 능력치 + 아직 확정되지 않은 연중 누적.</summary>
    private double EffectiveStat(PrimaryStat kind) => _adventurer.Stats[kind] + _accumulated[(int)kind];

    /// <summary>
    /// 휴식.
    /// <para>
    /// <b>피로 회복량은 고정이고 컨디션 회복만 등급으로 갈립니다.</b>
    /// 피로에 난수가 끼면 계획 화면의 12개월 피로 예보가 더 이상 정확하지 않게 되는데,
    /// 불확실한 정보가 이미 많은 게임에서 <b>정확한 정보가 하나쯤은 남아 있는 게 좋습니다.</b>
    /// (docs/08 #26에서 그 예보를 만든 게 피로에 난수가 없기 때문이었습니다.)
    /// </para>
    /// </summary>
    private MonthOutcome DoRest(int month)
    {
        int before = Fatigue;
        Fatigue = Math.Clamp(
            Fatigue + TrainingActivities.Of(TrainingActivity.Rest).FatigueCost,
            0, TrainingRules.MaxFatigue);

        var previous = Condition;
        int steps = DriftCondition(restBonus: true);

        // 쉬었는데 컨디션이 얼마나 올라왔는가로 등급을 매깁니다.
        var grade = steps switch
        {
            >= 2 => MonthGrade.GreatSuccess,
            <= -1 => MonthGrade.Poor,
            _ => MonthGrade.Success
        };

        string note = $"{month}월: 휴식 · {grade.ToKorean()} " +
                      $"(피로 {before} → {Fatigue}, 컨디션 {previous.ToKorean()} → {Condition.ToKorean()})";

        return new MonthOutcome(month, TrainingActivity.Rest, PrimaryStats.Zero, 0.0, 0.0, Fatigue, Condition, note, grade);
    }

    private MonthOutcome DoTraining(int month, TrainingActivityProfile profile)
    {
        var growth = _adventurer.Growth;
        double bloom = growth.BloomFactorAt(_adventurer.Age);

        // ⚠️ 실패 판정은 훈련 전 피로도로 합니다. 훈련해서 쌓인 피로로 판정하면
        //    "이번 달에 무리할까"를 물을 때 보여준 확률과 실제가 달라집니다.
        bool failed = _rng.Chance(FailureChance);

        double multiplier =
            bloom
            * growth.TrainingMultiplier
            * _mentorship.TrainingMultiplier
            * Condition.Multiplier()
            * FatiguePenalty();

        // 등급 판정용: 컨디션과 피로가 만든 배율이 평상시 대비 얼마나 좋은가.
        double qualityRatio = Condition.Multiplier() * FatiguePenalty();

        // 이번 달 훈련이 실제로 이루어진 컨디션. 아래에서 컨디션이 변동하므로 미리 잡아둡니다.
        var trainedUnder = Condition;

        if (failed) multiplier *= TrainingRules.FailureGrowthRatio;

        var gain = PrimaryStats.Zero;
        foreach (var kind in PrimaryStats.AllStats)
        {
            double weight = profile.WeightOf(kind);
            if (weight <= 0.0) continue;

            double current = EffectiveStat(kind);
            int potential = growth.Potential[kind];
            if (potential <= current) continue;

            double variance = 0.85 + _rng.NextDouble() * 0.3;
            double amount = (potential - current) * TrainingRules.MonthlyLearnRate * multiplier * weight * variance;

            _accumulated[(int)kind] += amount;
            gain = gain.With(kind, (int)Math.Round(amount));
        }

        // 무기 숙련도와 판단력. 실패한 달에는 거의 안 오릅니다.
        double yield = failed ? TrainingRules.FailureGrowthRatio : 1.0;

        double proficiency = profile.ProficiencyPerMonth * yield * _mentorship.TrainingMultiplier;
        _proficiency += proficiency;

        double judgement = profile.JudgementPerMonth * yield;
        _judgement += judgement;

        Fatigue = Math.Clamp(
            Fatigue + profile.FatigueCost + (failed ? TrainingRules.ExtraFatigueOnFailure : 0),
            0, TrainingRules.MaxFatigue);

        DriftCondition(restBonus: false);

        MonthGrade grade;
        if (failed)
        {
            _failedMonths++;
            grade = MonthGrade.Failure;
        }
        else
        {
            // 실제로 돌려보니 12개월 내내 "성공"만 떠서 화면이 단조로웠습니다. (docs/08 #18)
            grade = qualityRatio switch
            {
                >= 1.10 => MonthGrade.GreatSuccess,
                <= 0.90 => MonthGrade.Poor,
                _ => MonthGrade.Success
            };
        }

        string note = $"{month}월: {profile.Name} · {grade.ToKorean()} " +
                      $"(컨디션 {trainedUnder.ToKorean()} → {Condition.ToKorean()}, 피로 {Fatigue})";

        return new MonthOutcome(month, profile.Activity, gain, proficiency, judgement, Fatigue, Condition, note, grade);
    }

    /// <summary>피로가 임계치를 넘으면 성장이 떨어집니다.</summary>
    private double FatiguePenalty()
    {
        if (Fatigue <= TrainingRules.FatigueSoftCap) return 1.0;

        int excess = Fatigue - TrainingRules.FatigueSoftCap;
        int range = TrainingRules.MaxFatigue - TrainingRules.FatigueSoftCap;
        return Math.Max(0.35, 1.0 - (double)excess / range * 0.65);
    }

    /// <summary>
    /// 컨디션은 매달 흔들립니다.
    ///
    /// <para>
    /// 문턱을 ±1.0으로 두었더니 실제 플레이에서 거의 언제나 "보통"이라
    /// 컨디션을 보고 판단할 일이 생기지 않았습니다. ±0.6으로 낮췄습니다. (docs/08 #19)
    /// </para>
    ///
    /// <para>
    /// <b>피로압은 무리했을 때만 걸립니다.</b> 예전에는 피로에 정비례해서
    /// (<c>−피로/100</c>) 평범하게 훈련하는 것만으로도 컨디션이 계속 깎였습니다.
    /// 지금은 <see cref="TrainingRules.FatigueSoftCap"/>을 넘긴 만큼만 반영합니다 —
    /// <b>그 선 하나가 성장 저하와 컨디션 하락을 동시에 가릅니다.</b>
    /// </para>
    ///
    /// <para>
    /// <b>평균 회귀가 없으면 최악에 갇힙니다.</b> 예전에는 컨디션이 낮다고 해서
    /// 올라갈 확률이 높아지지 않아서, 한 번 최악에 빠지면 휴식 말고는 나올 길이 없었습니다.
    /// 실제 플레이에서 6개월 내내 최악이었던 적이 있습니다.
    /// 이제 보통 쪽으로 당기는 힘이 있어 <b>최악도 절호조도 오래 못 갑니다.</b>
    /// </para>
    ///
    /// 근거: docs/08-balance-log.md #32
    /// </summary>
    /// <returns>실제로 이동한 단계 수. 휴식 등급 판정에 씁니다.</returns>
    private int DriftCondition(bool restBonus)
    {
        // 무리한 만큼만 깎입니다. 피로 45 이하에서는 순수하게 운입니다.
        int overwork = Math.Max(0, Fatigue - TrainingRules.FatigueSoftCap);
        double fatigueBias = -(double)overwork / (TrainingRules.MaxFatigue - TrainingRules.FatigueSoftCap);

        // 보통(Normal)으로 당기는 힘. 최악이면 +0.5, 절호조면 −0.5.
        double pullToNormal = ((int)Condition.Normal - (int)Condition) * TrainingRules.ConditionMeanReversion;

        double restEffect = restBonus ? TrainingRules.ConditionRestBonus : 0.0;
        double roll = _rng.NextGaussian() + fatigueBias + pullToNormal + restEffect;

        int step = roll switch
        {
            < -1.5 => -2,
            < -0.6 => -1,
            > 1.5 => +2,
            > 0.6 => +1,
            _ => 0
        };

        int before = (int)Condition;
        int next = Math.Clamp(before + step, (int)Condition.Terrible, (int)Condition.Excellent);
        Condition = (Condition)next;

        return next - before;
    }

    /// <summary>12개월을 마치고 결과를 모험가에게 적용합니다.</summary>
    public YearRecord Complete()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException($"아직 {TrainingRules.MonthsPerYear - MonthsCompleted}개월이 남았습니다.");
        }

        return Settle();
    }

    /// <summary>
    /// <b>지금까지 훈련한 달만</b> 결산합니다. 12개월을 안 채워도 됩니다.
    /// <para>
    /// 달력이 달 단위이므로 한 사람이 <b>몇 달 훈련하고 몇 달 파견</b>을 갑니다.
    /// 12개월을 강제하면 "훈련을 고르면 그 해가 다 간다"가 되어 매달 정책이 성립하지 않습니다.
    /// </para>
    /// <para>노화는 훈련한 달 수만큼만 적용합니다 — 나이 자체는 <see cref="Adventurer"/>가 셉니다.</para>
    /// </summary>
    public YearRecord Settle()
    {
        if (MonthsCompleted == 0)
        {
            throw new InvalidOperationException("훈련한 달이 없습니다.");
        }

        double share = (double)MonthsCompleted / TrainingRules.MonthsPerYear;

        // 노화는 훈련한 기간만큼.
        // 반올림은 여기서 딱 한 번 — 월 단위로 반올림하면 미세한 차이가 전부 사라집니다.
        double decline = _adventurer.Growth.DeclineFactorAt(_adventurer.Age) * share;
        var change = PrimaryStats.Zero;

        foreach (var kind in PrimaryStats.AllStats)
        {
            double total = _accumulated[(int)kind];
            if (decline > 0.0) total -= _adventurer.Stats[kind] * decline;
            change = change.With(kind, (int)Math.Round(total));
        }

        string span = MonthsCompleted == TrainingRules.MonthsPerYear ? "훈련" : $"{MonthsCompleted}달 훈련";

        string note = _failedMonths > 0
            ? $"{_adventurer.Age}세: {span} ({_failedMonths}개월 실패)"
            : _mentorship.TrainingMultiplier > 1.0
                ? $"{_adventurer.Age}세: {_mentorship.MentorName}의 지도 아래 {span}"
                : $"{_adventurer.Age}세: {span}";

        var record = new YearRecord(
            _adventurer.Age, YearActivity.Training, change, null, 0, note,
            ProficiencyGain: _proficiency, Months: MonthsCompleted);

        _adventurer.ApplyYear(record);

        // 훈련 기간의 기본 판단력 + 모의전으로 따로 쌓은 만큼. 기본값도 기간에 비례합니다.
        int baseJudgement = (int)Math.Round(CareerRules.JudgementFromTraining * share);
        _adventurer.GainJudgement(baseJudgement + (int)Math.Round(_judgement));
        return record;
    }
}
