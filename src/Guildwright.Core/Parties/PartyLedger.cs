using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Parties;

/// <summary>정규 등록이 안 되는 이유.</summary>
public enum RegistrationProblem
{
    None,

    /// <summary>함께 나간 달이 모자랍니다.</summary>
    NotEnoughMonths,

    /// <summary>이미 정규 파티에 속한 사람이 있습니다. 한 사람은 정규 파티 하나입니다.</summary>
    AlreadyInRegularParty,

    /// <summary>조합 자체가 성립하지 않습니다. 자세한 이유는 <see cref="PartyFormation.Check"/>.</summary>
    InvalidComposition
}

/// <summary>증원이 안 되는 이유.</summary>
public enum AdmissionProblem
{
    None,

    /// <summary>그 파티와 함께 나간 달이 모자랍니다.</summary>
    NotEnoughMonths,

    /// <summary>이미 다른 정규 파티에 속해 있습니다.</summary>
    AlreadyInRegularParty,

    /// <summary>해체된 파티입니다.</summary>
    Disbanded,

    /// <summary>이미 그 파티의 멤버입니다.</summary>
    AlreadyMember,

    /// <summary>파티 등급에 비해 등급이 낮습니다 ("B + B + F는 안 된다").</summary>
    RankTooLow,

    /// <summary>넣으면 조합 규칙을 깹니다 (짐꾼 둘 등).</summary>
    InvalidComposition
}

/// <summary>
/// 파티 장부 — <b>가상 파티의 누적과 정규 파티의 소속</b>을 함께 관리합니다.
/// <para>
/// 층은 둘입니다. <b>가상 파티</b>는 이 장부의 누적으로만 존재하고(객체가 없습니다),
/// <b>정규 파티</b>는 등록되어 <see cref="Party"/> 객체를 갖습니다.
/// </para>
/// <para>
/// 누적은 <b>정확히 그 조합에만</b> 쌓입니다. A+B+C로 나간 달은 A+B의 달이 아닙니다.
/// </para>
/// <para>
/// ⚠️ 한때 <b>상위 집합으로</b> 셌습니다(A+B+C 6달 → A+B도 6달). 문서에 근거가 없는
/// 구현자 규칙이었고, §6.1의 "증원도 6개월"과 §6.6의 "죽음의 대가는 증원에 다시 6개월"을
/// <b>무력화</b>했습니다 — 넷으로 여섯 달 나간 뒤 한 명이 죽으면 대기 0달로 즉시 합류가
/// 됩니다. "누구와 등록할까"라는 선택은 <b>달마다 다른 조합을 굴리는 것</b>으로 이미
/// 성립하므로 그 규칙이 필요하지 않았습니다.
/// </para>
/// <para>부작용이 없습니다 — 시간·난수·파일에 손대지 않습니다. 달은 호출자가 넘겨줍니다.</para>
/// 근거: docs/08-design-revision.md §6.0, §6.1
/// </summary>
public sealed class PartyLedger
{
    // 조합 → 함께 나간 달. 순회할 때는 항상 Key로 정렬합니다.
    private readonly Dictionary<PartyComposition, int> _months = [];
    private readonly List<Party> _parties = [];

    /// <summary>등록된 정규 파티. 해체된 것도 남습니다 — 이력이기 때문입니다.</summary>
    public IReadOnlyList<Party> Parties => _parties;

    /// <summary>살아 있는 정규 파티.</summary>
    public IEnumerable<Party> ActiveParties => _parties.Where(p => !p.Disbanded);

    /// <summary>
    /// 그 조합으로 한 달 나갔다고 기록합니다.
    /// <para>
    /// <b>조합이 성립하지 않으면 기록하지 않습니다.</b> 자격이 안 맞는 사람과는 애초에
    /// 같이 못 나가므로, "같이 내보내 놓고 누적만 안 쌓임"이라는 상태가 없습니다.
    /// </para>
    /// </summary>
    /// <returns>기록되었는지.</returns>
    public bool RecordMonth(IReadOnlyList<Adventurer> members)
    {
        if (!PartyFormation.CanForm(members)) return false;
        if (!EveryNewcomerIsEligible(members)) return false;

        var composition = PartyComposition.Of(members);
        _months[composition] = _months.GetValueOrDefault(composition) + 1;
        return true;
    }

