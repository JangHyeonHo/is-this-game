using Guildwright.Core.Rng;

namespace Guildwright.Core.Combat;

public enum BattleOutcome
{
    PlayerVictory,
    EnemyVictory,
    /// <summary>라운드 제한에 걸림. 양쪽 다 결판을 못 낸 경우입니다.</summary>
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
/// 순수 함수에 가깝게 유지하세요 — 파일 I/O, 시간, 전역 상태 없이
/// (전투원 구성 + 시드)만으로 결과가 완전히 결정되어야 합니다.
/// 이 성질이 있어야 밸런싱을 배치 시뮬레이션으로 할 수 있습니다.
/// </para>
/// </summary>
public sealed class BattleResolver
{
    private readonly int _maxRounds;
    private readonly bool _recordLog;

    public BattleResolver(int maxRounds = 50, bool recordLog = false)
    {
        _maxRounds = maxRounds;
        _recordLog = recordLog;
    }

    public BattleResult Resolve(BattleState state, IRandomSource rng)
    {
        var log = _recordLog ? new List<string>() : null;

        for (int round = 1; round <= _maxRounds; round++)
        {
            foreach (var actor in state.TurnOrder(rng))
            {
                // 순서를 계산한 뒤 죽었을 수 있습니다.
                if (!actor.IsAlive) continue;

                actor.ClearDefending();

                var choice = TacticalBrain.Decide(actor, state, rng);
                Execute(actor, choice, rng, log);

                if (state.IsTeamWipedOut(Team.Enemy))
                {
                    return new BattleResult(BattleOutcome.PlayerVictory, round, ReadOnly(log));
                }

                if (state.IsTeamWipedOut(Team.Player))
                {
                    return new BattleResult(BattleOutcome.EnemyVictory, round, ReadOnly(log));
                }
            }
        }

        return new BattleResult(BattleOutcome.Draw, _maxRounds, ReadOnly(log));
    }

    private static void Execute(
        Combatant actor,
        ChosenAction choice,
        IRandomSource rng,
        List<string>? log)
    {
        switch (choice.Action)
        {
            case TacticAction.UsePotion:
            {
                // 규칙과 노이즈가 겹쳐 회복약 없이 이 행동이 선택될 수 있으므로 방어적으로 확인합니다.
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

            case TacticAction.Defend:
                actor.BeginDefending();
                log?.Add($"{actor.Name}: 방어 태세");
                return;

            case TacticAction.AttackNearest:
            case TacticAction.AttackWeakest:
            case TacticAction.AttackStrongest:
            {
                var target = choice.Target;
                if (target is null || !target.IsAlive)
                {
                    log?.Add($"{actor.Name}: 대상 없음");
                    return;
                }

                int damage = DamageModel.RollDamage(actor, target, rng);
                target.TakeDamage(damage);
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
