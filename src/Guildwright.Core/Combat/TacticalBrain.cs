using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

/// <summary>실제로 실행될 행동 하나.</summary>
/// <param name="Action">행동 종류.</param>
/// <param name="Target">대상. 대상이 없는 행동이면 null.</param>
public readonly record struct ChosenAction(TacticAction Action, Combatant? Target);

/// <summary>
/// 전투 중 한 캐릭터가 무엇을 할지 결정하는 Utility AI.
/// <para>
/// <b>판단력이 이 결정의 품질을 좌우합니다.</b> 각 행동의 "진짜 가치"에 판단력에 반비례하는
/// 노이즈를 섞어 "체감 가치"를 만들고, 체감 가치가 가장 높은 행동을 고릅니다.
/// </para>
/// <para>
/// 그래서 판단력이 높으면 편성된 규칙을 안정적으로 따르고 규칙이 없는 상황도 잘 메우며,
/// 낮으면 규칙을 무시하고 엉뚱한 선택을 합니다. 특히 <b>물러설 때를 모릅니다</b> —
/// 죽어가면서도 전열에 남아 있는 신입이 그래서 나옵니다.
/// </para>
/// 근거: docs/04-game-design.md §4.2
/// </summary>
public static class TacticalBrain
{
    /// <summary>편성된 규칙에 주는 기본 가산점.</summary>
    private const double RuleBonusBase = 0.9;

    /// <summary>판단력 0일 때의 노이즈 표준편차.</summary>
    private const double MaxNoise = 1.1;

    /// <summary>
    /// 규칙을 얼마나 맹목적으로 따르는가. <b>판단력이 낮을수록 높습니다.</b>
    /// <para>
    /// 처음에는 판단력과 무관하게 고정했는데, 그러면 판단력 100이
    /// "평범한 계획을 완벽하게 수행하는 것"이 되어 오히려 70보다 약해졌습니다.
    /// 기본 규칙이 "목록의 첫 적을 때려라"인데 그걸 완벽히 지키느라
    /// 빈사인 적을 두고 멀쩡한 적을 계속 때렸기 때문입니다. (docs/06 #12)
    /// </para>
    /// <para>
    /// 지금 모델: <b>낮은 판단력은 규칙을 기계적으로 따르되 실수가 잦고,
    /// 높은 판단력은 규칙을 지침으로 삼되 명백히 나은 수가 보이면 스스로 판단합니다.</b>
    /// 규칙 가산점이 여전히 크기 때문에 플레이어의 편성은 그대로 중요합니다 —
    /// 뒤집히는 것은 <i>확실히</i> 더 나은 경우뿐입니다.
    /// </para>
    /// </summary>
    private static double RuleWeightFor(int judgement) =>
        RuleBonusBase * (1.35 - 0.55 * judgement / 100.0);

    public static ChosenAction Decide(Combatant self, BattleState state, IRandomSource rng)
    {
        var allies = state.LivingMembersOf(self.Team);
        var enemies = state.LivingOpponentsOf(self.Team);

        if (enemies.Count == 0) return new ChosenAction(TacticAction.Defend, null);

        TacticAction? ruleAction = MatchRule(self, state, allies, enemies);
        double noiseScale = (1.0 - self.Judgement / 100.0) * MaxNoise;
        double ruleWeight = RuleWeightFor(self.Judgement);

        ChosenAction best = new(TacticAction.Defend, null);
        double bestScore = double.NegativeInfinity;

        foreach (var candidate in EnumerateLegalActions(self, state, allies, enemies))
        {
            double score = TrueUtility(self, state, candidate, allies, enemies);

            if (ruleAction.HasValue && candidate.Action == ruleAction.Value) score += ruleWeight;
            if (noiseScale > 0.0) score += rng.NextGaussian() * noiseScale;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static TacticAction? MatchRule(
        Combatant self,
        BattleState state,
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
                TacticCondition.SelfInFrontRow => self.Row == Row.Front,
                TacticCondition.SelfInBackRow => self.Row == Row.Back,
                TacticCondition.FrontRowEmpty => state.IsFrontRowEmpty(self.Team),
                _ => false
            };

            if (!matched) continue;
            if (!IsActionAvailable(self, rule.Action)) continue;

            return rule.Action;
        }

        return null;
    }

