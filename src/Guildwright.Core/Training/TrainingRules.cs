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
    /// 활동 가중치 1.0짜리 능력치가 한 달에 흡수하는 남은 잠재력의 비율.
    /// <para>
    /// 활동마다 가중치 합계가 1.5 안팎이므로, 한 달에 흡수하는 총량은
    /// 이전(집중 1.0 + 파급 0.08 × 5 = 1.4)과 비슷한 규모입니다.
    /// </para>
    /// <para>
    /// <b>파급(spillover)은 없앴습니다.</b> 활동이 이미 여러 능력치에 걸쳐 있어서
    /// "한 능력치만 미는 게 언제나 정답"이 되는 걸 막을 필요가 사라졌습니다.
    /// </para>
    /// </summary>
    public const double MonthlyLearnRate = 0.09;

    /// <summary>훈련 한 번의 피로 누적.</summary>
    public const int FatiguePerTraining = 17;

    /// <summary>
    /// 훈련에 <b>실패</b>했을 때의 피로 누적.
    /// <para>
    /// 성장도 없는데 피로만 더 쌓이므로 실패는 휴식보다 확실히 나쁩니다.
    /// 그리고 <b>실패가 연쇄를 부릅니다</b> — 피로가 더 쌓이니 다음 달 실패 확률이 올라갑니다.
    /// 부상 없이도 "무리하면 무너진다"가 성립합니다.
    /// </para>
    /// </summary>
    public const int FatigueOnFailure = 25;

    /// <summary>휴식 한 번의 피로 회복.</summary>
    public const int FatigueRecoveryOnRest = 38;

    /// <summary>이 피로도를 넘으면 성장이 떨어지기 시작합니다.</summary>
    public const int FatigueSoftCap = 45;

    /// <summary>
    /// 이 피로도를 넘으면 훈련이 실패할 수 있습니다.
    /// <para>
    /// 예전에는 이 선을 넘으면 <b>부상</b>이 나서 2~4개월 요양했습니다.
    /// 육성에도 부상, 실전에도 부상은 중복이라 육성 쪽을 걷어냈습니다.
    /// 육성의 대가는 <b>잃어버린 한 달</b>로 충분합니다.
    /// </para>
    /// </summary>
    public const int FailureThreshold = 50;

    /// <summary>임계치 초과 피로 1당 실패 확률.</summary>
    public const double FailureChancePerFatiguePoint = 0.012;

    /// <summary>실패해도 이만큼은 자랍니다. 완전히 0이면 한 달이 통째로 증발한 느낌이라 가혹합니다.</summary>
    public const double FailureGrowthRatio = 0.15;

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
