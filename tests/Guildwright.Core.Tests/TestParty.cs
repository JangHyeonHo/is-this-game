using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Tests;

/// <summary>테스트용 전투원 구성 헬퍼.</summary>
internal static class TestParty
{
    /// <summary>기본 전술: 위험하면 회복, 마무리 가능하면 마무리, 아니면 공격.</summary>
    internal static readonly TacticRule[] SensibleTactics =
    [
        TacticRule.SelfHpBelow(0.35, TacticAction.UsePotion),
        TacticRule.EnemyHpBelow(0.25, TacticAction.AttackWeakest),
        TacticRule.Always(TacticAction.AttackNearest)
    ];

    /// <summary>비교용 나쁜 전술: 무조건 가장 가까운 적만 때립니다.</summary>
    internal static readonly TacticRule[] NaiveTactics =
    [
        TacticRule.Always(TacticAction.AttackNearest)
    ];

    /// <summary>후퇴를 아는 전술.</summary>
    internal static readonly TacticRule[] RetreatingTactics =
    [
        TacticRule.SelfHpBelow(0.35, TacticAction.MoveBack),
        TacticRule.Always(TacticAction.AttackNearest)
    ];

    internal static PrimaryStats BaseStats(int agility = 10) => new(
        Strength: 20, Agility: agility, Finesse: 15,
        Vitality: 34, Intellect: 12, Spirit: 18);

    internal static Combatant Make(
        string id,
        Team team,
        int judgement,
        IReadOnlyList<TacticRule>? tactics = null,
        PrimaryStats? stats = null,
        Loadout? loadout = null,
        Row row = Row.Front,
        double weaponEffectiveness = 1.0,
        int potions = 2,
        IReadOnlyList<SkillId>? actives = null,
        IReadOnlyList<SkillId>? passives = null)
    {
        return new Combatant(
            id: id,
            name: id,
            team: team,
            stats: stats ?? BaseStats(),
            judgement: judgement,
            loadout: loadout ?? Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield),
            weaponEffectiveness: weaponEffectiveness,
            row: row,
            tactics: tactics ?? SensibleTactics,
            potions: potions,
            passives: passives,
            actives: actives);
    }

    /// <summary>
    /// 판단력과 전술만 다르고 나머지는 완전히 동일한 3 대 3 전투.
    /// 능력치가 같으므로 승률 차이는 오직 의사결정 품질에서만 나옵니다.
    /// </summary>
    internal static BattleState MirrorMatch(
        int playerJudgement,
        int enemyJudgement,
        IReadOnlyList<TacticRule>? playerTactics = null,
        IReadOnlyList<TacticRule>? enemyTactics = null,
        int partySize = 3,
        Loadout? loadout = null)
    {
        var combatants = new List<Combatant>(partySize * 2);

        for (int i = 0; i < partySize; i++)
        {
            // 속도를 서로 다르게 주어 턴 순서 동점 상황을 줄입니다.
            var stats = BaseStats(agility: 10 + i);
            combatants.Add(Make($"P{i}", Team.Player, playerJudgement, playerTactics, stats, loadout));
            combatants.Add(Make($"E{i}", Team.Enemy, enemyJudgement, enemyTactics, stats, loadout));
        }

        return new BattleState(combatants);
    }
}
