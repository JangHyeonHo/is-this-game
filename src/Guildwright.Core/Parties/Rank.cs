namespace Guildwright.Core.Parties;

/// <summary>
/// 등급 F ~ SS.
/// <para>
/// <b>모험가 등급 · 파티 등급 · 길드 랭크가 같은 눈금을 씁니다.</b> 셋은 서로 다른 것을 열지만
/// (§6.3 — 길드 랭크는 의뢰의 양, 개인 등급은 난이도, 파티 등급은 파티 전용 의뢰),
/// 눈금이 다르면 "파티 등급 − 2 이상"같은 비교를 할 수 없습니다.
/// </para>
/// <para>
/// 값의 순서가 곧 높낮이입니다 — <c>F &lt; E &lt; … &lt; SS</c>. 비교에 그대로 쓸 수 있습니다.
/// </para>
/// 근거: docs/08-design-revision.md §6.1
/// </summary>
public enum Rank
{
    F = 0,
    E = 1,
    D = 2,
    C = 3,
    B = 4,
    A = 5,
    S = 6,
    SS = 7
}

/// <summary>등급 눈금을 다루는 헬퍼.</summary>
public static class Ranks
{
    /// <summary>가장 낮은 등급.</summary>
    public const Rank Lowest = Rank.F;

    /// <summary>가장 높은 등급.</summary>
    public const Rank Highest = Rank.SS;

    /// <summary>낮은 것부터 순서대로.</summary>
    public static IReadOnlyList<Rank> All { get; } =
        [Rank.F, Rank.E, Rank.D, Rank.C, Rank.B, Rank.A, Rank.S, Rank.SS];

    /// <summary>등급 글자. 눈금이 알파벳이라 한국어도 같습니다.</summary>
    public static string ToKorean(this Rank rank) => rank.ToString();

    /// <summary>
    /// 화면에 쓰는 표기 — <b>"A급"</b>.
    /// <para>
    /// 무기 적성도 A~S 눈금을 쓰므로 화면에 <c>A</c> 하나만 뜨면 무엇의 A인지 알 수 없습니다.
    /// <b>표기로 가르는 것이 싸고 충분합니다</b> — 적성은 "적성 A", 등급은 "A급".
    /// </para>
    /// 근거: docs/08-design-revision.md §"등급 표기 충돌"
    /// </summary>
    public static string Label(this Rank rank) => $"{rank}급";

    /// <summary>몇 단 위. 상한에서 멈춥니다.</summary>
    public static Rank Above(this Rank rank, int steps) =>
        (Rank)Math.Clamp((int)rank + steps, (int)Lowest, (int)Highest);

    /// <summary>몇 단 아래. 하한에서 멈춥니다.</summary>
    public static Rank Below(this Rank rank, int steps) => rank.Above(-steps);
}
