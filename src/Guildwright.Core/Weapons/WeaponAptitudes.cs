using Guildwright.Core.Adventurers;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Weapons;

/// <summary>무기 적성 등급. 숙련도가 오르는 속도를 결정합니다.</summary>
public enum AptitudeGrade
{
    E,
    D,
    C,
    B,
    A,
    S
}

public static class AptitudeGrades
{
    /// <summary>숙련도 성장 배율.</summary>
    public static double GrowthMultiplier(this AptitudeGrade grade) => grade switch
    {
        AptitudeGrade.E => 0.25,
        AptitudeGrade.D => 0.50,
        AptitudeGrade.C => 0.80,
        AptitudeGrade.B => 1.10,
        AptitudeGrade.A => 1.45,
        AptitudeGrade.S => 1.90,
        _ => 1.0
    };
}

/// <summary>
/// 스타일별 무기 적성.
/// <para>
/// <b>능력치 잠재력과 상관관계를 갖되 결정적이지는 않습니다.</b>
/// 완전 독립으로 굴리면 "마공 최고인데 대검 적성"같은 모순 캐릭터가 나와서
/// 개성이 아니라 불량품이 되고, 완전 종속이면 굴릴 이유가 없어집니다.
/// </para>
/// 근거: docs/04-game-design.md §3.5
/// </summary>
public sealed class WeaponAptitudes
{
    private readonly Dictionary<WeaponStyle, AptitudeGrade> _grades;

    private WeaponAptitudes(Dictionary<WeaponStyle, AptitudeGrade> grades) => _grades = grades;

    public AptitudeGrade this[WeaponStyle style] => _grades[style];

    /// <summary>가장 잘 맞는 스타일.</summary>
    public WeaponStyle Best =>
        _grades.OrderByDescending(kv => kv.Value)
               .ThenBy(kv => kv.Key)
               .First().Key;

    public IEnumerable<KeyValuePair<WeaponStyle, AptitudeGrade>> All =>
        _grades.OrderBy(kv => kv.Key);

    /// <summary>테스트·기본값용. 모든 스타일이 같은 등급.</summary>
    public static WeaponAptitudes Uniform(AptitudeGrade grade) =>
        new(WeaponStyles.All.ToDictionary(s => s, _ => grade));

    public static WeaponAptitudes Of(IReadOnlyDictionary<WeaponStyle, AptitudeGrade> grades)
    {
        var full = WeaponStyles.All.ToDictionary(
            s => s,
            s => grades.TryGetValue(s, out var g) ? g : AptitudeGrade.C);
        return new WeaponAptitudes(full);
    }

    /// <summary>
    /// 능력치 잠재력과 상관관계를 갖는 적성을 굴립니다.
    /// </summary>
    public static WeaponAptitudes Roll(StatBlock potential, IRandomSource rng)
    {
        double average = potential.Total / (double)StatBlock.AllKinds.Count;
        if (average <= 0.0) return Uniform(AptitudeGrade.C);

        var grades = new Dictionary<WeaponStyle, AptitudeGrade>();

        foreach (var style in WeaponStyles.All)
        {
            var affinity = WeaponStyles.AffinityOf(style);

            double weighted = 0.0, weightSum = 0.0;
            foreach (var (stat, weight) in affinity)
            {
                weighted += potential[stat] * weight;
                weightSum += weight;
            }

            // 1.0이면 그 캐릭터의 평균적인 능력치 수준.
            double relative = weightSum > 0.0 ? weighted / weightSum / average : 1.0;

            // 노이즈가 있어야 가끔 예상 밖의 재능이 나옵니다.
            double score = relative + rng.NextGaussian() * 0.18;

            grades[style] = ToGrade(score);
        }

        // ★ 안전장치: 모든 스타일이 낮으면 그 캐릭터는 그냥 하자품이 됩니다.
        //    5년 키워서 알아낸 게 "얘는 못 쓴다"면 긴장이 아니라 처벌입니다.
        if (grades.Values.Max() < AptitudeGrade.B)
        {
            var best = grades.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
            grades[best] = AptitudeGrade.A;
        }

        return new WeaponAptitudes(grades);
    }

    private static AptitudeGrade ToGrade(double score) => score switch
    {
        >= 1.28 => AptitudeGrade.S,
        >= 1.13 => AptitudeGrade.A,
        >= 0.98 => AptitudeGrade.B,
        >= 0.85 => AptitudeGrade.C,
        >= 0.72 => AptitudeGrade.D,
        _ => AptitudeGrade.E
    };

    public override string ToString() =>
        string.Join(" ", All.Select(kv => $"{kv.Key.ToKorean()}:{kv.Value}"));
}
