using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Parties;

/// <summary>
/// 정규 파티 — <b>등록된</b> 조합.
/// <para>
/// 가상 파티에는 이 객체가 없습니다. 가상 파티는 <see cref="PartyComposition"/>과
/// <see cref="PartyLedger"/>의 누적으로만 존재합니다. <b>등록되어야 이름과 등급을 갖습니다.</b>
/// </para>
/// <para>
/// 등급은 <b>개인 등급에서 파생되지 않습니다.</b> 최하에서 시작해 파티로서 평가를 쌓아야
/// 오릅니다 — 그래서 "파티를 짰다고 처음부터 A"가 되지 않고, 순환 참조도 없습니다.
/// </para>
/// 근거: docs/08-design-revision.md §6.0, §6.2
/// </summary>
public sealed class Party
{
    private readonly List<Adventurer> _members;

    internal Party(string id, string name, IEnumerable<Adventurer> members)
    {
        Id = id;
        Name = name;

        // 서수 정렬로 고정합니다 — 순회 순서가 결과에 섞이면 배치 시뮬레이션이 흔들립니다.
        _members = [.. members.OrderBy(m => m.Id, StringComparer.Ordinal)];
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>등급. <b>등록 직후에는 최하</b>입니다.</summary>
    public Rank Rank { get; private set; } = Ranks.Lowest;

    /// <summary>지금 등급에서 쌓은 평가. 다음 등급으로 오르면 0으로 돌아갑니다.</summary>
    public int Evaluation { get; private set; }

    /// <summary>해체되었는가. 남은 멤버가 1명이 되면 자동으로 그렇게 됩니다.</summary>
    public bool Disbanded { get; private set; }

    public IReadOnlyList<Adventurer> Members => _members;

    public PartyComposition Composition => PartyComposition.Of(_members);

    /// <summary>이 파티에 들어오려면 최소 몇 등급이어야 하는가.</summary>
    public Rank JoinFloor => Rank.Below(PartyRules.JoinRankGap);

    /// <summary>다음 등급까지 남은 평가.</summary>
    public int EvaluationToNextRank =>
        Rank == Ranks.Highest ? 0 : Math.Max(0, PartyRules.EvaluationNeeded(Rank) - Evaluation);

    /// <summary>
    /// 파티로서 평가를 쌓습니다. 문턱을 넘으면 등급이 오릅니다.
    /// <para>한 번에 여러 단이 오를 수도 있습니다 — 큰 의뢰 하나가 그럴 수 있습니다.</para>
    /// </summary>
    /// <returns>이번에 오른 단 수.</returns>
    internal int RecordEvaluation(int points)
    {
        if (Disbanded || points <= 0) return 0;

        Evaluation += points;

        int risen = 0;
        while (Rank != Ranks.Highest && Evaluation >= PartyRules.EvaluationNeeded(Rank))
        {
            Evaluation -= PartyRules.EvaluationNeeded(Rank);
            Rank = Rank.Above(1);
            risen++;
        }

        // 최고 등급에서는 더 쌓아도 갈 곳이 없습니다.
        if (Rank == Ranks.Highest) Evaluation = 0;

        return risen;
    }

    internal void Add(Adventurer member)
    {
        _members.Add(member);
        _members.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    /// <summary>
    /// 멤버를 뺍니다. <b>남은 멤버가 1명이 되면 해체됩니다.</b>
    /// <para>
    /// 죽음의 대가가 여기 남습니다 — 빈자리는 다시 6개월을 들여야 메워집니다.
    /// 다만 1명이 될 때까지는 해체되지 않으므로 파국은 아닙니다.
    /// </para>
    /// </summary>
    internal bool Remove(string adventurerId)
    {
        int removed = _members.RemoveAll(m => string.Equals(m.Id, adventurerId, StringComparison.Ordinal));
        if (removed == 0) return false;

        if (_members.Count < PartyRules.MinimumMembers) Disbanded = true;
        return true;
    }

    public override string ToString() =>
        $"{Name} [{Rank.ToKorean()}] {_members.Count}명" +
        (Disbanded ? " (해체)" : $" 평가 {Evaluation}/{PartyRules.EvaluationNeeded(Rank)}");
}
