namespace Guildwright.Core.Items;

/// <summary>
/// 소모품 (docs/07 §20.7 — 당장은 소형 포션 둘뿐).
/// 파견 보급(회복약)과의 연결은 파견을 아이템 체계에 잇는 단계에서 한다.
/// </summary>
public enum ConsumableKind
{
    HealthPotionSmall,
    ManaPotionSmall
}

public static class Consumables
{
    public static string ToKorean(this ConsumableKind kind) => kind switch
    {
        ConsumableKind.HealthPotionSmall => "체력 포션(소)",
        ConsumableKind.ManaPotionSmall => "마나 포션(소)",
        _ => "?"
    };
}
