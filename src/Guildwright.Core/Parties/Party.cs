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
/// 근거: docs/07-decisions.md §6.0, §6.2
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
    /// 승급 의뢰를 받을 만큼 쌓았는가.
    /// <para>
    /// <b>이것만으로는 오르지 않습니다.</b> 평가는 "이제 올릴 때가 됐다"는 표시일 뿐이고,
    /// 실제로 오르는 것은 <see cref="Promote"/> — 승급 의뢰를 통과했을 때입니다.
    /// </para>
    /// <para>
    /// <b>[검토중]</b> 승급 의뢰의 자격 조건이 평가 문턱인지 (§6.5). 문턱이라는 형태 자체가
    /// 아직 승인되지 않았으므로 <b>자격에만 쓰고 승급 판정에는 쓰지 않습니다.</b>
    /// </para>
    /// </summary>
    public bool ReadyToPromote =>
        !Disbanded && Rank != Ranks.Highest && Evaluation >= PartyRules.EvaluationNeeded(Rank);

    /// <summary>
    /// 파티로서 평가를 쌓습니다. <b>등급은 여기서 오르지 않습니다.</b>
    /// <para>
    /// 예전에는 문턱을 넘는 순간 등급이 올라갔습니다 — 한 번에 다섯 단이 오르기도 했습니다.
    /// 그것이 §6.5가 명시적으로 금지한 형태입니다: <b>"승급이 자동이 아니라 사건이 됩니다.
    /// 문턱을 넘는 순간 조용히 올라가는 게 아니라, 의뢰를 받고 통과해야 합니다."</b>
    /// </para>
    /// </summary>
    internal void RecordEvaluation(int points)
    {
        if (Disbanded || points <= 0) return;
        Evaluation += points;
    }

    /// <summary>
    /// 한 단 승급합니다. <b>승급 의뢰를 통과했을 때만</b> 불립니다 (§6.5).
    /// <para>쌓은 평가는 승급과 함께 소진됩니다 — 다음 단은 다시 쌓아야 합니다.</para>
    /// </summary>
    /// <returns>실제로 올랐는지.</returns>
    internal bool Promote()
    {
        if (Disbanded || Rank == Ranks.Highest) return false;

        Evaluation = Math.Max(0, Evaluation - PartyRules.EvaluationNeeded(Rank));
        Rank = Rank.Above(1);
        return true;
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
        $"{Name} [{Rank.Label()}] {_members.Count}명" +
        (Disbanded
            ? " (해체)"
            : $" 평가 {Evaluation}/{PartyRules.EvaluationNeeded(Rank)}" +
              (ReadyToPromote ? " · 승급 의뢰 가능" : ""));
}