    /// <summary>
    /// 그 행동을 지금 할 수 있는가. <b>지휘 개입도 이 검사를 지나야 합니다</b> —
    /// 검사가 UI에만 있으면 배치 시뮬레이터가 규칙 밖의 행동을 시킬 수 있습니다.
    /// </summary>
    public static bool CanTake(Combatant self, TacticAction action) => IsActionAvailable(self, action);

    /// <summary>스타일과 현재 상태가 이 행동을 허용하는가.</summary>
    private static bool IsActionAvailable(Combatant self, TacticAction action) => action switch
    {
        TacticAction.UsePotion => self.Potions > 0,

        // 아래는 전부 스킬이 엽니다 — 무기가 아닙니다 (docs/08 §10 "스킬이 떠맡게 된 것").
        // 예전에는 후열 타격이 Loadout.CanStrikeBackRow로, 강화·약화가 UsesMagicPower로
        // 열렸습니다. 그러면 스킬 하나 없는 견습이 지팡이만 들어도 버프를 씁니다.
        TacticAction.AttackBackRow => self.CanAfford(TacticAction.AttackBackRow),
        TacticAction.AttackAll => self.CanAfford(TacticAction.AttackAll),
        TacticAction.HealAlly => self.CanAfford(TacticAction.HealAlly),
        TacticAction.BuffAlly or TacticAction.DebuffEnemy => self.CanAfford(action),
        TacticAction.Taunt => self.CanAfford(TacticAction.Taunt),
        TacticAction.GivePotion => self.Potions > 0 && self.CanAfford(TacticAction.GivePotion),
        TacticAction.MoveBack => self.Row == Row.Front,
        TacticAction.MoveFront => self.Row == Row.Back,
        _ => true
    };

    private static List<ChosenAction> EnumerateLegalActions(
        Combatant self,
        BattleState state,
        IReadOnlyList<Combatant> allies,
        IReadOnlyList<Combatant> enemies)
    {
        var actions = new List<ChosenAction>(12);
        var reachable = state.ReachableTargets(self);

        if (reachable.Count > 0)
        {
            actions.Add(new ChosenAction(TacticAction.AttackNearest, PickNearest(reachable)));
            actions.Add(new ChosenAction(TacticAction.AttackWeakest, PickWeakest(reachable)));
            actions.Add(new ChosenAction(TacticAction.AttackStrongest, PickStrongest(reachable)));
        }

        if (self.CanAfford(TacticAction.AttackBackRow))
        {
            var back = enemies.Where(e => e.Row == Row.Back).ToList();
            if (back.Count > 0) actions.Add(new ChosenAction(TacticAction.AttackBackRow, PickWeakest(back)));
        }

        if (self.CanAfford(TacticAction.AttackAll) && reachable.Count > 1)
        {
            actions.Add(new ChosenAction(TacticAction.AttackAll, null));
        }

        if (self.CanAfford(TacticAction.HealAlly))
        {
            var wounded = allies.OrderBy(a => a.HpRatio).ThenBy(a => a.Id, StringComparer.Ordinal).First();
            if (wounded.HpRatio < 0.999) actions.Add(new ChosenAction(TacticAction.HealAlly, wounded));
        }

        if (self.CanAfford(TacticAction.BuffAlly))
        {
            var strongestAlly = allies.OrderByDescending(a => a.EffectiveOffense).ThenBy(a => a.Id, StringComparer.Ordinal).First();
            if (!strongestAlly.HasEffect(EffectName.PowerUp))
            {
                actions.Add(new ChosenAction(TacticAction.BuffAlly, strongestAlly));
            }
        }

        if (self.CanAfford(TacticAction.DebuffEnemy))
        {
            var threat = PickStrongest(enemies);
            if (!threat.HasEffect(EffectName.PowerDown))
            {
                actions.Add(new ChosenAction(TacticAction.DebuffEnemy, threat));
            }
        }

        if (self.CanAfford(TacticAction.Taunt) && self.Row == Row.Front)
        {
            actions.Add(new ChosenAction(TacticAction.Taunt, null));
        }

        // 짐꾼의 회복약 건네기. 예전에는 후보에 아예 없어서 기본 전술 규칙이 조용히 무효였습니다.
        if (self.Potions > 0 && self.CanAfford(TacticAction.GivePotion))
        {
            var hurt = allies.Where(a => a.IsAlive && a.Id != self.Id && a.HpRatio < 0.999)
                             .OrderBy(a => a.HpRatio).ThenBy(a => a.Id, StringComparer.Ordinal)
                             .FirstOrDefault();
            if (hurt is not null) actions.Add(new ChosenAction(TacticAction.GivePotion, hurt));
        }

        if (self.Potions > 0 && self.Hp < self.MaxHp)
        {
            actions.Add(new ChosenAction(TacticAction.UsePotion, null));
        }

        // 포지션 변경은 언제나 후보입니다. 물러설 때를 아는 것이 판단력입니다.
        if (self.Row == Row.Front) actions.Add(new ChosenAction(TacticAction.MoveBack, null));
        else actions.Add(new ChosenAction(TacticAction.MoveFront, null));

        actions.Add(new ChosenAction(TacticAction.Defend, null));

        return actions;
    }

