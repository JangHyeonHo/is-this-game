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
    /// <param name="adventurer">전투원이 될 모험가.</param>
    /// <param name="team">소속.</param>
    /// <param name="row">시작 위치.</param>
    /// <param name="tactics">전술 규칙. 생략하면 무기 스타일의 기본값.</param>
    /// <param name="potions">들고 있는 회복약.</param>
    /// <param name="startingHp">
    /// 전투 시작 HP. 생략하면 최대치입니다.
    /// <para>
    /// 파견 연도는 12개월 동안 여러 번 싸우고 <b>전투 사이에 저절로 회복되지 않습니다.</b>
    /// 그래야 "지금 싸울까 피할까"가 판단이 되고 야영과 포션이 의미를 갖습니다.
    /// </para>
    /// </param>
    public static Combatant Create(
        Adventurer adventurer,
        Team team,
        Row row,
        IReadOnlyList<TacticRule>? tactics = null,
        int potions = 2,
        int? startingHp = null)
    {
        if (!adventurer.IsAlive)
        {
            throw new ArgumentException($"{adventurer.Name}은(는) 전투에 나갈 수 없습니다 (상태: {adventurer.Status}).", nameof(adventurer));
        }

        var combatant = new Combatant(
            id: adventurer.Id,
            name: adventurer.Name,
            team: team,
            stats: adventurer.Stats,
            judgement: adventurer.Judgement,
            loadout: adventurer.Loadout,
            weaponEffectiveness: adventurer.WeaponEffectiveness,
            bonuses: adventurer.Bonuses,
            row: row,
            tactics: tactics ?? DefaultTacticsFor(adventurer.Loadout),
            potions: potions);

        if (startingHp is { } hp && hp < combatant.MaxHp)
        {
            combatant.TakeDamage(combatant.MaxHp - Math.Max(1, hp));
        }

        return combatant;
    }

    /// <summary>
    /// 손에 든 것에 맞는 기본 전술 규칙.
    /// <para>
    /// 플레이어가 편성하기 전의 출발점입니다. 이것만으로도 그럭저럭 싸워야
    /// 신규 플레이어가 규칙 편집 화면에서 막히지 않습니다.
    /// </para>
    /// <para>
    /// ⚠️ 예전에는 <c>WeaponStyle</c> 7종에 대한 <c>switch</c>였습니다. 이제 스타일이라는
    /// 개념이 없으므로 <b>손에 든 것의 성질</b>로 판단합니다 — 무기를 추가해도
    /// 이 함수를 안 건드립니다.
    /// </para>
    /// </summary>
    public static IReadOnlyList<TacticRule> DefaultTacticsFor(Loadout loadout)
    {
        // 가방을 들었으면 싸울 수 없습니다. 살아남는 것과 아군을 돕는 것만 합니다.
        if (loadout.CarryingPack)
        {
            return
            [
                TacticRule.AllyHpBelow(0.40, TacticAction.GivePotion),
                TacticRule.When(TacticCondition.SelfInFrontRow, TacticAction.MoveBack),
                TacticRule.Always(TacticAction.Defend)
            ];
        }

        // 방패를 들었으면 앞에서 버티고 끌어당깁니다.
        if (loadout.Holding(WeaponKind.Shield))
        {
            return
            [
                TacticRule.SelfHpBelow(0.30, TacticAction.UsePotion),
                TacticRule.When(TacticCondition.SelfInBackRow, TacticAction.MoveFront),
                TacticRule.AllyHpBelow(0.50, TacticAction.Taunt),
                TacticRule.Always(TacticAction.AttackNearest)
            ];
        }

        // 지팡이는 회복을 먼저 봅니다.
        if (loadout.UsesMagicPower)
        {
            return
            [
                TacticRule.AllyHpBelow(0.45, TacticAction.HealAlly),
                TacticRule.SelfHpBelow(0.40, TacticAction.MoveBack),
                TacticRule.EnemyHpBelow(0.30, TacticAction.AttackBackRow),
                TacticRule.Always(TacticAction.AttackNearest)
            ];
        }

        // 원거리는 뒤에서 적 후열을 노립니다.
        if (loadout.CanStrikeBackRow)
        {
            return
            [
                TacticRule.SelfHpBelow(0.45, TacticAction.MoveBack),
                TacticRule.Always(TacticAction.AttackBackRow)
            ];
        }

        // 리치가 길면 뒤에 서서도 칩니다.
        if (loadout.CanActFromBackRow)
        {
            return
            [
                TacticRule.SelfHpBelow(0.35, TacticAction.MoveBack),
                TacticRule.EnemyHpBelow(0.30, TacticAction.AttackWeakest),
                TacticRule.Always(TacticAction.AttackNearest)
            ];
        }

        // 광역을 쓸 수 있으면 그걸 기본으로 둡니다 (대검 숙련이 열어줍니다).
        return
        [
            TacticRule.SelfHpBelow(0.30, TacticAction.UsePotion),
            TacticRule.EnemyHpBelow(0.35, TacticAction.AttackWeakest),
            TacticRule.Always(TacticAction.AttackNearest)
        ];
    }

    /// <summary>
    /// 모험가 파티를 전투 대형으로 배치합니다.
    /// <para>
    /// 후열에서도 제 몫을 하는 스타일(활·석궁·지팡이·창)은 뒤로, 나머지는 앞으로 보냅니다.
    /// 전열이 아무도 없으면 가장 튼튼한 사람을 앞으로 끌어냅니다 —
    /// 전열이 빈 채로 시작하면 첫 라운드에 후열이 통째로 노출됩니다.
    /// </para>
    /// </summary>
    /// <param name="playerParty">아군.</param>
    /// <param name="enemyParty">적군.</param>
    /// <param name="carriedHp">
    /// 아군이 이어받는 HP (모험가 Id → 남은 HP). 파견 연도에서 씁니다.
    /// </param>
    /// <param name="carriedPotions">아군이 들고 있는 회복약 (Id → 개수).</param>
    public static BattleState FormParty(
        IEnumerable<Adventurer> playerParty,
        IEnumerable<Adventurer> enemyParty,
        IReadOnlyDictionary<string, int>? carriedHp = null,
        IReadOnlyDictionary<string, int>? carriedPotions = null)
    {
        var combatants = new List<Combatant>();
        combatants.AddRange(Arrange(playerParty, Team.Player, carriedHp, carriedPotions));
        combatants.AddRange(Arrange(enemyParty, Team.Enemy));
        return new BattleState(combatants);
    }

    private static List<Combatant> Arrange(
        IEnumerable<Adventurer> party,
        Team team,
        IReadOnlyDictionary<string, int>? carriedHp = null,
        IReadOnlyDictionary<string, int>? carriedPotions = null)
    {
        var members = party.ToList();
        var rows = members.ToDictionary(
            a => a.Id,
            a => a.Loadout.CanActFromBackRow ? Row.Back : Row.Front);

        if (members.Count > 0 && rows.Values.All(r => r == Row.Back))
        {
            var toughest = members.OrderByDescending(a => a.Stats.Vitality + a.Stats.Strength)
                                  .ThenBy(a => a.Id, StringComparer.Ordinal)
                                  .First();
            rows[toughest.Id] = Row.Front;
        }

        return members
            .Select(a => Create(
                a, team, rows[a.Id],
                potions: carriedPotions?.GetValueOrDefault(a.Id, 2) ?? 2,
                startingHp: carriedHp?.GetValueOrDefault(a.Id)))
            .ToList();
    }
}
