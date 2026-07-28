using Guildwright.Core.Adventurers;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

/// <summary>
/// 육성한 모험가를 전투원으로 변환합니다.
/// <para>
/// <b>이 클래스가 육성 파트와 전투 파트를 잇는 유일한 다리입니다.</b>
/// 여기가 없으면 두 시스템이 각자 검증될 뿐 한 줄로 이어지지 않습니다.
/// </para>
/// </summary>
public static class CombatantFactory
{
    public static Combatant Create(
        Adventurer adventurer,
        Team team,
        Row row,
        IReadOnlyList<TacticRule>? tactics = null,
        int potions = 2)
    {
        if (!adventurer.IsAlive)
        {
            throw new ArgumentException($"{adventurer.Name}은(는) 전투에 나갈 수 없습니다 (상태: {adventurer.Status}).", nameof(adventurer));
        }

        return new Combatant(
            id: adventurer.Id,
            name: adventurer.Name,
            team: team,
            stats: adventurer.Stats,
            judgement: adventurer.Judgement,
            style: adventurer.EquippedStyle,
            weaponEffectiveness: adventurer.WeaponEffectiveness,
            row: row,
            tactics: tactics ?? DefaultTacticsFor(adventurer.EquippedStyle),
            potions: potions);
    }

    /// <summary>
    /// 스타일에 맞는 기본 전술 규칙.
    /// <para>
    /// 플레이어가 편성하기 전의 출발점입니다. 이것만으로도 그럭저럭 싸워야
    /// 신규 플레이어가 규칙 편집 화면에서 막히지 않습니다.
    /// </para>
    /// </summary>
    public static IReadOnlyList<TacticRule> DefaultTacticsFor(WeaponStyle style) => style switch
    {
        WeaponStyle.SwordAndShield =>
        [
            TacticRule.SelfHpBelow(0.30, TacticAction.UsePotion),
            TacticRule.When(TacticCondition.SelfInBackRow, TacticAction.MoveFront),
            TacticRule.AllyHpBelow(0.50, TacticAction.Taunt),
            TacticRule.Always(TacticAction.AttackNearest)
        ],

        WeaponStyle.Staff =>
        [
            TacticRule.AllyHpBelow(0.45, TacticAction.HealAlly),
            TacticRule.SelfHpBelow(0.40, TacticAction.MoveBack),
            TacticRule.EnemyHpBelow(0.30, TacticAction.AttackBackRow),
            TacticRule.Always(TacticAction.AttackNearest)
        ],

        WeaponStyle.Bow or WeaponStyle.Crossbow =>
        [
            TacticRule.SelfHpBelow(0.45, TacticAction.MoveBack),
            TacticRule.Always(TacticAction.AttackBackRow)
        ],

        WeaponStyle.TwoHanded =>
        [
            TacticRule.SelfHpBelow(0.25, TacticAction.UsePotion),
            TacticRule.EnemyHpBelow(0.30, TacticAction.AttackWeakest),
            TacticRule.Always(TacticAction.AttackAll)
        ],

        WeaponStyle.DualWield =>
        [
            TacticRule.SelfHpBelow(0.30, TacticAction.UsePotion),
            TacticRule.SelfHpBelow(0.20, TacticAction.MoveBack),
            TacticRule.EnemyHpBelow(0.35, TacticAction.AttackWeakest),
            TacticRule.Always(TacticAction.AttackNearest)
        ],

        WeaponStyle.Polearm =>
        [
            TacticRule.SelfHpBelow(0.35, TacticAction.MoveBack),
            TacticRule.EnemyHpBelow(0.30, TacticAction.AttackWeakest),
            TacticRule.Always(TacticAction.AttackNearest)
        ],

        _ => [TacticRule.Always(TacticAction.AttackNearest)]
    };

    /// <summary>
    /// 모험가 파티를 전투 대형으로 배치합니다.
    /// <para>
    /// 후열에서도 제 몫을 하는 스타일(활·석궁·지팡이·창)은 뒤로, 나머지는 앞으로 보냅니다.
    /// 전열이 아무도 없으면 가장 튼튼한 사람을 앞으로 끌어냅니다 —
    /// 전열이 빈 채로 시작하면 첫 라운드에 후열이 통째로 노출됩니다.
    /// </para>
    /// </summary>
    public static BattleState FormParty(
        IEnumerable<Adventurer> playerParty,
        IEnumerable<Adventurer> enemyParty)
    {
        var combatants = new List<Combatant>();
        combatants.AddRange(Arrange(playerParty, Team.Player));
        combatants.AddRange(Arrange(enemyParty, Team.Enemy));
        return new BattleState(combatants);
    }

    private static List<Combatant> Arrange(IEnumerable<Adventurer> party, Team team)
    {
        var members = party.ToList();
        var rows = members.ToDictionary(
            a => a.Id,
            a => WeaponStyles.CapabilityOf(a.EquippedStyle).CanActFromBackRow ? Row.Back : Row.Front);

        if (members.Count > 0 && rows.Values.All(r => r == Row.Back))
        {
            var toughest = members.OrderByDescending(a => a.Stats.Vitality + a.Stats.Defense)
                                  .ThenBy(a => a.Id, StringComparer.Ordinal)
                                  .First();
            rows[toughest.Id] = Row.Front;
        }

        return members.Select(a => Create(a, team, rows[a.Id])).ToList();
    }
}
