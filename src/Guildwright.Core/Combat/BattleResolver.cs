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
/// <param name="maxRounds">라운드 상한. 넘으면 무승부.</param>
/// <param name="recordLog">전투 기록을 남길지. 배치 시뮬레이션에서는 꺼서 돌립니다.</param>
/// <param name="explainAttacks">
/// 공격 한 번마다 계산 과정을 기록에 남길지. <c>recordLog</c>가 켜져 있어야 의미가 있습니다.
/// <b>난수 소비와 전투 결과는 이 값과 무관하게 완전히 동일합니다.</b>
/// </param>
public sealed class BattleResolver(int maxRounds = 50, bool recordLog = false, bool explainAttacks = false)
{
    /// <summary>
    /// 전투를 진행합니다.
    /// </summary>
    /// <param name="state">전투 상태.</param>
    /// <param name="rng">난수원.</param>
    /// <param name="commander">
    /// 플레이어 개입 통로. null이면 완전 자동으로 돌아갑니다(배치 시뮬레이션).
    /// </param>
    /// <param name="onLine">
    /// 기록이 한 줄 생길 때마다 호출되는 <b>출력 전용</b> 콜백. 관전·수동 전투에서
    /// 진행 상황을 실시간으로 보여주는 용도입니다. 전투 결과에 영향을 주면 안 됩니다.
    /// <c>recordLog: false</c>이면 호출되지 않습니다.
    /// </param>
    public BattleResult Resolve(
        BattleState state,
        IRandomSource rng,
        IBattleCommander? commander = null,
        Action<string>? onLine = null)
    {
        var log = recordLog ? new BattleLog(onLine) : null;

        try
        {
            return Fight(state, rng, commander, log);
        }
        finally
        {
            // 전투가 끝났습니다 — 상황이 만든 것은 풀리고 몸에 난 것은 남습니다.
            // HP·마나·회복약처럼 상처도 파견 내내 이어집니다.
            foreach (var combatant in state.All) combatant.EndBattle();
        }
    }

