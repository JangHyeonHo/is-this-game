using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Training;

/// <summary>한 달에 무엇을 할지.</summary>
/// <summary>
/// 한 달의 성과 등급.
/// <para>
/// 새 규칙이 아니라 <b>이미 나온 결과에 이름을 붙이는 것</b>입니다.
/// 컨디션 배율과 변동값이 만든 차이를 이산 등급으로 보여줄 뿐인데,
/// "대성공!"이 뜨는 순간이 육성 게임 체감의 큰 부분을 차지합니다.
/// </para>
/// </summary>
public enum MonthGrade
{
    /// <summary>부상. 그 달을 잃습니다.</summary>
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

public enum TrainingFocus
{
    Strength,
    Agility,
    Finesse,
    Vitality,
    Intellect,
    Spirit,
    /// <summary>휴식. 성장은 없지만 피로가 크게 줄고 컨디션이 회복됩니다.</summary>
    Rest
}

/// <param name="Month">1~12.</param>
/// <param name="Focus">실제로 수행된 행동. 요양 중이면 강제로 <see cref="TrainingFocus.Rest"/>가 됩니다.</param>
/// <param name="StatGain">그 달의 능력치 변화.</param>
/// <param name="FatigueAfter">행동 후 피로도.</param>
/// <param name="ConditionAfter">행동 후 컨디션.</param>
/// <param name="GotInjured">이번 달에 부상을 입었는지.</param>
/// <param name="WasRecovering">요양 때문에 선택이 무시되었는지.</param>
/// <param name="Note">표시용 설명.</param>
/// <param name="Grade">그 달의 성과 등급. 연출용이지만 플레이어 체감의 큰 부분입니다.</param>
public sealed record MonthOutcome(
    int Month,
    TrainingFocus Focus,
    PrimaryStats StatGain,
    int FatigueAfter,
    Condition ConditionAfter,
    bool GotInjured,
    bool WasRecovering,
    string Note,
    MonthGrade Grade = MonthGrade.Success);

/// <summary>
/// 훈련 1년을 월 단위로 진행하는 세션.
/// <para>
/// 월 단위가 "12번 클릭"이 되지 않으려면 매달의 선택에 대가가 있어야 합니다.
/// 그 대가가 <b>피로도</b>입니다. 훈련하면 쌓이고, 쌓이면 성장이 떨어지고,
/// 더 쌓이면 <b>부상으로 몇 달을 통째로 잃습니다</b>. 휴식은 안전하지만 한 달을 버립니다.
/// </para>
/// <para>
/// 여기에 <b>컨디션</b>이 매달 변동해서, "이번 달은 절호조니까 밀어붙일까"라는
/// 판단이 생깁니다. 이게 육성의 묘미를 만드는 최소 장치입니다.
/// </para>
/// 근거: docs/04-game-design.md §5.5
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
    /// 파급 효과(0.29 → 0)와 피로 페널티(2.94 → 3) 같은 미세한 차이가 통째로 사라집니다.
    /// 실제로 그 버그를 겪었습니다. 반올림은 <see cref="Complete"/>에서 딱 한 번만 합니다.
    /// </para>
    /// </summary>
    private readonly double[] _accumulated = new double[7];

    private int _recoveryMonthsRemaining;
    private bool _injuredThisYear;

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

    /// <summary>다음에 진행할 달 (1~12). 12를 마치면 <see cref="MonthsCompleted"/>가 12가 됩니다.</summary>
    public int CurrentMonth => _months.Count + 1;

    public int MonthsCompleted => _months.Count;

    public bool IsComplete => _months.Count >= TrainingRules.MonthsPerYear;

    public int Fatigue { get; private set; }

    public Condition Condition { get; private set; }

    /// <summary>요양으로 인해 남은 개월 수. 0보다 크면 선택이 무시됩니다.</summary>
    public int RecoveryMonthsRemaining => _recoveryMonthsRemaining;

    public IReadOnlyList<MonthOutcome> Months => _months;

