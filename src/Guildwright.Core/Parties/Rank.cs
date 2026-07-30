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

    /// <summary>등급 이름. 눈금이 알파벳이라 한국어도 같습니다.</summary>
    public static string ToKorean(this Rank rank) => rank == Rank.SS ? "SS" : rank.ToString();

    /// <summary>몇 단 위. 상한에서 멈춥니다.</summary>
    public static Rank Above(this Rank rank, int steps) =>
        (Rank)Math.Clamp((int)rank + steps, (int)Lowest, (int)Highest);

    /// <summary>몇 단 아래. 하한에서 멈춥니다.</summary>
    public static Rank Below(this Rank rank, int steps) => rank.Above(-steps);
}
