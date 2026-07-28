namespace Guildwright.Core.Training;

/// <summary>
/// 월 단위 훈련의 밸런스 상수.
/// <para>
/// ⚠️ <b>전부 임시값입니다.</b> 배치 시뮬레이션으로 검증한 뒤 데이터 파일로 분리합니다.
/// 감으로 고치지 말고 근거를 docs/06-balance-log.md에 남기세요.
/// </para>
/// </summary>
public static class TrainingRules
{
    public const int MonthsPerYear = 12;

    /// <summary>
    /// 집중한 능력치가 한 달에 흡수하는 남은 잠재력의 비율.
    /// <para>
    /// 특화 방침(3능력치)이면 한 능력치가 연 3회쯤 집중 훈련을 받아 연 30% 안팎을 흡수하고,
    /// 균형 방침(7능력치)이면 연 15% 안팎에 그칩니다. <b>퍼뜨리면 손해</b>라는 구조를 의도한 값입니다.
    /// </para>
    /// </summary>
    public const double MonthlyLearnRate = 0.09;

    /// <summary>
    /// 집중하지 않은 능력치가 받는 비율.
    /// <para>
    /// 0으로 두면 한 능력치만 미는 게 언제나 정답이 되어 선택이 사라집니다.
    /// 반대로 너무 크면 무엇을 고르든 비슷해져서 역시 선택이 사라집니다.
    /// </para>
    /// </summary>
    public const double SpilloverRatio = 0.08;

    /// <summary>훈련 한 번의 피로 누적.</summary>
    public const int FatiguePerTraining = 17;

    /// <summary>휴식 한 번의 피로 회복.</summary>
    public const int FatigueRecoveryOnRest = 38;

    /// <summary>이 피로도를 넘으면 성장이 떨어지기 시작합니다.</summary>
    public const int FatigueSoftCap = 45;

    /// <summary>이 피로도를 넘으면 훈련 중 부상 위험이 생깁니다.</summary>
    public const int InjuryThreshold = 65;

    /// <summary>임계치 초과 피로 1당 부상 확률.</summary>
    public const double InjuryChancePerFatiguePoint = 0.0055;

    /// <summary>훈련 부상 시 요양 개월 수의 범위.</summary>
    public const int RecoveryMonthsMin = 2;
    public const int RecoveryMonthsMax = 4;

    /// <summary>훈련 부상 시 잃는 능력치 비율.</summary>
    public const double InjuryStatLoss = 0.04;

    public const int MaxFatigue = 100;
}

/// <summary>컨디션. 매달 변동하며 성장에 곱해집니다.</summary>
public enum Condition
{
    Terrible,
    Poor,
    Normal,
    Good,
    Excellent
}

public static class ConditionExtensions
{
    public static double Multiplier(this Condition condition) => condition switch
    {
        Condition.Terrible => 0.70,
        Condition.Poor => 0.88,
        Condition.Normal => 1.00,
        Condition.Good => 1.12,
        Condition.Excellent => 1.30,
        _ => 1.0
    };

    public static string ToKorean(this Condition condition) => condition switch
    {
        Condition.Terrible => "최악",
        Condition.Poor => "저조",
        Condition.Normal => "보통",
        Condition.Good => "양호",
        Condition.Excellent => "절호조",
        _ => "?"
    };
}
