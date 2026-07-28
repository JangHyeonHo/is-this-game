using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

public enum BattleOutcome
{
    PlayerVictory,
    EnemyVictory,
    /// <summary>라운드 제한에 걸림.</summary>
    Draw
}

/// <param name="Outcome">결과.</param>
/// <param name="Rounds">소요 라운드 수.</param>
/// <param name="Log">전투 기록. 배치 시뮬레이션에서는 비워서 실행합니다.</param>
public sealed record BattleResult(
    BattleOutcome Outcome,
    int Rounds,
    IReadOnlyList<string> Log);

/// <summary>
/// 전투를 끝까지 진행시킵니다.
/// <para>
/// 순수 함수에 가깝게 유지하세요 — (전투원 구성 + 시드)만으로 결과가 완전히 결정되어야
/// 밸런싱을 배치 시뮬레이션으로 할 수 있습니다.
/// </para>
/// </summary>
public sealed class BattleResolver(int maxRounds = 50, bool recordLog = false)
{
    /// <summary>
    /// 전투를 진행합니다.
    /// </summary>
    /// <param name="state">전투 상태.</param>
    /// <param name="rng">난수원.</param>
    /// <param name="commander">
    /// 플레이어 개입 통로. null이면 완전 자동으로 돌아갑니다(배치 시뮬레이션).
    /// </param>
    public BattleResult Resolve(BattleState state, IRandomSource rng, IBattleCommander? commander = null)
    {
        var log = recordLog ? new List<string>() : null;
        int commandPoints = commander is null ? 0 : CommandRules.BasePoints;

        for (int round = 1; round <= maxRounds; round++)
        {
            log?.Add($"--- {round}라운드 ---");

            foreach (var actor in state.TurnOrder(rng))
            {
                if (!actor.IsAlive) continue;

                actor.ClearDefending();

                var choice = TacticalBrain.Decide(actor, state, rng);

                // 플레이어가 끼어들 기회. 지휘 포인트가 남아 있어야 합니다.
                if (commander is not null && commander.Team == actor.Team && commandPoints > 0)
                {
                    var order = commander.Intervene(actor, choice, state, commandPoints);
                    if (order is { } given)
                    {
                        int cost = CommandRules.CostOf(given.Action);
                        if (cost <= commandPoints)
                        {
                            commandPoints -= cost;
                            choice = new ChosenAction(given.Action, given.Target);
                            log?.Add($"[지휘] {actor.Name}에게 지시 (남은 지휘 {commandPoints})");
                        }
                    }
                }

                actor.Contribution.RecordAction();
                Execute(actor, choice, state, rng, log);

                if (state.IsTeamWipedOut(Team.Enemy))
                    return new BattleResult(BattleOutcome.PlayerVictory, round, ReadOnly(log));

                if (state.IsTeamWipedOut(Team.Player))
                    return new BattleResult(BattleOutcome.EnemyVictory, round, ReadOnly(log));
            }

            EndOfRound(state, log);

            if (state.IsTeamWipedOut(Team.Enemy))
                return new BattleResult(BattleOutcome.PlayerVictory, round, ReadOnly(log));

            if (state.IsTeamWipedOut(Team.Player))
                return new BattleResult(BattleOutcome.EnemyVictory, round, ReadOnly(log));
        }

        return new BattleResult(BattleOutcome.Draw, maxRounds, ReadOnly(log));
    }

    /// <summary>라운드 종료 처리 — 지속 피해와 효과 만료.</summary>
    private static void EndOfRound(BattleState state, List<string>? log)
    {
        foreach (var combatant in state.All)
        {
            if (!combatant.IsAlive) continue;

            if (combatant.HasEffect(StatusEffectKind.Poisoned))
            {
                int damage = DamageModel.PoisonDamage(combatant);
                combatant.TakeDamage(damage);
                log?.Add($"{combatant.Name}: 중독 피해 {damage}");
            }

            combatant.TickEffects();
        }
    }