    /// <summary>
    /// 행동의 "진짜 가치". 노이즈가 0일 때(판단력 100) 최적에 가까운 선택이 나오도록 설계합니다.
    /// </summary>
    private static double TrueUtility(
        Combatant self,
        BattleState state,
        ChosenAction candidate,
        IReadOnlyList<Combatant> allies,
        IReadOnlyList<Combatant> enemies)
    {
        switch (candidate.Action)
        {
            case TacticAction.UsePotion:
                return 1.6 * Sq(1.0 - self.HpRatio);

            case TacticAction.HealAlly:
            {
                var target = candidate.Target;
                if (target is null) return 0.0;
                // 죽기 직전의 아군을 살리는 것이 최우선입니다.
                return 1.9 * Sq(1.0 - target.HpRatio);
            }

            case TacticAction.Defend:
            {
                double urgency = 1.0 - self.HpRatio;
                double potionPenalty = self.Potions > 0 ? 0.5 : 1.0;
                return 0.7 * Sq(urgency) * potionPenalty;
            }

            case TacticAction.MoveBack:
            {
                // ★ 후퇴 판단. 이 게임에서 판단력이 가장 눈에 띄게 드러나는 지점입니다.

                // 전열을 얼마나 얇게 만드는가.
                // 전열이 한 명만 남으면 적 전원이 그 한 명에게 몰리므로, 둘만 남는 것도 이미 위험합니다.
                int frontCount = state.LivingIn(self.Team, Row.Front).Count;
                double abandonPenalty = frontCount switch
                {
                    <= 1 => 0.10,
                    2 => 0.45,
                    _ => 1.0
                };

                // 후열에서도 제 몫을 하는 스타일(활·석궁·지팡이·창)은 후퇴 비용이 없습니다.
                // 근접 무기는 후열에서 위력이 절반 이하이므로 계산이 완전히 다릅니다.
                double stylePenalty = self.CanActFromBackRow ? 1.0 : 0.5;

                // 세제곱을 쓰는 이유: 제곱이면 HP 50%에서도 물러나기 시작하는데,
                // 그건 신중함이 아니라 그냥 딜을 버리는 것입니다.
                //
                // ⚠️ 후퇴를 "공격력 손실이 없으니 거의 항상 이득"으로 평가하도록 올려봤더니
                //    승률이 오히려 크게 떨어졌습니다(창 55% → 47%). 전열이 얇아지면 남은
                //    전열원에게 적 전원이 몰리기 때문으로 보입니다. 근거 없이 다시 올리지 마세요.
                double danger = Math.Pow(1.0 - self.HpRatio, 3.0);

                // ★ 회복할 수단이 없으면 후퇴는 그냥 천천히 지는 것입니다.
                //   물러나 봐야 죽는 시점만 늦출 뿐 그동안 아무것도 못 합니다.
                //   차라리 한 대라도 더 때리는 게 낫습니다.
                bool canRecover =
                    self.Potions > 0 ||
                    allies.Any(a => a.IsAlive && a.CanAfford(TacticAction.HealAlly));

                return 2.2 * danger * stylePenalty * abandonPenalty * (canRecover ? 1.0 : 0.3);
            }

            case TacticAction.MoveFront:
            {
                // 후열에서도 제 몫을 하는 스타일은 굳이 나갈 이유가 없습니다.
                if (self.CanActFromBackRow) return 0.05;

                // 근접 무기가 후열에 있으면 위력이 절반 이하입니다.
                // 회복해서 여유가 생겼으면 다시 나가야 합니다 — 후퇴는 편도가 아니어야 합니다.
                double needed = state.IsFrontRowEmpty(self.Team) ? 1.6 : 0.85;
                return needed * Sq(self.HpRatio);
            }

            case TacticAction.Taunt:
            {
                // 도발은 적 전원의 공격을 나 하나에게 모으고, 나는 방어 태세가 됩니다.
                // 튼튼할 때 써야 의미가 있습니다. 빈사에 도발하면 그냥 죽습니다.

                // 이미 내가 건 도발이 유효하면 다시 걸 이유가 없습니다.
                // 이 확인이 없으면 매 턴 도발만 반복하고 공격을 전혀 하지 않습니다.
                if (enemies.All(e => e.TauntedBy == self.Id)) return 0.0;

                // ⚠️ 이 계수를 1.5까지 올려봤더니 승률이 54% → 32%로 폭락했습니다.
                //    근거 없이 올리지 마세요. (docs/06 #12)
                double wounded = allies.Count(a => a.HpRatio < 0.5) / (double)Math.Max(1, allies.Count);
                return 0.9 * self.HpRatio * (0.4 + wounded);
            }

            case TacticAction.BuffAlly:
                return 0.75;

            case TacticAction.DebuffEnemy:
                return 0.70;

            case TacticAction.AttackAll:
            {
                int targets = state.ReachableTargets(self).Count;
                // 광역은 대상이 많을 때만 단일 공격보다 낫습니다.
                return 0.35 + 0.22 * targets;
            }

            case TacticAction.AttackBackRow:
            {
                var target = candidate.Target;
                if (target is null) return 0.0;
                // 후열은 대개 회복·마법 담당이라 우선순위가 높습니다.
                double priority = target.CanDo(TacticAction.HealAlly) ? 0.35 : 0.15;
                return AttackUtility(self, target, enemies) + priority;
            }

            case TacticAction.AttackNearest:
            case TacticAction.AttackWeakest:
            case TacticAction.AttackStrongest:
            {
                var target = candidate.Target;
                return target is null ? 0.0 : AttackUtility(self, target, enemies);
            }

            default:
                return 0.0;
        }
    }

