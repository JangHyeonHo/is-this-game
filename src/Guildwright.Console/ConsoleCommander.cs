using Guildwright.Core.Combat;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;

namespace Guildwright.Cli;

/// <summary>
/// 전투 중 플레이어의 개입을 콘솔로 받습니다.
/// <para>
/// <b>개입 횟수에는 제한이 없습니다.</b> 대신 매 턴 물어보지는 않습니다 —
/// 12라운드 × 4명을 다 물어보면 "자동 전투"라는 말이 무색해지므로,
/// <b>정말 중요해 보이는 순간에만</b> 멈춥니다.
/// </para>
/// <para>
/// 물어보는 횟수를 줄이는 것과 개입을 제한하는 것은 다릅니다 —
/// 전자는 편의고 후자는 규칙이었습니다. 후자를 없앴습니다.
/// </para>
/// </summary>
public sealed class ConsoleCommander(bool watchEveryTurn = false) : IBattleCommander
{
    public Team Team => Team.Player;

    public CommandOrder? Intervene(Combatant actor, ChosenAction aiChoice, BattleState state)
    {
        if (!watchEveryTurn && !IsWorthAsking(actor, aiChoice, state)) return null;

        Ui.Line();
        Display.Formation(state);
        Ui.Line();
        Ui.Line($"   ▶ {actor.Name}의 차례 — AI 판단: {Display.ActionName(aiChoice.Action)}" +
                (aiChoice.Target is null ? "" : $" → {aiChoice.Target.Name}"));
        if (!actor.AcceptsOrders)
        {
            Ui.Note("이 상태에서는 지시가 통하지 않습니다 — 말을 듣지 않습니다.");
            return null;
        }

        var options = BuildOptions(actor, state);
        int picked = Ui.Choose("   지시", options.Select(o => o.Label).ToList());

        return options[picked].Order;
    }

    /// <summary>
    /// 물어볼 가치가 있는 순간인가.
    /// <para>모든 턴을 물어보면 자동 전투의 의미가 사라지고, 아예 안 물어보면 개입이 무의미해집니다.</para>
    /// </summary>
    private static bool IsWorthAsking(Combatant actor, ChosenAction aiChoice, BattleState state)
    {
        // 내가 위험하다
        if (actor.HpRatio < 0.35) return true;

        // 아군이 죽기 직전이다
        if (state.LivingMembersOf(actor.Team).Any(a => a.HpRatio < 0.25)) return true;

        // 적을 마무리할 수 있다
        var reachable = state.ReachableTargets(actor);
        if (reachable.Any(e => DamageModel.ExpectedDamage(actor, e) >= e.Hp)) return true;

        // 전열이 비었다 — 대형이 무너지는 순간
        if (state.IsFrontRowEmpty(actor.Team)) return true;

        return false;
    }

    private static List<(string Label, CommandOrder? Order)> BuildOptions(
        Combatant actor,
        BattleState state)
    {
        var options = new List<(string, CommandOrder?)>
        {
            ("그대로 진행", null)
        };

        var reachable = state.ReachableTargets(actor);
        var allies = state.LivingMembersOf(actor.Team);

        {
            foreach (var target in reachable.OrderBy(e => e.Hp).Take(3))
            {
                string finisher = DamageModel.ExpectedDamage(actor, target) >= target.Hp ? " ★마무리 가능" : "";
                options.Add(($"{target.Name} 집중 공격 (HP {target.Hp}){finisher}",
                    new CommandOrder(TacticAction.AttackNearest, target)));
            }

            if (actor.Potions > 0 && actor.Hp < actor.MaxHp)
            {
                options.Add(($"회복약 사용 (남은 {actor.Potions}개)", new CommandOrder(TacticAction.UsePotion, null)));
            }

            if (actor.CanDo(TacticAction.HealAlly) && actor.Mana >= SkillBook.Of(SkillId.Cure).ManaCost)
            {
                var wounded = allies.OrderBy(a => a.HpRatio).First();
                options.Add(($"{wounded.Name} 회복 (HP {wounded.Hp}/{wounded.MaxHp})",
                    new CommandOrder(TacticAction.HealAlly, wounded)));
            }

            if (actor.CanDo(TacticAction.Taunt) && actor.Row == Row.Front)
            {
                options.Add(("도발 — 적의 공격을 끌어들임", new CommandOrder(TacticAction.Taunt, null)));
            }

            options.Add(("방어 태세", new CommandOrder(TacticAction.Defend, null)));
        }

        if (actor.IsRestricted(ActionRestriction.Movement))
        {
            Ui.Note("발이 묶여 위치를 바꿀 수 없습니다.");
        }
        else if (actor.Row == Row.Front)
        {
            int frontCount = state.LivingIn(actor.Team, Row.Front).Count;
            string warning = frontCount <= 1 ? " ⚠ 전열이 비어 후열이 노출됩니다" : "";
            options.Add(($"후열로 물러남{warning}", new CommandOrder(TacticAction.MoveBack, null)));
        }
        else
        {
            options.Add(("전열로 나섬", new CommandOrder(TacticAction.MoveFront, null)));
        }

        return options;
    }
}