    /// <summary>
    /// 등급이 세워진 정규 파티에 <b>붙어 나가는 사람</b>이 자격을 갖췄는가.
    /// <para>
    /// 기준은 <b>조합에 섞여 있는 정규 파티 중 가장 높은 등급</b>입니다. 그 파티의 멤버는
    /// 자격을 안 봅니다 — F등급 둘이 모여 B급까지 올린 파티가 자기 멤버 때문에 못 나가면
    /// 안 됩니다 (§"등급 −2 제한이 막는 것": "가입 후 파티 등급이 올라도 기존 멤버는 남습니다").
    /// </para>
    /// <para>
    /// ⚠️ 예전에는 <b>파티가 조합에 온전히 들어 있을 때만</b> 기준으로 삼았습니다. 그러면
    /// 1군에서 한 명만 빼고 신입을 끼우는 것으로 자격이 통째로 우회됩니다 —
    /// B급 파티원 둘이 난이도를 열어주고 신입은 위험 없이 숙련을 챙기는,
    /// §6.0이 "성립할 자리가 없다"고 한 바로 그 캐리 태우기입니다.
    /// </para>
    /// </summary>
    private bool EveryNewcomerIsEligible(IReadOnlyList<Adventurer> members)
    {
        // 조합에 한 명이라도 섞여 있는 정규 파티들. 등급 순으로 가장 높은 것이 기준입니다.
        var involved = ActiveParties
            .Where(p => p.Members.Any(m => members.Any(x => string.Equals(x.Id, m.Id, StringComparison.Ordinal))))
            .ToArray();

        if (involved.Length == 0) return true;

        var standard = involved.MaxBy(p => (int)p.Rank)!;
        if (standard.Rank == Ranks.Lowest) return true;   // 최하 등급은 자격이 걸릴 기준이 못 됩니다.

        foreach (var person in members)
        {
            // 기준을 세운 파티의 멤버는 자격을 안 봅니다.
            if (standard.Members.Any(m => string.Equals(m.Id, person.Id, StringComparison.Ordinal))) continue;

            if (!PartyFormation.IsEligibleToJoin(person, standard.Rank)) return false;
        }

        return true;
    }

    /// <summary>
    /// 그 조합이 함께 나간 달. <b>정확히 그 조합으로 나간 달만</b> 셉니다.
    /// </summary>
    public int MonthsTogether(PartyComposition composition) =>
        composition.Size == 0 ? 0 : _months.GetValueOrDefault(composition);

    public int MonthsTogether(IReadOnlyList<Adventurer> members) =>
        MonthsTogether(PartyComposition.Of(members));

    /// <summary>
    /// 누적이 쌓여 있는 조합 전부 — <b>가상 파티 목록</b>입니다.
    /// <para>
    /// 등록 가능한 것만 보여주면 플레이어는 <b>조건을 채우는 순간까지 아무것도 못 봅니다.</b>
    /// "어느새 이 셋이 조건을 채웠네"가 되려면 쌓이는 과정이 보여야 합니다 (§6.0).
    /// </para>
    /// <para>많이 나간 조합부터, 동수면 서수 정렬 — 순서가 흔들리면 화면이 매번 달라집니다.</para>
    /// </summary>
    public IReadOnlyList<(PartyComposition Composition, int Months)> VirtualParties =>
        [.. _months.Select(e => (e.Key, e.Value))
                   .OrderByDescending(e => e.Item2)
                   .ThenBy(e => e.Item1.Key, StringComparer.Ordinal)];

    /// <summary>그 사람이 속한 정규 파티. 없으면 <c>null</c>. 한 사람은 정규 파티 하나입니다.</summary>
    public Party? RegularPartyOf(string adventurerId) =>
        ActiveParties.FirstOrDefault(p =>
            p.Members.Any(m => string.Equals(m.Id, adventurerId, StringComparison.Ordinal)));

    /// <summary>이 조합이 그대로 어떤 정규 파티인가. 부분 조합이면 <c>null</c>입니다.</summary>
    public Party? RegularPartyOf(IReadOnlyList<Adventurer> members)
    {
        var composition = PartyComposition.Of(members);
        return ActiveParties.FirstOrDefault(p => p.Composition.Equals(composition));
    }

    // ---- 등록 ----

    /// <summary>정규 등록이 가능한가.</summary>
    public RegistrationProblem CheckRegistration(IReadOnlyList<Adventurer> members)
    {
        // 등록에는 최소 인원이 필요합니다 — 솔로잉이 열려도 혼자는 파티가 아닙니다.
        if (members.Count < PartyRules.MinimumMembers) return RegistrationProblem.InvalidComposition;

        // 짐꾼 규칙은 등록에도 걸립니다 (§16.8b).
        // ⚠️ 파견 가능 여부(CanDeploy)는 보지 않습니다 — 등록 조건은 "함께 나간 6개월"과
        //   소속뿐입니다(§6.1). 예전에는 PartyFormation.Check를 그대로 써서, 멤버 한 명이
        //   불구가 되면 여섯 달을 채웠어도 등록이 막혔습니다. 문서에 없는 제약이었습니다.
        if (members.Count(PartyFormation.IsPorter) > PartyRules.MaxPorters)
            return RegistrationProblem.InvalidComposition;

        if (!members.Any(m => m.JobProfile.Combat)) return RegistrationProblem.InvalidComposition;

        if (members.Any(m => RegularPartyOf(m.Id) is not null))
            return RegistrationProblem.AlreadyInRegularParty;

        if (MonthsTogether(members) < PartyRules.MonthsToRegister)
            return RegistrationProblem.NotEnoughMonths;

        return RegistrationProblem.None;
    }

