using Guildwright.Core.Adventurers;
using Guildwright.Core.Skills;

namespace Guildwright.Core.Parties;

/// <summary>
/// 한 조합 — <b>누가 같이 나갔는가</b>.
/// <para>
/// 조합에는 이름이 없습니다. <b>멤버 집합이 곧 식별자</b>입니다. 그래서 플레이어가
/// "가상 파티를 만든다"고 선언할 필요가 없고, 같이 내보내기만 하면 누적이 붙습니다 —
/// "어느새 이 셋이 조건을 채웠네"가 되는 이유가 여기입니다.
/// </para>
/// <para>
/// 순서는 <b>서수 정렬</b>로 고정합니다. 넣은 순서가 식별자를 바꾸면 A+B와 B+A가
/// 다른 조합이 되어 누적이 갈라지고, 같은 시드가 다른 결과를 냅니다.
/// </para>
/// 근거: docs/08-design-revision.md §6.0
/// </summary>
public sealed class PartyComposition : IEquatable<PartyComposition>
{
    private PartyComposition(IReadOnlyList<string> memberIds)
    {
        MemberIds = memberIds;
        Key = string.Join("|", memberIds);
    }

    /// <summary>멤버 id. <b>서수 정렬되어 있고 중복이 없습니다.</b></summary>
    public IReadOnlyList<string> MemberIds { get; }

    /// <summary>조합의 식별자. 정렬된 id를 이어붙인 것입니다.</summary>
    public string Key { get; }

    public int Size => MemberIds.Count;

    public bool Contains(string adventurerId) => MemberIds.Contains(adventurerId, StringComparer.Ordinal);

    public static PartyComposition Of(IEnumerable<string> memberIds) =>
        new([.. memberIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)]);

    public static PartyComposition Of(IEnumerable<Adventurer> members) =>
        Of(members.Select(m => m.Id));

    public static PartyComposition Of(params Adventurer[] members) =>
        Of((IEnumerable<Adventurer>)members);

    /// <summary>이 조합에 한 명을 더한 조합. 원본은 그대로입니다.</summary>
    public PartyComposition With(string adventurerId) => Of([.. MemberIds, adventurerId]);

    /// <summary>이 조합에서 한 명을 뺀 조합.</summary>
    public PartyComposition Without(string adventurerId) =>
        Of(MemberIds.Where(id => !string.Equals(id, adventurerId, StringComparison.Ordinal)));

    public bool Equals(PartyComposition? other) =>
        other is not null && string.Equals(Key, other.Key, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as PartyComposition);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);

    public override string ToString() => Key;
}

/// <summary>조합이 성립하지 않는 이유. <see cref="None"/>이면 성립합니다.</summary>
public enum FormationProblem
{
    None,

    /// <summary>인원이 부족합니다. 솔로잉은 등급으로 열립니다.</summary>
    TooFewMembers,

    /// <summary>솔로잉 등급에 못 미칩니다.</summary>
    SoloingLocked,

    /// <summary>짐꾼이 둘 이상입니다 (§16.8b).</summary>
    TooManyPorters,

    /// <summary>전투 직업이 없습니다 — 짐꾼만으로는 못 나갑니다.</summary>
    NoCombatant,

    /// <summary>파견할 수 없는 상태(사망·은퇴·첫 해)입니다.</summary>
    NotDeployable

    // 가입 자격 미달은 여기 없습니다 — 조합 규칙이 아니라 들어오는 사람의 자격이고,
    // AdmissionProblem.RankTooLow가 그 자리입니다.
}

/// <summary>
/// 조합이 성립하는지 판정합니다. <b>상태가 없는 순수 함수</b>입니다.
/// <para>
/// 가상 파티든 정규 파티든 같은 조합 규칙을 씁니다. 다른 것은 <b>가입 자격</b>뿐이고,
/// 그것은 <see cref="IsEligibleToJoin"/>로 따로 봅니다 — <b>자격은 들어오는 사람에게만
/// 걸립니다.</b> 이미 있던 멤버에게 걸면 파티가 성장하는 순간 자기 멤버들이 자격 미달이
/// 되어 파티가 스스로 깨집니다 (F등급 둘이 모여 B급까지 올린 파티가 그 예입니다).
/// </para>
/// 근거: docs/08-design-revision.md §6.1, §6.2
/// </summary>
public static class PartyFormation
{
    /// <summary>짐꾼인가. 비전투 직업이 곧 짐꾼입니다.</summary>
    public static bool IsPorter(Adventurer member) => !member.JobProfile.Combat;

    /// <summary>
    /// 조합 규칙 판정 — 인원 · 짐꾼 · 전투원 · 파견 가능 여부.
    /// <para>가입 자격은 여기서 보지 않습니다. <see cref="IsEligibleToJoin"/>를 쓰세요.</para>
    /// </summary>
    public static FormationProblem Check(IReadOnlyList<Adventurer> members)
    {
        if (members.Count == 0) return FormationProblem.TooFewMembers;

        if (members.Any(m => !m.CanDeploy)) return FormationProblem.NotDeployable;

        if (members.Count(IsPorter) > PartyRules.MaxPorters) return FormationProblem.TooManyPorters;

        // 짐꾼만으로는 못 나갑니다. 혼자 나가는 짐꾼도 여기서 걸립니다.
        if (!members.Any(m => m.JobProfile.Combat)) return FormationProblem.NoCombatant;

        if (members.Count < PartyRules.MinimumMembers)
        {
            // 솔로잉은 금지가 아니라 등급으로 열립니다.
            return members.Count == 1 && members[0].Rank >= PartyRules.SoloingUnlock
                ? FormationProblem.None
                : members.Count == 1 ? FormationProblem.SoloingLocked : FormationProblem.TooFewMembers;
        }

        return FormationProblem.None;
    }

    /// <summary>
    /// 등급이 세워진 파티에 <b>새로 붙을</b> 자격이 있는가.
    /// <para>
    /// "B + B + F는 되도록 안 돼야 한다" 가 여기입니다. 등급이 없는 가상 파티에는
    /// 기준이 없으므로 이 판정을 하지 않습니다 — 그래서 첫 파티가 막히지 않습니다.
    /// </para>
    /// </summary>
    public static bool IsEligibleToJoin(Adventurer candidate, Rank partyRank) =>
        candidate.Rank >= partyRank.Below(PartyRules.JoinRankGap);

    /// <summary>조합 규칙을 지키는가.</summary>
    public static bool CanForm(IReadOnlyList<Adventurer> members) =>
        Check(members) == FormationProblem.None;

    /// <summary>왜 안 되는지 한국어로.</summary>
    public static string ToKorean(this FormationProblem problem) => problem switch
    {
        FormationProblem.None => "성립",
        FormationProblem.TooFewMembers => $"인원 부족 (최소 {PartyRules.MinimumMembers}명)",
        FormationProblem.SoloingLocked => $"솔로잉은 {PartyRules.SoloingUnlock.ToKorean()}등급부터",
        FormationProblem.TooManyPorters => $"짐꾼은 최대 {PartyRules.MaxPorters}명",
        FormationProblem.NoCombatant => "전투 직업이 없음 (짐꾼만으로는 구성 불가)",
        FormationProblem.NotDeployable => "파견할 수 없는 상태",
        _ => problem.ToString()
    };
}
