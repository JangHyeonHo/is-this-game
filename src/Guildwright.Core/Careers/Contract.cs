namespace Guildwright.Core.Careers;

/// <summary>의뢰의 성격.</summary>
public enum ContractKind
{
    /// <summary>토벌·호위 등. 전투력이 성패를 좌우합니다.</summary>
    Combat,
    /// <summary>채집·채광. 마을의 재료 요구에 응하는 의뢰로, 전투 비중이 낮습니다.</summary>
    Gathering,
    /// <summary>탐색·정찰. 위험하지만 전투보다 정보와 판단이 중요합니다.</summary>
    Exploration
}

/// <summary>
/// 길드가 받는 의뢰.
/// <para>
/// <b>난이도 하나로 끝나지 않고 "어떤 역량이 유리한가"를 함께 갖습니다.</b>
/// 이게 있어야 파티 편성이 전투력 순으로 줄 세우기가 아니게 됩니다.
/// </para>
/// <para>
/// 던전 탐험 층을 따로 만들지 않고, 함정·척후·운반의 효과를 여기서 흡수합니다.
/// 근거: docs/04-game-design.md §5.8
/// </para>
/// </summary>
/// <param name="Name">표시용 이름.</param>
/// <param name="Kind">성격.</param>
/// <param name="Difficulty">난이도. 보수와 위험을 좌우합니다.</param>
/// <param name="Preferences">유리한 역량과 그 비중(0.0~1.0).</param>
public sealed record Contract(
    string Name,
    ContractKind Kind,
    int Difficulty,
    IReadOnlyDictionary<SupportSkill, double> Preferences)
{
    public static Contract Combat(string name, int difficulty, IReadOnlyDictionary<SupportSkill, double>? preferences = null) =>
        new(name, ContractKind.Combat, difficulty, preferences ?? new Dictionary<SupportSkill, double>());

    /// <summary>
    /// 전투 비중.
    /// <para>
    /// 채집 의뢰는 전투가 거의 없으므로, <b>전투력이 낮은 캐릭터도 제 몫을 할 자리</b>가 됩니다.
    /// </para>
    /// </summary>
    public double CombatWeight => Kind switch
    {
        ContractKind.Combat => 1.0,
        ContractKind.Exploration => 0.6,
        ContractKind.Gathering => 0.25,
        _ => 1.0
    };

    public double PreferenceOf(SupportSkill skill) =>
        Preferences.TryGetValue(skill, out double value) ? value : 0.0;

    public override string ToString()
    {
        string prefs = Preferences.Count == 0
            ? "없음"
            : string.Join(", ", Preferences.OrderByDescending(kv => kv.Value)
                                           .Select(kv => $"{kv.Key.ToKorean()}({kv.Value:F1})"));
        return $"[{Name}] 난이도 {Difficulty} · {Kind} · 유리: {prefs}";
    }
}

/// <summary>
/// 파티가 의뢰에 가져오는 비전투 역량의 총합과 그 효과.
/// </summary>
/// <param name="RiskMultiplier">사고 위험 배율. 낮을수록 안전합니다.</param>
/// <param name="IncomeMultiplier">보수 배율.</param>
/// <param name="ExtraPotions">운반 역량으로 추가로 들고 가는 회복약.</param>
/// <param name="AppraisalBonus">감정 역량 기여 (0.0~1.0).</param>
public sealed record ContractSupport(
    double RiskMultiplier,
    double IncomeMultiplier,
    int ExtraPotions,
    double AppraisalBonus);

public static class ContractResolver
{
    /// <summary>역량이 아무리 높아도 위험을 이만큼 아래로는 못 낮춥니다.</summary>
    private const double MinRiskMultiplier = 0.45;

    /// <summary>보수 배율 상한.</summary>
    private const double MaxIncomeMultiplier = 1.6;

    /// <summary>
    /// 파티의 비전투 역량이 의뢰에 미치는 영향을 계산합니다.
    /// <para>
    /// <b>파티에서 가장 잘하는 사람 기준</b>입니다. 함정을 찾는 데 다섯 명이 다 필요하진 않습니다.
    /// 대신 운반은 합산합니다 — 짐은 나눠 들 수 있으니까요.
    /// </para>
    /// </summary>
    public static ContractSupport Evaluate(Contract contract, IReadOnlyList<SupportSkillSet> party)
    {
        if (party.Count == 0)
        {
            return new ContractSupport(1.0, 1.0, 0, 0.0);
        }

        int BestAt(SupportSkill skill) => party.Max(p => p[skill]);
        int SumAt(SupportSkill skill) => party.Sum(p => p[skill]);

        // --- 위험 감소: 함정 감지와 척후 ---
        double trapWeight = contract.PreferenceOf(SupportSkill.TrapSense);
        double scoutWeight = contract.PreferenceOf(SupportSkill.Scouting);

        double riskReduction =
            trapWeight * (BestAt(SupportSkill.TrapSense) / (double)SupportSkills.Max) * 0.40 +
            scoutWeight * (BestAt(SupportSkill.Scouting) / (double)SupportSkills.Max) * 0.35;

        double risk = Math.Max(MinRiskMultiplier, 1.0 - riskReduction);

        // --- 보수 증가: 운반과 채집 ---
        double porterWeight = contract.PreferenceOf(SupportSkill.Portering);
        double gatherWeight = contract.PreferenceOf(SupportSkill.Gathering);

        // 운반은 합산입니다 — 짐은 나눠 들 수 있으니 사람이 늘면 더 챙깁니다.
        // 인원수로 나누면 평균이 되어, 초보 짐꾼을 붙일수록 손해가 됩니다.
        // (처음에 그렇게 구현했다가 "짐꾼 3명이 1명보다 못하다"는 결과가 나왔습니다.)
        // 숙련자 두 사람이면 포화하도록 고정된 기준으로 나눕니다.
        double porterTotal = Math.Min(1.0, SumAt(SupportSkill.Portering) / (double)(SupportSkills.Max * 2));

        double incomeBonus =
            porterWeight * porterTotal * 0.30 +
            gatherWeight * (BestAt(SupportSkill.Gathering) / (double)SupportSkills.Max) * 0.55;

        double income = Math.Min(MaxIncomeMultiplier, 1.0 + incomeBonus);

        // --- 운반이 좋으면 회복약을 더 들고 갑니다 ---
        int extraPotions = SumAt(SupportSkill.Portering) / 120;

        // --- 감정은 길드의 신입 평가 정확도에 기여합니다 ---
        double appraisal = Math.Clamp(BestAt(SupportSkill.Appraisal) / (double)SupportSkills.Max * 0.6, 0.0, 0.6);

        return new ContractSupport(risk, income, extraPotions, appraisal);
    }
}
