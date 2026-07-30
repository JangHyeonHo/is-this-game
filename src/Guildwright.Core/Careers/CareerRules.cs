namespace Guildwright.Core.Careers;

/// <summary>
/// 경력 시뮬레이션의 밸런스 상수.
/// <para>
/// ⚠️ <b>여기 있는 수치는 전부 임시값입니다.</b> 배치 시뮬레이션으로 검증한 뒤
/// 데이터 파일로 분리할 예정입니다. 감으로 고치지 말고 근거를 docs/08-balance-log.md에 남기세요.
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

    // ── 전투 결과가 그 해에 미치는 영향 ───────────────────────
    //
    // 전투에서 지고도 보수를 받고 멀쩡히 돌아오면, 전투를 보는 의미가 사라집니다.
    // (실제로 "쓰러졌는데 보수 144를 받고 승급"하는 장면을 콘솔에서 봤습니다.)
    //
    // ⚠️ 아래 배율은 아직 배치 시뮬레이션으로 검증하지 않은 임시값입니다.
    //    docs/08-balance-log.md #23 참조.

    /// <summary>전투에서 패배했을 때 사고 위험 배율.</summary>
    public const double DefeatRiskMultiplier = 3.0;

    /// <summary>결판이 나지 않았을 때 사고 위험 배율.</summary>
    public const double DrawRiskMultiplier = 1.6;

    /// <summary>전투 중 본인이 쓰러졌을 때 추가로 곱해지는 위험 배율.</summary>
    public const double DownedRiskMultiplier = 2.2;

    /// <summary>결판이 나지 않았을 때 받는 보수 비율. 의뢰를 절반만 해낸 셈입니다.</summary>
    public const double DrawIncomeRatio = 0.4;
}
