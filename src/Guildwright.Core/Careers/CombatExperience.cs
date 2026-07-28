using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Careers;

/// <summary>
/// 실전에서 겪은 것이 무엇을 키우는지.
/// <para>
/// <b>훈련은 무엇을 단련할지 고르지만, 실전은 무엇을 겪었는지가 정합니다.</b>
/// 앞에서 두들겨 맞은 캐릭터는 활력이 자라고, 뒤에서 마법만 쓴 캐릭터는 지능이 자랍니다.
/// </para>
/// <para>
/// 두 가지가 함께 나옵니다.
/// <list type="bullet">
///   <item><b>원천 능력치 가중치</b> — 어느 원천이 더 자랄지. 총량은 그대로고 방향만 바뀝니다.</item>
///   <item><b>파생 보정</b> — 계산식과 무관하게 직접 붙는 값.
///     계속 맞다 보면 몸이 단단해지고, 급소를 노리다 보면 손에 익습니다.</item>
/// </list>
/// </para>
/// <para>
/// 파생 보정 덕분에 <b>원천 능력치가 같아도 이력이 다르면 다른 캐릭터</b>가 됩니다.
/// </para>
/// 근거: docs/04-game-design.md §5.7
/// </summary>
public sealed class CombatExperience
{
    /// <summary>전혀 쓰지 않은 능력치도 이만큼은 자랍니다. 0이면 역할 변경이 불가능해집니다.</summary>
    private const double MinWeight = 0.35;

    /// <summary>한 능력치에 성장이 지나치게 쏠리는 것을 막습니다.</summary>
    private const double MaxWeight = 2.2;

    private readonly Dictionary<PrimaryStat, double> _weights;
    private readonly Dictionary<DerivedStat, double> _bonuses;

    private CombatExperience(
        Dictionary<PrimaryStat, double> weights,
        Dictionary<DerivedStat, double>? bonuses = null)
    {
        _weights = weights;
        _bonuses = bonuses ?? [];
    }

    public double WeightOf(PrimaryStat stat) => _weights[stat];

    /// <summary>그 해에 얻은 파생 보정.</summary>
    public double BonusFor(DerivedStat stat) => _bonuses.GetValueOrDefault(stat);

    public IEnumerable<KeyValuePair<DerivedStat, double>> Bonuses => _bonuses;

    /// <summary>모든 능력치가 균등하게 자랍니다.</summary>
    public static CombatExperience Uniform { get; } =
        new(PrimaryStats.AllStats.ToDictionary(k => k, _ => 1.0));

    /// <summary>실제 전투 기록으로부터 성장 방향과 파생 보정을 만듭니다.</summary>
    public static CombatExperience From(CombatContribution c)
    {
        var raw = new Dictionary<PrimaryStat, double>
        {
            // 휘두른 만큼 힘이 붙습니다.
            [PrimaryStat.Strength] = c.PhysicalDamageDealt,
            // 읽고 외운 만큼 지능이 붙습니다.
            [PrimaryStat.Intellect] = c.MagicDamageDealt,
            // 회복과 보조는 정신력에서 나옵니다.
            [PrimaryStat.Spirit] = c.HealingDone + c.SupportActions * 18.0,
            // 맞아본 만큼 몸이 버팁니다.
            [PrimaryStat.Vitality] = c.TotalDamageTaken,
            // 움직이고 피한 만큼 발이 빨라집니다.
            [PrimaryStat.Agility] = c.Repositions * 30.0 + c.Evasions * 25.0,
            // 급소를 노린 만큼 손끝이 정밀해집니다.
            [PrimaryStat.Finesse] = c.CriticalHits * 40.0 + c.Actions * 3.0
        };

        // ★ 파생 보정 — 계산식을 거치지 않고 직접 붙습니다.
        var bonuses = new Dictionary<DerivedStat, double>();

        void Bonus(DerivedStat stat, double amount)
        {
            if (amount > 0.0) bonuses[stat] = amount;
        }

        Bonus(DerivedStat.PhysicalGuard, c.PhysicalDamageTaken / 220.0);
        Bonus(DerivedStat.MagicGuard, c.MagicDamageTaken / 220.0);
        Bonus(DerivedStat.MaxHp, c.TotalDamageTaken / 90.0);
        Bonus(DerivedStat.CritChance, c.CriticalHits * 0.0035);
        Bonus(DerivedStat.EvasionChance, c.Evasions * 0.0030);
        Bonus(DerivedStat.ActionSpeed, c.Repositions * 0.25);

        return new CombatExperience(NormalizeWeights(raw), bonuses);
    }

