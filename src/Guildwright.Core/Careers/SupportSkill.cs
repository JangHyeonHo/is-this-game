using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Careers;

/// <summary>
/// 비전투 역량.
/// <para>
/// <b>목적: 전투력만으로 캐릭터를 평가하지 않게 하는 것.</b>
/// 전투는 못하지만 함정을 기가 막히게 찾는 아이를 데리고 있을 이유가 생겨야 합니다.
/// </para>
/// <para>
/// ⚠️ <b>이 역량들은 던전 탐험 층을 만들지 않고 의뢰 해석에 흡수됩니다.</b>
/// 함정 감지 판정을 실시간으로 굴리는 대신, 의뢰의 위험도와 보수를 조정합니다.
/// 나중에 실제 탐험을 붙이더라도 이 데이터 구조는 그대로 씁니다.
/// 근거: docs/00-charter.md §4 (스코프 방어선), docs/04-game-design.md §5.8
/// </para>
/// </summary>
public enum SupportSkill
{
    /// <summary>함정 감지·해제. 의뢰의 사고 위험을 줄입니다.</summary>
    TrapSense,
    /// <summary>척후. 지형과 적을 미리 파악해 위험을 줄이고 좋은 의뢰를 물어옵니다.</summary>
    Scouting,
    /// <summary>운반. 전리품을 더 챙기고 회복약을 더 들고 갑니다.</summary>
    Portering,
    /// <summary>채집·채광. 재료 의뢰의 수확량을 좌우합니다.</summary>
    Gathering,
    /// <summary>감정. 신입의 재능을 알아봅니다. 길드의 감정 역량에 기여합니다.</summary>
    Appraisal
}

public static class SupportSkills
{
    public static IReadOnlyList<SupportSkill> All { get; } = Enum.GetValues<SupportSkill>();

    /// <summary>역량 상한.</summary>
    public const int Max = 100;

    /// <summary>실전 1년을 그 역할로 보냈을 때의 기본 상승치.</summary>
    public const double PerAssignedYear = 13.0;

    /// <summary>맡지 않은 역량도 어깨너머로 조금은 늡니다.</summary>
    public const double PassiveGain = 1.5;

    /// <summary>
    /// 이 역량을 좌우하는 능력치.
    /// <para>
    /// <b>비전투 역량에는 별도의 적성 랜덤을 두지 않습니다.</b>
    /// 이미 개화 시기·기질·능력치 잠재력·무기 적성으로 랜덤 축이 충분히 많습니다.
    /// 대신 기존 능력치가 성장 속도를 좌우하므로, 캐릭터마다 자연스럽게 잘 맞는 역할이 갈립니다.
    /// </para>
    /// </summary>
    public static PrimaryStat GoverningStat(this SupportSkill skill) => skill switch
    {
        // 함정을 알아보는 것은 지식입니다.
        SupportSkill.TrapSense => PrimaryStat.Intellect,
        // 척후는 발이 빨라야 합니다.
        SupportSkill.Scouting => PrimaryStat.Agility,
        // 짐은 힘으로 집니다.
        SupportSkill.Portering => PrimaryStat.Strength,
        // 채굴과 채집은 손끝의 정밀함입니다.
        SupportSkill.Gathering => PrimaryStat.Finesse,
        // 사람과 물건을 알아보는 것도 지식입니다.
        SupportSkill.Appraisal => PrimaryStat.Intellect,
        _ => PrimaryStat.Vitality
    };

    public static string ToKorean(this SupportSkill skill) => skill switch
    {
        SupportSkill.TrapSense => "함정 감지",
        SupportSkill.Scouting => "척후",
        SupportSkill.Portering => "운반",
        SupportSkill.Gathering => "채집",
        SupportSkill.Appraisal => "감정",
        _ => "?"
    };
}

/// <summary>
/// 한 모험가의 비전무 역량 수준. 무기 숙련도와 같은 원리로, <b>맡은 역할의 이력</b>입니다.
/// </summary>
public sealed class SupportSkillSet
{
    private readonly Dictionary<SupportSkill, double> _levels =
        SupportSkills.All.ToDictionary(s => s, _ => 0.0);

    public int this[SupportSkill skill] => (int)Math.Round(_levels[skill]);

    public SupportSkill Best =>
        _levels.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;

    /// <summary>
    /// 실전 1년을 보냈을 때 역량을 올립니다.
    /// </summary>
    /// <param name="assigned">그 해에 맡은 역할. 없으면 전 역량이 조금씩만 늡니다.</param>
    /// <param name="stats">성장 속도를 좌우하는 현재 능력치.</param>
    internal void AdvanceYear(SupportSkill? assigned, PrimaryStats stats)
    {
        foreach (var skill in SupportSkills.All)
        {
            double governing = stats[skill.GoverningStat()];

            // 능력치 50을 기준으로 0.5배 ~ 1.5배.
            double aptitude = Math.Clamp(0.5 + governing / 100.0, 0.5, 1.5);

            double baseGain = skill == assigned
                ? SupportSkills.PerAssignedYear
                : SupportSkills.PassiveGain;

            // 상한에 가까울수록 느려집니다.
            double remaining = SupportSkills.Max - _levels[skill];
            double gain = baseGain * aptitude * Math.Clamp(remaining / SupportSkills.Max, 0.12, 1.0);

            _levels[skill] = Math.Min(SupportSkills.Max, _levels[skill] + gain);
        }
    }

    public IEnumerable<KeyValuePair<SupportSkill, int>> All =>
        _levels.OrderBy(kv => kv.Key)
               .Select(kv => new KeyValuePair<SupportSkill, int>(kv.Key, (int)Math.Round(kv.Value)));

    public override string ToString() =>
        string.Join(" ", All.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key.ToKorean()}:{kv.Value}"));
}
