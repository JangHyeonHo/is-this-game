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
    /// <para>[검토중] C냐 D냐 — "최소 못해도 C등급이나 D등급부터".</para>
    /// </summary>
    public const Rank SoloingUnlock = Rank.D;

    /// <summary>한 파티에 들어갈 수 있는 짐꾼 수 (§16.8b).</summary>
    public const int MaxPorters = 1;

    /// <summary>
    /// 등급이 한 단 오르는 데 필요한 평가 점수. ⚠️ 임시값 — 승급 속도는 아직 안 잽니다.
    /// <para>
    /// 뒤로 갈수록 커집니다. 선형이면 SS까지 그냥 시간만 들이면 되는 일이 됩니다.
    /// </para>
    /// </summary>
    public static readonly int[] EvaluationToRankUp = [30, 70, 130, 220, 350, 520, 750];

    /// <summary>
    /// 그 등급에서 다음 등급까지 남은 평가 점수. 최고 등급이면 0입니다.
    /// </summary>
    public static int EvaluationNeeded(Rank rank) =>
        rank == Ranks.Highest ? 0 : EvaluationToRankUp[(int)rank];
}
