namespace Guildwright.Core.Combat;

/// <summary>
/// 상태 효과.
/// <para>
/// ⚠️ <b>의도적으로 적게 유지합니다.</b> 상태이상을 20종 만들면 조합 폭발로 밸런싱이 불가능해집니다.
/// 새로 추가하기 전에 "기존 것으로 표현할 수 없는가"를 먼저 확인하세요.
/// </para>
/// </summary>
public enum StatusEffectKind
{
    /// <summary>공격력 상승.</summary>
    Empowered,
    /// <summary>방어력 상승.</summary>
    Warded,
    /// <summary>공격력 감소.</summary>
    Weakened,
    /// <summary>방어력 감소.</summary>
    Sundered,
    /// <summary>중독. 매 라운드 지속 피해.</summary>
    Poisoned,
    /// <summary>둔화. 행동 순서가 밀립니다.</summary>
    Slowed,
    /// <summary>도발됨. 도발한 대상만 공격하게 됩니다.</summary>
    Taunted
}

/// <param name="Kind">종류.</param>
/// <param name="RemainingRounds">남은 라운드.</param>
/// <param name="Magnitude">세기. 배율 계열은 0.25면 25% 변화.</param>
/// <param name="SourceId">건 사람. 도발 대상 추적에 씁니다.</param>
public sealed record StatusEffect(
    StatusEffectKind Kind,
    int RemainingRounds,
    double Magnitude,
    string? SourceId = null)
{
    public StatusEffect Tick() => this with { RemainingRounds = RemainingRounds - 1 };

    public bool IsExpired => RemainingRounds <= 0;

    public static string ToKorean(StatusEffectKind kind) => kind switch
    {
        StatusEffectKind.Empowered => "공격 강화",
        StatusEffectKind.Warded => "방어 강화",
        StatusEffectKind.Weakened => "공격 약화",
        StatusEffectKind.Sundered => "방어 약화",
        StatusEffectKind.Poisoned => "중독",
        StatusEffectKind.Slowed => "둔화",
        StatusEffectKind.Taunted => "도발됨",
        _ => "?"
    };
}
