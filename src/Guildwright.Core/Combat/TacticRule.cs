namespace Guildwright.Core.Combat;

/// <summary>전술 규칙의 발동 조건.</summary>
public enum TacticCondition
{
    /// <summary>항상 참. 기본 규칙(fallback)으로 목록 마지막에 둡니다.</summary>
    Always,

    /// <summary>자신의 HP 비율이 임계값 미만.</summary>
    SelfHpBelow,

    /// <summary>아군 중 누군가의 HP 비율이 임계값 미만.</summary>
    AllyHpBelow,

    /// <summary>적 중 누군가의 HP 비율이 임계값 미만 (마무리 기회).</summary>
    EnemyHpBelow
}

/// <summary>전술 규칙이 지시하는 행동.</summary>
public enum TacticAction
{
    /// <summary>가장 가까운(= 첫 번째 생존) 적을 공격.</summary>
    AttackNearest,

    /// <summary>HP가 가장 낮은 적을 공격. 마무리에 유리.</summary>
    AttackWeakest,

    /// <summary>공격력이 가장 높은 적을 공격.</summary>
    AttackStrongest,

    /// <summary>회복약 사용.</summary>
    UsePotion,

    /// <summary>방어 태세. 받는 피해 감소.</summary>
    Defend
}

/// <summary>
/// FF12 감빗과 유사한 조건-행동 규칙.
/// <para>
/// 캐릭터는 육성을 통해 규칙 슬롯을 늘리고 규칙 자체를 배웁니다.
/// 플레이어는 이 목록의 순서를 편성합니다 — 그게 곧 빌드입니다.
/// </para>
/// 근거: docs/04-game-design.md §4.1
/// </summary>
/// <param name="Condition">발동 조건.</param>
/// <param name="Threshold">조건이 HP 비율을 볼 때 쓰는 임계값 (0.0~1.0). Always면 무시됩니다.</param>
/// <param name="Action">지시할 행동.</param>
public readonly record struct TacticRule(
    TacticCondition Condition,
    double Threshold,
    TacticAction Action)
{
    public static TacticRule Always(TacticAction action) =>
        new(TacticCondition.Always, 0.0, action);

    public static TacticRule SelfHpBelow(double threshold, TacticAction action) =>
        new(TacticCondition.SelfHpBelow, threshold, action);

    public static TacticRule EnemyHpBelow(double threshold, TacticAction action) =>
        new(TacticCondition.EnemyHpBelow, threshold, action);

    public static TacticRule AllyHpBelow(double threshold, TacticAction action) =>
        new(TacticCondition.AllyHpBelow, threshold, action);
}