    private BattleResult Fight(
        BattleState state,
        IRandomSource rng,
        IBattleCommander? commander,
        BattleLog? log)
    {
        for (int round = 1; round <= maxRounds; round++)
        {
            log?.Add($"--- {round}라운드 ---");

            foreach (var actor in state.TurnOrder(rng))
            {
                if (!actor.IsAlive) continue;

                actor.ClearDefending();

                // 마비·빙결·석화 — 지시를 듣든 안 듣든 몸이 안 움직입니다.
                double blocked = actor.IncapacitateChance;
                if (blocked > 0.0 && rng.Chance(blocked))
                {
                    log?.Add($"{actor.Name}: 움직이지 못함");
                    continue;
                }

                var choice = TacticalBrain.Decide(actor, state, rng);

                // 플레이어가 끼어들 기회. 횟수 제한은 없습니다 —
                // 아끼게 만들면 결국 안 쓰게 되고, 그게 "개입으로 할 게 없다"의 원인이었습니다.
                // 유일한 제약은 공포·혼란에 걸린 아군에게는 지시가 통하지 않는 것입니다.
                if (commander is not null && commander.Team == actor.Team)
                {
                    var order = commander.Intervene(actor, choice, state);
                    if (order is { } given)
                    {
                        if (actor.AcceptsOrders)
                        {
                            choice = new ChosenAction(given.Action, given.Target);
                            log?.Add($"[지휘] {actor.Name}에게 지시");
                        }
                        else
                        {
                            log?.Add($"[지휘] {actor.Name}에게 지시가 통하지 않음 — 말을 듣지 않는다");
                        }
                    }
                }

                choice = Permitted(actor, choice, log);

                actor.Contribution.RecordAction();
                actor.GrowOnAction();
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

    /// <summary>
    /// 속박·침묵에 걸린 행동을 대체합니다.
    /// <para>지시 불통과 다릅니다 — 이쪽은 <b>그 행동만</b> 확정으로 막힙니다.</para>
    /// </summary>
    private static ChosenAction Permitted(Combatant actor, ChosenAction choice, BattleLog? log)
    {
        bool movement = choice.Action is TacticAction.MoveBack or TacticAction.MoveFront;
        bool manaSkill = choice.Action is TacticAction.HealAlly
            or TacticAction.BuffAlly or TacticAction.DebuffEnemy;

        if (movement && actor.IsRestricted(ActionRestriction.Movement))
        {
            log?.Add($"{actor.Name}: 발이 묶여 움직일 수 없음 — 방어");
            return new ChosenAction(TacticAction.Defend, null);
        }

        if (manaSkill && actor.IsRestricted(ActionRestriction.ManaSkills))
        {
            log?.Add($"{actor.Name}: 침묵 상태 — 방어");
            return new ChosenAction(TacticAction.Defend, null);
        }

        return choice;
    }

    /// <summary>라운드 종료 처리 — 지속 피해, 재생, 임계 전이, 효과 만료.</summary>
    private static void EndOfRound(BattleState state, BattleLog? log)
    {
        foreach (var combatant in state.All)
        {
            if (!combatant.IsAlive) continue;

            foreach (var effect in combatant.Effects.ToList())
            {
                switch (effect.Mechanism)
                {
                    case EffectMechanism.DamageOverTime:
                    {
                        int damage = DamageModel.OverTimeDamage(combatant, effect);
                        combatant.TakeDamage(damage);
                        log?.Add($"{combatant.Name}: {effect} 피해 {damage}");
                        break;
                    }

                    case EffectMechanism.Recovery when !effect.Profile.BlocksRecovery:
                    {
                        int healed = Math.Max(1, (int)Math.Round(combatant.MaxHp * effect.Magnitude));
                        combatant.Heal(healed);
                        log?.Add($"{combatant.Name}: 재생 +{healed}");
                        break;
                    }
                }
            }

            if (!combatant.IsAlive) continue;

            // 동상이 쌓이면 빙결로 넘어갑니다. 파국이자 리셋입니다.
            if (combatant.ResolveTransition() is { } transitioned)
            {
                log?.Add($"{combatant.Name}: {StatusEffects.ToKorean(transitioned)} 상태가 됨");
            }

            combatant.TickEffects();
        }
    }

    private void Execute(
        Combatant actor,
        ChosenAction choice,
        BattleState state,
        IRandomSource rng,
        BattleLog? log)
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
                target.ApplyEffect(StatusEffects.Create(
                    EffectName.PowerUp, DamageModel.BuffDuration, actor.Id));
                actor.Contribution.RecordSupport();
                log?.Add($"{actor.Name} → {target.Name}: 공격 강화");
                return;
            }

            case TacticAction.DebuffEnemy:
            {
                var target = choice.Target;
                if (target is null || !target.IsAlive || actor.Mana < DamageModel.ManaPerSpell) return;

                actor.SpendMana(DamageModel.ManaPerSpell);
                target.ApplyEffect(StatusEffects.Create(
                    EffectName.PowerDown, DamageModel.BuffDuration, actor.Id));
                actor.Contribution.RecordSupport();
                log?.Add($"{actor.Name} → {target.Name}: 공격 약화");
                return;
            }

            case TacticAction.Taunt:
            {
                var enemies = state.LivingOpponentsOf(actor.Team);
                foreach (var enemy in enemies)
                {
                    enemy.ApplyEffect(StatusEffects.Create(
                        EffectName.Taunt, DamageModel.TauntDuration, actor.Id));
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
                    var hit = DamageModel.ResolveAttack(actor, target, rng, area: true, explain: explainAttacks);

                    // ⚠️ ApplyHit은 피해를 실제로 적용합니다. log?.Add(ApplyHit(...))로 쓰면
                    //    log가 null일 때 ?. 가 전체 식을 단락시켜 ApplyHit이 호출되지 않습니다.
                    //    실제로 그 버그로 배치 시뮬레이션이 전부 무승부가 났습니다. (docs/06 #13)
                    string areaLine = ApplyHit(actor, target, hit, areaMagic, prefix: "광역 ");
                    log?.Add(areaLine);
                    Explain(hit, log);
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

                var result = DamageModel.ResolveAttack(actor, target, rng, explain: explainAttacks);

                // ⚠️ 부작용이 있는 호출을 log?.Add(...)의 인자로 넣지 마세요. 위 주석 참조.
                string line = ApplyHit(actor, target, result, actor.Capability.UsesMagic);
                log?.Add(line);
                Explain(result, log);
                return;
            }

            default:
                return;
        }
    }

    /// <summary>계산 과정을 한 단 들여써서 기록에 붙입니다.</summary>
    private static void Explain(AttackResult result, BattleLog? log)
    {
        if (log is null || result.Detail is null) return;

        foreach (var line in result.Detail.Split('\n'))
        {
            log.Add("      " + line);
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

    private static IReadOnlyList<string> ReadOnly(BattleLog? log) =>
        log is null ? Array.Empty<string>() : log.Lines;
}
