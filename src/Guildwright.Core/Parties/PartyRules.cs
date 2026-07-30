namespace Guildwright.Core.Parties;

/// <summary>
/// 파티 규칙의 수치.
/// <para>
/// ⚠️ <b>평가 관련 수치는 임시값입니다.</b> 승급 속도는 배치 시뮬레이션으로 잡습니다.
/// 인원·달 수·등급 격차는 설계에서 나온 값이라 임시값이 아닙니다.
/// </para>
/// 근거: docs/08-design-revision.md §6.1
/// </summary>
public static class PartyRules
{
    /// <summary>파티 최소 인원. 혼자는 파티가 아닙니다.</summary>
    public const int MinimumMembers = 2;

    /// <summary>정규 등록에 필요한 <b>함께 나간 달</b>. 증원에도 같이 걸립니다.</summary>
    public const int MonthsToRegister = 6;

    /// <summary>
    /// 가입 자격의 등급 격차. <b>파티 등급 − 이 값</b> 이상이어야 들어올 수 있습니다.
    /// <para>[방향] "최소한 파티 등급의 −2등급까지만 허용되거나 그런식으로".</para>
    /// </summary>
    public const int JoinRankGap = 2;

    /// <summary>
    /// 솔로잉이 열리는 개인 등급.
    /// <para>
    /// ⚠️ <b>[검토중] — 승인된 값이 아닙니다.</b> 주인님 발언은 "최소 못해도 C등급이나
    /// D등급부터"이고 어느 쪽인지 정해지지 않았습니다. 예전에 에이전트가 C로 확정한 것이
    /// 승인 취소됐으므로, <b>D도 같은 자격의 미승인 값</b>입니다.
    /// </para>
    /// <para>
    /// 코드는 하나를 골라야 돌아가므로 D를 씁니다. <b>이 값에 의존하는 판단을 하지 마세요</b> —
    /// docs/06 #41 임시값 목록에 올려두었으니 확정되면 여기만 고칩니다.
    /// </para>
    /// </summary>
    public const Rank SoloingUnlock = Rank.D;

    /// <summary>한 파티에 들어갈 수 있는 짐꾼 수 (§16.8b).</summary>
    public const int MaxPorters = 1;

    /// <summary>
    /// 등급이 한 단 오르는 데 필요한 평가 점수. ⚠️ 임시값 — 승급 속도는 아직 안 잽니다.
    /// <para>
    /// 뒤로 갈수록 커집니다. 선형이면 SS까지 그냥 시간만 들이면 되는 일이 됩니다.
    /// </para>
    /// <para>
    /// ⚠️ <b>[검토중] — 수치뿐 아니라 "점수 문턱"이라는 형태 자체가 미승인입니다.</b>
    /// §6.4는 "파티 평가가 무엇으로 얼마나 쌓이는가"를 검토중으로 두었습니다. 그래서
    /// 이 문턱은 <b>승급 의뢰를 받을 자격</b>(<see cref="Party.ReadyToPromote"/>)에만 쓰고,
    /// <b>승급 판정에는 쓰지 않습니다</b> — 문턱을 넘는 순간 오르면 §6.5 위반입니다.
    /// </para>
    /// </summary>
    public static readonly int[] EvaluationToRankUp = [30, 70, 130, 220, 350, 520, 750];

    /// <summary>
    /// 그 등급에서 다음 등급까지 남은 평가 점수. 최고 등급이면 0입니다.
    /// </summary>
    public static int EvaluationNeeded(Rank rank) =>
        rank == Ranks.Highest ? 0 : EvaluationToRankUp[(int)rank];
}