    /// <summary>
    /// 실제 전투를 돌리지 않을 때 쓰는 근사.
    /// <para>
    /// 배치 시뮬레이션이나 전투 해석기를 붙이지 않은 경로에서 씁니다.
    /// 스타일과 위치만으로 "대체로 이런 걸 겪었을 것"을 추정합니다.
    /// </para>
    /// </summary>
    public static CombatExperience FromRole(WeaponStyle style, Row row)
    {
        var raw = PrimaryStats.AllStats.ToDictionary(k => k, _ => 0.0);
        void Add(PrimaryStat stat, double value) => raw[stat] += value;

        switch (style)
        {
            case WeaponStyle.SwordAndShield:
                Add(PrimaryStat.Vitality, 95); Add(PrimaryStat.Strength, 55); Add(PrimaryStat.Spirit, 30);
                break;

            case WeaponStyle.TwoHanded:
                Add(PrimaryStat.Strength, 105); Add(PrimaryStat.Vitality, 60); Add(PrimaryStat.Finesse, 25);
                break;

            case WeaponStyle.DualWield:
                Add(PrimaryStat.Agility, 85); Add(PrimaryStat.Finesse, 75); Add(PrimaryStat.Strength, 45);
                break;

            case WeaponStyle.Polearm:
                Add(PrimaryStat.Strength, 80); Add(PrimaryStat.Finesse, 55); Add(PrimaryStat.Agility, 45);
                break;

            case WeaponStyle.Bow:
                Add(PrimaryStat.Finesse, 90); Add(PrimaryStat.Agility, 70); Add(PrimaryStat.Strength, 30);
                break;

            case WeaponStyle.Crossbow:
                Add(PrimaryStat.Finesse, 95); Add(PrimaryStat.Strength, 45); Add(PrimaryStat.Spirit, 30);
                break;

            case WeaponStyle.Staff:
                Add(PrimaryStat.Intellect, 105); Add(PrimaryStat.Spirit, 85);
                break;
        }

        // 전열에 서 있었다면 더 맞았을 것입니다.
        var bonuses = new Dictionary<DerivedStat, double>();
        if (row == Row.Front)
        {
            Add(PrimaryStat.Vitality, 55);
            bonuses[DerivedStat.PhysicalGuard] = 0.8;
            bonuses[DerivedStat.MaxHp] = 2.0;
        }

        return new CombatExperience(NormalizeWeights(raw), bonuses);
    }

    private static Dictionary<PrimaryStat, double> NormalizeWeights(IReadOnlyDictionary<PrimaryStat, double> raw)
    {
        double total = raw.Values.Sum();
        int count = PrimaryStats.AllStats.Count;

        if (total <= 0.0)
        {
            return PrimaryStats.AllStats.ToDictionary(k => k, _ => 1.0);
        }

        var weights = new Dictionary<PrimaryStat, double>(count);
        foreach (var stat in PrimaryStats.AllStats)
        {
            // 균등 분포(share = 1/n)일 때 정확히 1.0이 되도록 맞춥니다.
            // 그래야 "무엇을 했느냐"가 총 성장량을 늘리거나 줄이지 않고, 방향만 바꿉니다.
            double share = raw[stat] / total;
            weights[stat] = Math.Clamp(MinWeight + (1.0 - MinWeight) * share * count, MinWeight, MaxWeight);
        }

        return weights;
    }

    public override string ToString() =>
        string.Join(" ", PrimaryStats.AllStats.Select(k => $"{k.ToKorean()}:{_weights[k]:F2}"));
}