    private static double AttackUtility(Combatant self, Combatant target, IReadOnlyList<Combatant> enemies)
    {
        double score = 0.5;

        int expected = DamageModel.ExpectedDamage(self, target);
        if (expected >= target.Hp) score += 0.6;
        else score += 0.25 * (1.0 - target.HpRatio);

        int maxAttack = enemies.Max(e => e.EffectiveOffense);
        if (maxAttack > 0) score += 0.15 * ((double)target.EffectiveOffense / maxAttack);

        return score;
    }

    private static double Sq(double x) => x * x;

    /// <summary>
    /// "가장 가까운 적" — <b>앞줄부터, 같은 줄이면 쓰러지기 직전인 쪽부터.</b>
    /// <para>
    /// 원래는 그냥 배열의 첫 원소를 집었습니다. 전열/후열을 도입해놓고 대상 선택은
    /// 리스트 순서였던 셈이라, 기본 규칙이 사실상 무의미했습니다.
    /// 그 결과 판단력 100이 그 무의미한 규칙을 완벽히 따르느라 70보다 약했습니다. (docs/06 #12)
    /// </para>
    /// <para>
    /// 기본 규칙은 <b>편성을 안 해도 그럭저럭 말이 되는 수</b>여야 합니다.
    /// 그래야 규칙 편성이 "기본값 고치기"가 아니라 "더 잘하기"가 됩니다.
    /// </para>
    /// </summary>
    private static Combatant PickNearest(IReadOnlyList<Combatant> targets) =>
        targets.OrderBy(e => e.Row == Row.Front ? 0 : 1)
               .ThenBy(e => e.Hp)
               .ThenBy(e => e.Id, StringComparer.Ordinal)
               .First();

    // 동점일 때 순서가 흔들리면 결정론이 깨지므로, Id로 타이브레이크합니다.
    private static Combatant PickWeakest(IReadOnlyList<Combatant> targets) =>
        targets.OrderBy(e => e.Hp).ThenBy(e => e.Id, StringComparer.Ordinal).First();

    private static Combatant PickStrongest(IReadOnlyList<Combatant> targets) =>
        targets.OrderByDescending(e => e.EffectiveOffense).ThenBy(e => e.Id, StringComparer.Ordinal).First();
}
