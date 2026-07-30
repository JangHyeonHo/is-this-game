namespace Guildwright.Core.Combat;

/// <summary>전술 규칙의 발동 조건.</summary>
public enum TacticCondition
{
    /// <summary>항상 참. 기본 규칙으로 목록 마지막에 둡니다.</summary>
    Always,
    /// <summary>자신의 HP 비율이 임계값 미만.</summary>
    SelfHpBelow,
    /// <summary>아군 중 누군가의 HP 비율이 임계값 미만.</summary>
    AllyHpBelow,
    /// <summary>적 중 누군가의 HP 비율이 임계값 미만 (마무리 기회).</summary>
    EnemyHpBelow,
    /// <summary>자신이 전열에 있음.</summary>
    SelfInFrontRow,
    /// <summary>자신이 후열에 있음.</summary>
    SelfInBackRow,
    /// <summary>아군 전열이 비어 있음. 후열이 노출된 위기 상황.</summary>
    FrontRowEmpty
}

/// <summary>
/// 전술 규칙이 지시하는 행동.
/// <para>
/// 무기 스타일이 어떤 행동을 열어주는지가 파티 편성의 핵심입니다.
/// 회복은 지팡이만, 도발은 한손+방패만 가능합니다.
/// </para>
/// </summary>
public enum TacticAction
{
    /// <summary>닿는 범위에서 첫 번째 적을 공격.</summary>
    AttackNearest,
    /// <summary>닿는 범위에서 HP가 가장 낮은 적을 공격.</summary>
    AttackWeakest,
    /// <summary>닿는 범위에서 공격력이 가장 높은 적을 공격.</summary>
    AttackStrongest,
    /// <summary>적 후열을 직접 노림. 활·석궁·지팡이만 가능.</summary>
    AttackBackRow,
    /// <summary>광역 공격. 다수를 동시에 타격.</summary>
    AttackAll,
    /// <summary>마법 회복. 지팡이만 가능.</summary>
    HealAlly,
    /// <summary>아군에게 강화를 겁니다.</summary>
    BuffAlly,
    /// <summary>적에게 약화를 겁니다.</summary>
    DebuffEnemy,
    /// <summary>도발. 적의 공격을 자신에게 끕니다. 한손+방패만 가능.</summary>
    Taunt,
    /// <summary>회복약을 자신에게 사용.</summary>
    UsePotion,
    /// <summary>
    /// 회복약을 <b>아군에게</b> 건넵니다. 짐꾼의 핵심입니다.
    /// <para>짐은 아무나 들 수 있지만 위기의 순간에 제때 쓰는 것은 다릅니다.</para>
    /// </summary>
    GivePotion,
    /// <summary>
    /// 주무기와 보조무기를 바꿔 듭니다. <b>턴을 하나 씁니다.</b>
    /// <para>공짜면 "액티브는 특정 무기를 요구한다"가 무의미해집니다.</para>
    /// </summary>
    SwitchWeapon,
    /// <summary>방어 태세.</summary>
    Defend,
    /// <summary>후열로 물러납니다. 그 턴은 공격하지 못합니다.</summary>
    MoveBack,
    /// <summary>전열로 나섭니다.</summary>
    MoveFront
}

/// <summary>
/// FF12 감빗과 유사한 조건-행동 규칙.
/// <para>
/// 캐릭터는 육성을 통해 규칙 슬롯을 늘리고 규칙 자체를 배웁니다.
/// 플레이어는 이 목록의 순서를 편성합니다 — 그게 곧 빌드입니다.
/// </para>
/// 근거: docs/01-game-design.md §4.1
/// </summary>
/// <param name="Condition">발동 조건.</param>
/// <param name="Threshold">HP 비율을 보는 조건에서 쓰는 임계값 (0.0~1.0).</param>
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

    public static TacticRule When(TacticCondition condition, TacticAction action) =>
        new(condition, 0.0, action);
}