    /// <summary>
    /// 정규 파티로 등록합니다. <b>강제가 아닙니다</b> — 조건을 채워도 등록 안 하고 굴러갑니다.
    /// </summary>
    /// <returns>등록된 파티. 조건이 안 되면 <c>null</c>.</returns>
    public Party? Register(string id, string name, IReadOnlyList<Adventurer> members)
    {
        if (CheckRegistration(members) != RegistrationProblem.None) return null;

        var party = new Party(id, name, members);
        _parties.Add(party);
        return party;
    }

    /// <summary>지금 등록할 수 있는 조합들. <b>여기서 "누구와 등록할까"가 판단이 됩니다.</b></summary>
    public IReadOnlyList<PartyComposition> RegistrableCompositions(IReadOnlyList<Adventurer> roster)
    {
        var byId = roster.ToDictionary(a => a.Id, StringComparer.Ordinal);

        var found = new List<PartyComposition>();
        foreach (var composition in _months.Keys.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            var members = composition.MemberIds
                .Select(id => byId.GetValueOrDefault(id))
                .OfType<Adventurer>()
                .ToArray();

            if (members.Length != composition.Size) continue;
            if (CheckRegistration(members) == RegistrationProblem.None) found.Add(composition);
        }

        return found;
    }

    // ---- 증원 ----

    /// <summary>증원이 가능한가. <b>새 멤버도 그 파티와 6개월을 채워야 합니다.</b></summary>
    public AdmissionProblem CheckAdmission(Party party, Adventurer candidate)
    {
        if (party.Disbanded) return AdmissionProblem.Disbanded;

        if (party.Members.Any(m => string.Equals(m.Id, candidate.Id, StringComparison.Ordinal)))
            return AdmissionProblem.AlreadyMember;

        if (RegularPartyOf(candidate.Id) is not null) return AdmissionProblem.AlreadyInRegularParty;

        // 자격은 들어오는 사람에게만 걸립니다 — 기존 멤버의 등급은 보지 않습니다.
        if (!PartyFormation.IsEligibleToJoin(candidate, party.Rank)) return AdmissionProblem.RankTooLow;

        // 넣은 뒤의 조합이 규칙을 지키는지 봅니다 — 짐꾼 둘이 여기서 걸립니다.
        var after = (IReadOnlyList<Adventurer>)[.. party.Members, candidate];
        if (!PartyFormation.CanForm(after)) return AdmissionProblem.InvalidComposition;

        if (MonthsTogether(after) < PartyRules.MonthsToRegister) return AdmissionProblem.NotEnoughMonths;

        return AdmissionProblem.None;
    }

    /// <summary>증원합니다.</summary>
    /// <returns>실제로 들어왔는지.</returns>
    public bool Admit(Party party, Adventurer candidate)
    {
        if (CheckAdmission(party, candidate) != AdmissionProblem.None) return false;

        party.Add(candidate);
        return true;
    }

    // ---- 이탈과 해체 ----

    /// <summary>
    /// 그 사람을 소속 정규 파티에서 뺍니다 (사망·은퇴·탈퇴).
    /// <b>남은 멤버가 1명이면 파티가 해체됩니다.</b>
    /// </summary>
    /// <returns>속한 파티가 있어 실제로 뺐는지.</returns>
    public bool Leave(string adventurerId)
    {
        var party = RegularPartyOf(adventurerId);
        return party is not null && party.Remove(adventurerId);
    }

    // ---- 평가 ----

    /// <summary>
    /// 파티가 평가를 쌓습니다. <b>등급은 여기서 오르지 않습니다</b> — 승급 의뢰를 통과해야 오릅니다.
    /// <para>파티 등급은 개인 등급에서 파생되지 않고 <b>독립적으로</b> 쌓입니다 (§6.2).</para>
    /// </summary>
    public void RecordEvaluation(Party party, int points) => party.RecordEvaluation(points);

    /// <summary>
    /// 파티를 한 단 승급시킵니다. <b>승급 의뢰를 통과했을 때만</b> 불립니다 (§6.5).
    /// </summary>
    /// <returns>실제로 올랐는지.</returns>
    public bool Promote(Party party) => party.Promote();
}
