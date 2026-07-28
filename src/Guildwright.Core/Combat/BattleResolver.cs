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
    public BattleResult Resolve(BattleState state, IRandomSource rng)
    {
        var log = recordLog ? new List<string>() : null;

        for (int round = 1; round <= maxRounds; round++)
        {
            log?.Add($"--- {round}라운드 ---");

            foreach (var actor in state.TurnOrder(rng))
            {
                if (!actor.IsAlive) continue;

                actor.ClearDefending();

                var choice = TacticalBrain.Decide(actor, state, rng);
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
                    int damage = DamageModel.RollDamage(actor, target, rng, area: true);
                    target.TakeDamage(damage, areaMagic);
                    actor.Contribution.RecordDamageDealt(damage, areaMagic);
                    if (!target.IsAlive) actor.Contribution.RecordKill();
                    log?.Add($"{actor.Name} ⇒ {target.Name}: 광역 {damage} 피해{(target.IsAlive ? "" : ", 쓰러뜨림")}");
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

                bool magic = actor.Capability.UsesMagic;
                int damage = DamageModel.RollDamage(actor, target, rng);
                target.TakeDamage(damage, magic);
                actor.Contribution.RecordDamageDealt(damage, magic);
                if (!target.IsAlive) actor.Contribution.RecordKill();
                log?.Add(target.IsAlive
                    ? $"{actor.Name} → {target.Name}: {damage} 피해 (남은 HP {target.Hp})"
                    : $"{actor.Name} → {target.Name}: {damage} 피해, 쓰러뜨림");
                return;
            }

            default:
                return;
        }
    }

    private static IReadOnlyList<string> ReadOnly(List<string>? log) =>
        log is null ? Array.Empty<string>() : log;
}
