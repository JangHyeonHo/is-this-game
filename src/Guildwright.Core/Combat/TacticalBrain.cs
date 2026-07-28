using Guildwright.Core.Rng;

namespace Guildwright.Core.Combat;

/// <summary>실제로 실행될 행동 하나.</summary>
/// <param name="Action">행동 종류.</param>
/// <param name="Target">대상. 공격이 아니면 null.</param>
public readonly record struct ChosenAction(TacticAction Action, Combatant? Target);

/// <summary>
/// 전투 중 한 캐릭터가 무엇을 할지 결정하는 Utility AI.
/// <para>
/// <b>판단력(Judgement)이 이 결정의 품질을 좌우합니다.</b> 구현상으로는
/// 각 행동의 "진짜 가치"에 판단력에 반비례하는 노이즈를 섞어 "체감 가치"를 만들고,
/// 체감 가치가 가장 높은 행동을 고릅니다.
/// </para>
/// <para>
/// 결과적으로 판단력이 높으면 편성된 전술 규칙을 안정적으로 따르고 규칙이 없는 상황도
/// 잘 메우며, 낮으면 규칙을 자주 무시하고 엉뚱한 선택을 합니다.
/// 두 효과가 하나의 파라미터에서 자연스럽게 나옵니다.
/// </para>
/// 근거: docs/04-game-design.md §4.2
/// </summary>
public static class TacticalBrain
{
    /// <summary>편성된 규칙에 부여하는 가산점. 노이즈보다 충분히 커야 규칙이 기본적으로 이깁니다.</summary>
    private const double RuleBonus = 0.8;

    /// <summary>판단력 0일 때의 노이즈 표준편차.</summary>
    private const double MaxNoise = 1.1;

    public static ChosenAction Decide(Combatant self, BattleState state, IRandomSource rng)
    {
        var allies = state.LivingMembersOf(self.Team);
        var enemies = state.LivingOpponentsOf(self.Team);

        // 적이 모두 죽었으면 할 일이 없습니다.
        if (enemies.Count == 0)
        {
            return new ChosenAction(TacticAction.Defend, null);
        }

        TacticAction? ruleAction = MatchRule(self, allies, enemies);

        // 판단력이 낮을수록 체감 가치가 흔들립니다.
        double noiseScale = (1.0 - self.Judgement / 100.0) * MaxNoise;

        ChosenAction best = default;
        double bestScore = double.NegativeInfinity;

        foreach (var candidate in EnumerateLegalActions(self, enemies))
        {
            double score = TrueUtility(self, candidate, enemies);

            if (ruleAction.HasValue && candidate.Action == ruleAction.Value)
            {
                score += RuleBonus;
            }

            if (noiseScale > 0.0)
            {
                score += rng.NextGaussian() * noiseScale;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>위에서부터 순서대로 평가해 처음 맞는 규칙의 행동을 반환합니다.</summary>
    private static TacticAction? MatchRule(
        Combatant self,
        IReadOnlyList<Combatant> allies,
        IReadOnlyList<Combatant> enemies)
    {
        foreach (var rule in self.Tactics)
        {
            bool matched = rule.Condition switch
            {
                TacticCondition.Always => true,
                TacticCondition.SelfHpBelow => self.HpRatio < rule.Threshold,
                TacticCondition.AllyHpBelow => allies.Any(a => a.HpRatio < rule.Threshold),
                TacticCondition.EnemyHpBelow => enemies.Any(e => e.HpRatio < rule.Threshold),
                _ => false
            };

            if (!matched) continue;

            // 회복약이 없는데 UsePotion을 지시하는 규칙은 건너뜁니다.
            if (rule.Action == TacticAction.UsePotion && self.Potions <= 0) continue;

            return rule.Action;
        }

        return null;
    }

    /// <summary>지금 실제로 할 수 있는 행동들. 열거 순서는 결정론적이어야 합니다.</summary>
    private static List<ChosenAction> EnumerateLegalActions(
        Combatant self,
        IReadOnlyList<Combatant> enemies)
    {
        var actions = new List<ChosenAction>(5);

        actions.Add(new ChosenAction(TacticAction.AttackNearest, enemies[0]));
        actions.Add(new ChosenAction(TacticAction.AttackWeakest, PickWeakest(enemies)));
        actions.Add(new ChosenAction(TacticAction.AttackStrongest, PickStrongest(enemies)));

        if (self.Potions > 0 && self.Hp < self.MaxHp)
        {
            actions.Add(new ChosenAction(TacticAction.UsePotion, null));
        }

        actions.Add(new ChosenAction(TacticAction.Defend, null));

        return actions;
    }

    /// <summary>
    /// 행동의 "진짜 가치". 노이즈가 0일 때(판단력 100) 최적에 가까운 선택이 나오도록 설계합니다.
    /// </summary>
    private static double TrueUtility(
        Combatant self,
        ChosenAction candidate,
        IReadOnlyList<Combatant> enemies)
    {
        switch (candidate.Action)
        {
            case TacticAction.UsePotion:
                // HP가 낮을수록 가치가 급격히 오릅니다. 가득 찼을 때 쓰는 건 낭비입니다.
                return 1.6 * (1.0 - self.HpRatio) * (1.0 - self.HpRatio);

            case TacticAction.Defend:
                // 회복약이 없고 위험할 때의 차선책.
                double urgency = 1.0 - self.HpRatio;
                double potionPenalty = self.Potions > 0 ? 0.5 : 1.0;
                return 0.7 * urgency * urgency * potionPenalty;

            case TacticAction.AttackNearest:
            case TacticAction.AttackWeakest:
            case TacticAction.AttackStrongest:
            {
                var target = candidate.Target;
                if (target is null) return 0.0;

                double score = 0.5;

                // 이번 턴에 죽일 수 있으면 큰 가치 — 적 하나를 지우면 받는 피해가 줄어듭니다.
                int expected = DamageModel.ExpectedDamage(self, target);
                if (expected >= target.Hp)
                {
                    score += 0.6;
                }
                else
                {
                    // 죽일 수 없어도 빈사인 적을 때리는 게 낫습니다.
                    score += 0.25 * (1.0 - target.HpRatio);
                }

                // 위협적인 적을 우선하는 약한 선호.
                int maxAttack = enemies.Max(e => e.Attack);
                if (maxAttack > 0)
                {
                    score += 0.15 * ((double)target.Attack / maxAttack);
                }

                return score;
            }

            default:
                return 0.0;
        }
    }

    // 동점일 때 순서가 흔들리면 결정론이 깨지므로, Id로 타이브레이크합니다.
    private static Combatant PickWeakest(IReadOnlyList<Combatant> enemies) =>
        enemies.OrderBy(e => e.Hp).ThenBy(e => e.Id, StringComparer.Ordinal).First();

    private static Combatant PickStrongest(IReadOnlyList<Combatant> enemies) =>
        enemies.OrderByDescending(e => e.Attack).ThenBy(e => e.Id, StringComparer.Ordinal).First();
}