    /// <summary>한 달을 진행합니다.</summary>
    public MonthOutcome AdvanceMonth(TrainingFocus focus)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("이미 12개월을 모두 진행했습니다. Complete()를 호출하세요.");
        }

        int month = CurrentMonth;

        // 요양 중이면 무엇을 고르든 쉬게 됩니다.
        bool recovering = _recoveryMonthsRemaining > 0;
        if (recovering)
        {
            _recoveryMonthsRemaining--;
            focus = TrainingFocus.Rest;
        }

        MonthOutcome outcome = focus == TrainingFocus.Rest
            ? DoRest(month, recovering)
            : DoTraining(month, focus);

        _months.Add(outcome);
        return outcome;
    }

    /// <summary>본체 능력치 + 아직 확정되지 않은 연중 누적.</summary>
    private double EffectiveStat(PrimaryStat kind) => _adventurer.Stats[kind] + _accumulated[(int)kind];

    private MonthOutcome DoRest(int month, bool recovering)
    {
        // 휴식이 얼마나 회복시켰는지 보여줍니다. 이게 안 보이면 "휴식 한 달"의 값어치를
        // 플레이어가 계산할 수 없어서, 쉬는 선택이 그냥 버리는 달처럼 느껴집니다.
        int before = Fatigue;
        Fatigue = Math.Max(0, Fatigue - TrainingRules.FatigueRecoveryOnRest);
        DriftCondition(restBonus: true);

        string note = recovering
            ? $"{month}월: 요양 (남은 {_recoveryMonthsRemaining}개월, 피로 {before} → {Fatigue})"
            : $"{month}월: 휴식 (피로 {before} → {Fatigue}, 컨디션 {Condition.ToKorean()})";

        return new MonthOutcome(month, TrainingFocus.Rest, PrimaryStats.Zero, Fatigue, Condition, false, recovering, note);
    }

    private MonthOutcome DoTraining(int month, TrainingFocus focus)
    {
        var growth = _adventurer.Growth;
        double bloom = growth.BloomFactorAt(_adventurer.Age);
        double multiplier =
            bloom
            * growth.TrainingMultiplier
            * _mentorship.TrainingMultiplier
            * Condition.Multiplier()
            * FatiguePenalty();

        var focusedKind = ToStatKind(focus);

        // 등급 판정용: 컨디션과 피로가 만든 배율이 평상시 대비 얼마나 좋은가.
        double qualityRatio = Condition.Multiplier() * FatiguePenalty();

        // 이번 달 훈련이 실제로 이루어진 컨디션. 아래에서 컨디션이 변동하므로 미리 잡아둡니다.
        // (표시에 변동 후 컨디션을 쓰면 "대성공인데 컨디션 저조" 같은 모순이 화면에 나옵니다.)
        var trainedUnder = Condition;

        // 부상 판정 전이므로 아직 확정하지 않고 후보만 계산합니다.
        var pending = new double[_accumulated.Length];

        foreach (var kind in PrimaryStats.AllStats)
        {
            double current = EffectiveStat(kind);
            int potential = growth.Potential[kind];
            if (potential <= current) continue;

            double share = kind == focusedKind ? 1.0 : TrainingRules.SpilloverRatio;
            double variance = 0.85 + _rng.NextDouble() * 0.3;
            pending[(int)kind] = (potential - current) * TrainingRules.MonthlyLearnRate * multiplier * share * variance;
        }

        Fatigue = Math.Min(TrainingRules.MaxFatigue, Fatigue + TrainingRules.FatiguePerTraining);
        DriftCondition(restBonus: false);

        bool injured = RollInjury();
        var gain = PrimaryStats.Zero;
        string note;
        MonthGrade grade;

        if (injured)
        {
            _injuredThisYear = true;
            _recoveryMonthsRemaining = _rng.NextInt(TrainingRules.RecoveryMonthsMin, TrainingRules.RecoveryMonthsMax + 1);
            Fatigue = 0;
            Condition = Condition.Poor;

            // 부상당하면 그 달의 성장은 없던 일이 되고, 능력치도 조금 잃습니다.
            foreach (var kind in PrimaryStats.AllStats)
            {
                double loss = EffectiveStat(kind) * TrainingRules.InjuryStatLoss;
                _accumulated[(int)kind] -= loss;
                gain = gain.With(kind, -(int)Math.Round(loss));
            }

            note = $"{month}월: 무리한 훈련으로 부상 — {_recoveryMonthsRemaining}개월 요양";
            grade = MonthGrade.Failure;
        }
        else
        {
            foreach (var kind in PrimaryStats.AllStats)
            {
                _accumulated[(int)kind] += pending[(int)kind];
                gain = gain.With(kind, (int)Math.Round(pending[(int)kind]));
            }

            // 실제로 돌려보니 12개월 내내 "성공"만 떠서 화면이 단조로웠습니다.
            // 양호(1.12) 이상이면 대성공, 저조(0.88) 이하면 부진으로 폭을 넓혔습니다.
            grade = qualityRatio switch
            {
                >= 1.10 => MonthGrade.GreatSuccess,
                <= 0.90 => MonthGrade.Poor,
                _ => MonthGrade.Success
            };

            note = $"{month}월: {ToKorean(focus)} 훈련 · {grade.ToKorean()} " +
                   $"(컨디션 {trainedUnder.ToKorean()} → {Condition.ToKorean()}, 피로 {Fatigue})";
        }

        return new MonthOutcome(month, focus, gain, Fatigue, Condition, injured, false, note, grade);
    }

    /// <summary>피로가 임계치를 넘으면 성장이 떨어집니다.</summary>
    private double FatiguePenalty()
    {
        if (Fatigue <= TrainingRules.FatigueSoftCap) return 1.0;

        int excess = Fatigue - TrainingRules.FatigueSoftCap;
        int range = TrainingRules.MaxFatigue - TrainingRules.FatigueSoftCap;
        return Math.Max(0.35, 1.0 - (double)excess / range * 0.65);
    }

    private bool RollInjury()
    {
        if (Fatigue <= TrainingRules.InjuryThreshold) return false;

        double chance = (Fatigue - TrainingRules.InjuryThreshold) * TrainingRules.InjuryChancePerFatiguePoint;
        return _rng.Chance(chance);
    }

    /// <summary>
    /// 컨디션은 매달 흔들립니다. 피로가 높으면 나빠지는 쪽으로 치우칩니다.
    /// <para>
    /// 문턱을 ±1.0으로 두었더니 실제 플레이에서 거의 언제나 "보통"이라
    /// 컨디션을 보고 판단할 일이 생기지 않았습니다. ±0.6으로 낮춰 실제로 출렁이게 했습니다.
    /// 컨디션이 움직이지 않으면 "좋은 달을 알아보는 것"이 실력 축이 될 수 없습니다.
    /// </para>
    /// </summary>
    private void DriftCondition(bool restBonus)
    {
        double fatigueBias = -(double)Fatigue / TrainingRules.MaxFatigue * 1.0;
        double restEffect = restBonus ? 0.7 : 0.0;
        double roll = _rng.NextGaussian() + fatigueBias + restEffect;

        int step = roll switch
        {
            < -1.5 => -2,
            < -0.6 => -1,
            > 1.5 => +2,
            > 0.6 => +1,
            _ => 0
        };

        int next = Math.Clamp((int)Condition + step, (int)Condition.Terrible, (int)Condition.Excellent);
        Condition = (Condition)next;
    }

    /// <summary>12개월을 마치고 결과를 모험가에게 적용합니다.</summary>
    public YearRecord Complete()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException($"아직 {TrainingRules.MonthsPerYear - MonthsCompleted}개월이 남았습니다.");
        }

        // 노화는 연 단위로 한 번 적용합니다.
        // 반올림은 여기서 딱 한 번 — 월 단위로 반올림하면 미세한 차이가 전부 사라집니다.
        double decline = _adventurer.Growth.DeclineFactorAt(_adventurer.Age);
        var change = PrimaryStats.Zero;

        foreach (var kind in PrimaryStats.AllStats)
        {
            double total = _accumulated[(int)kind];
            if (decline > 0.0) total -= _adventurer.Stats[kind] * decline;
            change = change.With(kind, (int)Math.Round(total));
        }

        string note = _injuredThisYear
            ? $"{_adventurer.Age}세: 훈련 (부상으로 일부 기간 요양)"
            : _mentorship.TrainingMultiplier > 1.0
                ? $"{_adventurer.Age}세: {_mentorship.MentorName}의 지도 아래 훈련"
                : $"{_adventurer.Age}세: 훈련";

        var record = new YearRecord(_adventurer.Age, YearActivity.Training, change, null, 0, note);
        _adventurer.ApplyYear(record);
        _adventurer.GainJudgement(CareerRules.JudgementFromTraining);
        return record;
    }

    private static PrimaryStat ToStatKind(TrainingFocus focus) => focus switch
    {
        TrainingFocus.Strength => PrimaryStat.Strength,
        TrainingFocus.Agility => PrimaryStat.Agility,
        TrainingFocus.Finesse => PrimaryStat.Finesse,
        TrainingFocus.Vitality => PrimaryStat.Vitality,
        TrainingFocus.Intellect => PrimaryStat.Intellect,
        TrainingFocus.Spirit => PrimaryStat.Spirit,
        _ => throw new ArgumentOutOfRangeException(nameof(focus), focus, "Rest는 능력치에 대응하지 않습니다.")
    };

    private static string ToKorean(TrainingFocus focus) =>
        focus == TrainingFocus.Rest ? "휴식" : ToStatKind(focus).ToKorean();
}
