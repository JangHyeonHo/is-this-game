using Guildwright.Core.Combat;

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

    internal static Combatant Make(
        string id,
        Team team,
        int judgement,
        IReadOnlyList<TacticRule>? tactics = null,
        int maxHp = 100,
        int attack = 20,
        int defense = 10,
        int agility = 10,
        int potions = 2)
    {
        return new Combatant(
            id: id,
            name: id,
            team: team,
            maxHp: maxHp,
            attack: attack,
            defense: defense,
            agility: agility,
            judgement: judgement,
            potions: potions,
            tactics: tactics ?? SensibleTactics);
    }

    /// <summary>
    /// 판단력과 전술만 다르고 나머지 능력치는 완전히 동일한 3 대 3 전투를 만듭니다.
    /// 능력치가 같으므로, 승률 차이는 오직 의사결정 품질에서만 나옵니다.
    /// </summary>
    internal static BattleState MirrorMatch(
        int playerJudgement,
        int enemyJudgement,
        IReadOnlyList<TacticRule>? playerTactics = null,
        IReadOnlyList<TacticRule>? enemyTactics = null,
        int partySize = 3)
    {
        var combatants = new List<Combatant>(partySize * 2);

        for (int i = 0; i < partySize; i++)
        {
            // 민첩을 서로 다르게 주어 턴 순서 동점 상황을 줄입니다.
            int agility = 10 + i;
            combatants.Add(Make($"P{i}", Team.Player, playerJudgement, playerTactics, agility: agility));
            combatants.Add(Make($"E{i}", Team.Enemy, enemyJudgement, enemyTactics, agility: agility));
        }

        return new BattleState(combatants);
    }
}