    private static void Execute(
        Combatant actor,
        ChosenAction choice,
        BattleState state,
        IRandomSource rng,
        List<string>? log)
    {
        switch (choice.Action)
        {
            case TacticAction.MoveBack:
                actor.MoveTo(Row.Back);
                actor.Contribution.RecordReposition();
                log?.Add($"{actor.Name}: 후열로 물러남 (HP {actor.Hp}/{actor.MaxHp})");
                return;

            case TacticAction.MoveFront:
                actor.MoveTo(Row.Front);
                actor.Contribution.RecordReposition();
                log?.Add($"{actor.Name}: 전열로 나섬");
                return;

            case TacticAction.UsePotion:
            {
                if (actor.Potions <= 0)
                {
                    actor.BeginDefending();
                    log?.Add($"{actor.Name}: 회복약이 없어 방어");
                    return;
                }

                int healed = DamageModel.PotionHealAmount(actor);
                actor.ConsumePotion();
                actor.Heal(healed);
                log?.Add($"{actor.Name}: 회복약 사용 (+{healed}, 남은 {actor.Potions}개)");
                return;
            }

            case TacticAction.HealAlly:
            {
                var target = choice.Target;
                if (target is null || !target.IsAlive || actor.Mana < DamageModel.ManaPerSpell)
                {
                    actor.BeginDefending();
                    return;
                }

                int healed = DamageModel.MagicHealAmount(actor);
                actor.SpendMana(DamageModel.ManaPerSpell);
                target.Heal(healed);
                actor.Contribution.RecordHealing(healed);
                actor.Contribution.RecordSupport();
                log?.Add($"{actor.Name} → {target.Name}: 회복 +{healed}");
                return;
            }

            case TacticAction.BuffAlly:
            {
                var target = choice.Target;
                if (target is null || !target.IsAlive || actor.Mana < DamageModel.ManaPerSpell) return;

                actor.SpendMana(DamageModel.ManaPerSpell);
                target.ApplyEffect(new StatusEffect(
                    StatusEffectKind.Empowered, DamageModel.BuffDuration, DamageModel.BuffMagnitude, actor.Id));
                actor.Contribution.RecordSupport();
                log?.Add($"{actor.Name} → {target.Name}: 공격 강화");
                return;
            }

            case TacticAction.DebuffEnemy:
            {
                var target = choice.Target;
                if (target is null || !target.IsAlive || actor.Mana < DamageModel.ManaPerSpell) return;

                actor.SpendMana(DamageModel.ManaPerSpell);
                target.ApplyEffect(new StatusEffect(
                    StatusEffectKind.Weakened, DamageModel.BuffDuration, DamageModel.BuffMagnitude, actor.Id));
                actor.Contribution.RecordSupport();
                log?.Add($"{actor.Name} → {target.Name}: 공격 약화");
                return;
            }

            case TacticAction.Taunt:
            {
                var enemies = state.LivingOpponentsOf(actor.Team);
                foreach (var enemy in enemies)
                {
                    enemy.ApplyEffect(new StatusEffect(
                        StatusEffectKind.Taunted, DamageModel.TauntDuration, 0.0, actor.Id));
                }
                actor.BeginDefending();
                actor.Contribution.RecordSupport();
                log?.Add($"{actor.Name}: 도발 — 적의 공격을 끌어들임");
                return;
            }

            case TacticAction.Defend:
                actor.BeginDefending();
                log?.Add($"{actor.Name}: 방어 태세");
                return;

            case TacticAction.AttackAll:
            {
                var targets = state.ReachableTargets(actor);
                if (targets.Count == 0) return;

                bool areaMagic = actor.Capability.UsesMagic;
                foreach (var target in targets.ToList())
                {
                    if (!target.IsAlive) continue;
                    var hit = DamageModel.ResolveAttack(actor, target, rng, area: true);

                    // ⚠️ ApplyHit은 피해를 실제로 적용합니다. log?.Add(ApplyHit(...))로 쓰면
                    //    log가 null일 때 ?. 가 전체 식을 단락시켜 ApplyHit이 호출되지 않습니다.
                    //    실제로 그 버그로 배치 시뮬레이션이 전부 무승부가 났습니다. (docs/06 #13)
                    string areaLine = ApplyHit(actor, target, hit, areaMagic, prefix: "광역 ");
                    log?.Add(areaLine);
                }
                return;
            }

            case TacticAction.AttackNearest:
            case TacticAction.AttackWeakest:
            case TacticAction.AttackStrongest:
            case TacticAction.AttackBackRow:
            {
                var target = choice.Target;
                if (target is null || !target.IsAlive)
                {
                    log?.Add($"{actor.Name}: 대상 없음");
                    return;
                }

                var result = DamageModel.ResolveAttack(actor, target, rng);

                // ⚠️ 부작용이 있는 호출을 log?.Add(...)의 인자로 넣지 마세요. 위 주석 참조.
                string line = ApplyHit(actor, target, result, actor.Capability.UsesMagic);
                log?.Add(line);
                return;
            }

            default:
                return;
        }
    }

    /// <summary>
    /// 공격 결과를 적용하고 기록에 남깁니다.
    /// <para>회피와 치명타가 성장 데이터로도 쌓입니다 — 피한 만큼 몸이 반응하게 됩니다.</para>
    /// </summary>
    private static string ApplyHit(
        Combatant actor,
        Combatant target,
        AttackResult hit,
        bool magic,
        string prefix = "")
    {
        if (hit.Evaded)
        {
            target.Contribution.RecordEvasion();
            return $"{actor.Name} → {target.Name}: {prefix}빗나감";
        }

        target.TakeDamage(hit.Damage, magic);
        actor.Contribution.RecordDamageDealt(hit.Damage, magic);
        if (hit.Critical) actor.Contribution.RecordCritical();
        if (!target.IsAlive) actor.Contribution.RecordKill();

        string crit = hit.Critical ? "치명타! " : "";
        string tail = target.IsAlive ? $"(남은 HP {target.Hp})" : "쓰러뜨림";
        return $"{actor.Name} → {target.Name}: {crit}{prefix}{hit.Damage} 피해, {tail}";
    }

    private static IReadOnlyList<string> ReadOnly(List<string>? log) =>
        log is null ? Array.Empty<string>() : log;
}
