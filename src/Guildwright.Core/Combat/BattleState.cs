using Guildwright.Core.Rng;

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
            throw new ArgumentException("전투원 Id가 중복되었습니다. Id는 타이브레이크에 쓰이므로 고유해야 합니다.", nameof(combatants));
        }
    }

    public IReadOnlyList<Combatant> All => _combatants;

    public IReadOnlyList<Combatant> LivingMembersOf(Team team) =>
        _combatants.Where(c => c.Team == team && c.IsAlive).ToList();

    public IReadOnlyList<Combatant> LivingOpponentsOf(Team team) =>
        _combatants.Where(c => c.Team != team && c.IsAlive).ToList();

    public bool IsTeamWipedOut(Team team) => !_combatants.Any(c => c.Team == team && c.IsAlive);

    /// <summary>
    /// 이번 라운드의 행동 순서. 민첩 내림차순으로 정하되, <b>동점은 무작위로</b> 가릅니다.
    /// <para>
    /// 동점을 Id 사전순으로 고정하면 안 됩니다. 팀 접두사가 순서를 결정해버려
    /// 한쪽이 항상 선공을 잡는 구조적 편향이 생깁니다.
    /// (실제로 이 버그가 있었고, "능력치가 같으면 승률 5할" 테스트가 36%로 잡아냈습니다.)
    /// </para>
    /// <para>
    /// 민첩 자체는 여전히 절대적으로 우선하므로, 민첩 스탯의 의미는 그대로 보존됩니다.
    /// 나중에 밸런스 조정이 필요하면 여기에 이니셔티브 변동폭을 도입할 수 있습니다.
    /// </para>
    /// </summary>
    public IReadOnlyList<Combatant> TurnOrder(IRandomSource rng)
    {
        // _combatants의 순서가 고정되어 있으므로 난수 배정도 결정론적입니다.
        return _combatants
            .Where(c => c.IsAlive)
            .Select(c => (Combatant: c, TieBreak: rng.NextDouble()))
            .OrderByDescending(x => x.Combatant.Agility)
            .ThenByDescending(x => x.TieBreak)
            .Select(x => x.Combatant)
            .ToList();
    }
}
