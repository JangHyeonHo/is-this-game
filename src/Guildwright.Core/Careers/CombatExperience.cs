using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Careers;

/// <summary>
/// 실전에서 겪은 것이 어떤 능력치를 키우는지.
/// <para>
/// <b>훈련은 무엇을 단련할지 고르지만, 실전은 무엇을 겪었는지가 정합니다.</b>
/// 앞에서 두들겨 맞은 캐릭터는 체력과 방어가 자라고, 뒤에서 마법만 쓴 캐릭터는 마력과 마공이 자랍니다.
/// </para>
/// <para>
/// 이 구조 덕분에 <b>파티 편성과 전술 편성이 육성에도 영향을 미칩니다.</b>
/// 탱커로 굴리면 탱커로 자라고, 후열에 세워두면 맞을 일이 없어 체력이 잘 안 큽니다.
/// 시스템이 여기서 한 바퀴 닫힙니다.
/// </para>
/// 근거: docs/04-game-design.md §5.7
/// </summary>
public sealed class CombatExperience
{
    /// <summary>전혀 쓰지 않은 능력치도 이만큼은 자랍니다.</summary>
    private const double MinWeight = 0.35;

    /// <summary>한 능력치에 성장이 지나치게 쏠리는 것을 막습니다.</summary>
    private const double MaxWeight = 2.2;

    private readonly Dictionary<StatKind, double> _weights;

    private CombatExperience(Dictionary<StatKind, double> weights) => _weights = weights;

    public double WeightOf(StatKind kind) => _weights[kind];

    /// <summary>모든 능력치가 균등하게 자랍니다.</summary>
    public static CombatExperience Uniform { get; } =
        new(StatBlock.AllKinds.ToDictionary(k => k, _ => 1.0));

    /// <summary>실제 전투 기록으로부터 성장 가중치를 만듭니다.</summary>
    public static CombatExperience From(CombatContribution contribution)
    {
        // 마법을 얼마나 썼는지는 가한 마법 피해 + 회복량으로 봅니다.
        double magicUse = contribution.MagicDamageDealt + contribution.HealingDone;

        var raw = new Dictionary<StatKind, double>
        {
            [StatKind.Attack] = contribution.PhysicalDamageDealt,
            [StatKind.MagicAttack] = contribution.MagicDamageDealt,
            [StatKind.Mana] = magicUse + contribution.SupportActions * 12.0,
            [StatKind.Vitality] = contribution.TotalDamageTaken,
            [StatKind.Defense] = contribution.PhysicalDamageTaken,
            [StatKind.MagicDefense] = contribution.MagicDamageTaken,
            // 기동은 이동 횟수로 봅니다. 자주 위치를 바꿨다면 발이 빨라진 것입니다.
            [StatKind.Speed] = contribution.Repositions * 25.0 + contribution.Actions * 2.0
        };

        return FromRawScores(raw);
    }

    /// <summary>
    /// 실제 전투를 돌리지 않을 때 쓰는 근사.
    /// <para>
    /// 배치 시뮬레이션이나 아직 전투 해석기를 붙이지 않은 경로에서 씁니다.
    /// 스타일과 위치만으로 "대체로 이런 걸 겪었을 것"을 추정합니다.
    /// </para>
    /// </summary>
    public static CombatExperience FromRole(WeaponStyle style, Row row)
    {
        var raw = StatBlock.AllKinds.ToDictionary(k => k, _ => 0.0);

        void Add(StatKind kind, double value) => raw[kind] += value;

        switch (style)
        {
            case WeaponStyle.SwordAndShield:
                Add(StatKind.Attack, 45); Add(StatKind.Vitality, 90);
                Add(StatKind.Defense, 90); Add(StatKind.Mana, 20);
                break;

            case WeaponStyle.TwoHanded:
                Add(StatKind.Attack, 100); Add(StatKind.Vitality, 60); Add(StatKind.Defense, 45);
                break;

            case WeaponStyle.DualWield:
                Add(StatKind.Attack, 85); Add(StatKind.Speed, 70); Add(StatKind.Vitality, 45);
                break;

            case WeaponStyle.Polearm:
                Add(StatKind.Attack, 80); Add(StatKind.Defense, 45); Add(StatKind.Speed, 45);
                break;

            case WeaponStyle.Bow:
                Add(StatKind.Attack, 75); Add(StatKind.Speed, 75); Add(StatKind.MagicDefense, 20);
                break;

            case WeaponStyle.Crossbow:
                Add(StatKind.Attack, 90); Add(StatKind.Vitality, 35); Add(StatKind.Defense, 30);
                break;

            case WeaponStyle.Staff:
                Add(StatKind.MagicAttack, 95); Add(StatKind.Mana, 95); Add(StatKind.MagicDefense, 45);
                break;
        }

        // 전열에 서 있었다면 더 맞았을 것입니다.
        if (row == Row.Front)
        {
            Add(StatKind.Vitality, 45);
            Add(StatKind.Defense, 35);
        }

        return FromRawScores(raw);
    }

    private static CombatExperience FromRawScores(IReadOnlyDictionary<StatKind, double> raw)
    {
        double total = raw.Values.Sum();
        int count = StatBlock.AllKinds.Count;

        if (total <= 0.0) return Uniform;

        var weights = new Dictionary<StatKind, double>(count);
        foreach (var kind in StatBlock.AllKinds)
        {
            // 균등 분포(share = 1/n)일 때 정확히 1.0이 되도록 맞춥니다.
            // 그래야 "무엇을 했느냐"가 총 성장량을 늘리거나 줄이지 않고, 방향만 바꿉니다.
            double share = raw[kind] / total;
            double weight = MinWeight + (1.0 - MinWeight) * share * count;
            weights[kind] = Math.Clamp(weight, MinWeight, MaxWeight);
        }

        return new CombatExperience(weights);
    }

    public override string ToString() =>
        string.Join(" ", StatBlock.AllKinds.Select(k => $"{k}:{_weights[k]:F2}"));
}
