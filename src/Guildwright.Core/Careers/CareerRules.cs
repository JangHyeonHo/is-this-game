namespace Guildwright.Core.Careers;

/// <summary>
/// 경력 시뮬레이션의 밸런스 상수.
/// <para>
/// ⚠️ <b>여기 있는 수치는 전부 임시값입니다.</b> 배치 시뮬레이션으로 검증한 뒤
/// 데이터 파일로 분리할 예정입니다. 감으로 고치지 말고 근거를 docs/06-balance-log.md에 남기세요.
/// </para>
/// </summary>
public static class CareerRules
{
    /// <summary>한 해에 남은 잠재력의 몇 %를 흡수하는지.</summary>
    public const double LearnRate = 0.32;

    /// <summary>실전 1년당 판단력 상승. 실전이 판단력을 키웁니다.</summary>
    public const int JudgementFromDeployment = 6;

    /// <summary>훈련 1년당 판단력 상승.</summary>
    public const int JudgementFromTraining = 2;

    /// <summary>의뢰 난이도 1당 요구되는 능력치 총합.</summary>
    public const double RequiredPowerPerDifficulty = 55.0;

    /// <summary>의뢰 난이도 1당 기본 보수.</summary>
    public const int IncomePerDifficulty = 120;

    /// <summary>실전 위험도 기본 계수.</summary>
    public const double BaseRisk = 0.055;

    /// <summary>판단력이 위험을 얼마나 줄여주는지 (판단력 100에서 최대 감소율).</summary>
    public const double JudgementRiskReduction = 0.45;

    /// <summary>사고가 났을 때 사망일 확률.</summary>
    public const double DeathShareOfMishap = 0.18;

    /// <summary>사고가 났을 때 불구일 확률 (사망 다음 구간).</summary>
    public const double CrippleShareOfMishap = 0.20;

    /// <summary>부상 시 잃는 능력치 비율의 범위.</summary>
    public const double InjuryLossMin = 0.05;
    public const double InjuryLossMax = 0.15;

    /// <summary>불구 시 잃는 능력치 비율.</summary>
    public const double CrippleLoss = 0.30;
}
