using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

/// <summary>
/// 전투 한 판의 상태.
/// <para>
/// 전투원 목록의 순서는 <b>절대 흔들리면 안 됩니다.</b> 순서가 바뀌면 같은 시드로도
/// 다른 결과가 나와 배치 시뮬레이션 기반 밸런싱이 무너집니다.
/// </para>
/// </summary>
public sealed class BattleState
{
    private readonly List<Combatant> _combatants;

    public BattleState(IEnumerable<Combatant> combatants)
    {
        _combatants = combatants.ToList();

        if (_combatants.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != _combatants.Count)
        {
            throw new ArgumentException("전투원 Id가 중복되었습니다. Id는 대상 선택에 쓰이므로 고유해야 합니다.", nameof(combatants));
        }
    }

    public IReadOnlyList<Combatant> All => _combatants;

    public IReadOnlyList<Combatant> LivingMembersOf(Team team) =>
        _combatants.Where(c => c.Team == team && c.IsAlive).ToList();

    public IReadOnlyList<Combatant> LivingOpponentsOf(Team team) =>
        _combatants.Where(c => c.Team != team && c.IsAlive).ToList();

    public IReadOnlyList<Combatant> LivingIn(Team team, Row row) =>
        _combatants.Where(c => c.Team == team && c.IsAlive && c.Row == row).ToList();

    public bool IsTeamWipedOut(Team team) => !_combatants.Any(c => c.Team == team && c.IsAlive);

    /// <summary>해당 팀의 전열이 비었는가. 비면 후열이 그대로 노출됩니다.</summary>
    public bool IsFrontRowEmpty(Team team) => LivingIn(team, Row.Front).Count == 0;

    /// <summary>
    /// <paramref name="attacker"/>가 지금 실제로 때릴 수 있는 적들.
    /// <para>
    /// <b>여기가 포지션 시스템의 핵심 규칙입니다.</b>
    /// 근접 무기는 적 전열까지만 닿습니다. 다만 <b>적 전열이 비면 후열이 그대로 노출됩니다</b> —
    /// 그래서 전열을 유지하는 것이 방어 행위가 되고, 후퇴가 공짜가 아니게 됩니다.
    /// </para>
    /// </summary>
    public IReadOnlyList<Combatant> ReachableTargets(Combatant attacker)
    {
        var enemies = LivingOpponentsOf(attacker.Team);
        if (enemies.Count == 0) return enemies;

        // 도발당했으면 도발한 대상만 노립니다.
        if (attacker.TauntedBy is { } tauntSource)
        {
            var taunter = enemies.FirstOrDefault(e => e.Id == tauntSource);
            if (taunter is not null) return [taunter];
        }

        if (attacker.Capability.CanStrikeBackRow) return enemies;

        var front = enemies.Where(e => e.Row == Row.Front).ToList();
        return front.Count > 0 ? front : enemies;
    }

    /// <summary>
    /// 이번 라운드의 행동 순서. 실효 속도 내림차순, 동점은 무작위로 가릅니다.
    /// <para>
    /// 동점을 Id 사전순으로 고정하면 팀 접두사가 순서를 결정해 한쪽이 항상 선공을 잡습니다.
    /// (실제로 그 버그가 있었고, "능력치가 같으면 승률 5할" 테스트가 36%로 잡아냈습니다.)
    /// </para>
    /// </summary>
    public IReadOnlyList<Combatant> TurnOrder(IRandomSource rng)
    {
        return _combatants
            .Where(c => c.IsAlive)
            .Select(c => (Combatant: c, TieBreak: rng.NextDouble()))
            .OrderByDescending(x => x.Combatant.EffectiveSpeed)
            .ThenByDescending(x => x.TieBreak)
            .Select(x => x.Combatant)
            .ToList();
    }
}
