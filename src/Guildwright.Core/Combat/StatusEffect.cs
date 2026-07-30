namespace Guildwright.Core.Combat;

/// <summary>
/// 상태 효과의 <b>기전</b>. 이름이 아니라 작동 방식입니다.
/// <para>
/// ⚠️ <b>코드 경로는 여기에만 있습니다.</b> 새 상태 효과를 추가하는 것은
/// <see cref="StatusEffects.Catalogue"/>에 줄 하나를 넣는 일이고, 기전은 늘리지 않습니다.
/// </para>
/// <para>
/// 예전에는 열거형에 이름을 나열했습니다(공격강화·방어강화·공격약화·…). 그러면
/// 공격·방어·명중·회피·속도에 각각 강화와 약화를 두는 것만으로 10종이 되는데,
/// 이것들은 전부 "무엇을 얼마나 몇 라운드"라는 <b>같은 모양</b>입니다.
/// <b>폭발하는 건 이름이지 기전이 아닙니다.</b>
/// </para>
/// <para>
/// 그리고 이 구분이 설계 질문에도 답을 줍니다 —
/// <b>설정값을 다르게 못 주는 것은 만들 이유가 없는 것</b>입니다.
/// </para>
/// 근거: docs/07-decisions.md §18.1
/// </summary>
public enum EffectMechanism
{
    /// <summary>수치를 올리거나 내립니다.</summary>
    StatShift,
    /// <summary>매 라운드 피해를 줍니다.</summary>
    DamageOverTime,
    /// <summary>확률적으로 행동을 못 하게 합니다. 확률 1.0이면 완전 행동 불가.</summary>
    Incapacitate,
    /// <summary>특정 종류의 행동만 확정으로 막습니다.</summary>
    RestrictAction,
    /// <summary><b>플레이어의 지시가 통하지 않습니다.</b> 행동은 하는데 말을 안 듣습니다.</summary>
    LoseControl,
    /// <summary>표적 선정을 비틉니다 — 끌어당기거나 제외합니다.</summary>
    TargetShift,
    /// <summary>HP 위에 얹히는 임시 방벽.</summary>
    Barrier,
    /// <summary>회복을 늘리거나 막습니다.</summary>
    Recovery
}

/// <summary>수치 증감이 건드리는 대상.</summary>
public enum ShiftTarget
{
    None,
    Power,
    Guard,
    Accuracy,
    Evasion,
    Speed
}

/// <summary>
/// 지속 피해가 커지는 방식. <b>지속 피해 넷을 가르는 축입니다.</b>
/// <para>
/// 넷 다 자연 해제가 없으므로(치료해야 풀림) "짧다/길다"로는 구분할 수 없습니다.
/// 그래서 <b>무엇 때문에 나빠지느냐</b>로 갈랐습니다.
/// </para>
/// </summary>
public enum GrowthMode
{
    /// <summary>안 커집니다. 화상 — 총량이 얼마인지 계산하면 됩니다.</summary>
    None,
    /// <summary>다시 걸리면 쌓입니다. 중독 — 적이 반복해야 위험해집니다.</summary>
    PerStack,
    /// <summary>
    /// 행동할 때마다 커집니다. 출혈 — 한 번만 걸려도 방치하면 위험해집니다.
    /// <para>
    /// 이 규칙 한 줄이 전투 안팎을 모두 해결합니다 — 전투 중에는 매 턴 행동하니
    /// 계속 커지고, 전투가 끝나면 행동이 없으니 저절로 멈춥니다.
    /// </para>
    /// </summary>
    PerAction
}

/// <summary>막히는 행동의 종류.</summary>
public enum ActionRestriction
{
    None,
    /// <summary>이동만 막습니다. 공격은 가능합니다.</summary>
    Movement,
    /// <summary>마나를 쓰는 스킬만 막습니다.</summary>
    ManaSkills
}

/// <summary>
/// 이 상태를 푸는 소모품.
/// <para>
/// 짐 수량 한도가 있으므로 다 챙길 수 없습니다. <b>어떤 마물이 나올지 짐작해서
/// 무엇을 들려 보낼지 정하는 것</b>이 보급 판단입니다.
/// </para>
/// <para>석화는 성수 외에 <b>정화 스킬</b>로도 풀립니다 — 그건 아이템이 아닙니다.</para>
/// </summary>
public enum CureItem
{
    /// <summary>소모품으로 풀 수 없습니다. 시간이 지나면 풀리거나, 스킬이 필요합니다.</summary>
    None,
    Antidote,
    BurnSalve,
    Bandage,
    FrostSalve,
    ParalysisCure,
    /// <summary>성수. 석화와 저주 둘을 풉니다.</summary>
    HolyWater
}

/// <summary>
/// 상태 효과의 이름.
/// <para>
/// <b>이 열거형이 길어지는 것은 문제가 아닙니다.</b> 이름이 늘어도 코드 경로는
/// <see cref="EffectMechanism"/> 8종에 머무릅니다.
/// </para>
/// </summary>
public enum EffectName
{
    // ---- 수치 증감 ----
    PowerUp,
    PowerDown,
    GuardUp,
    GuardDown,
    AccuracyUp,
    AccuracyDown,
    EvasionUp,
    EvasionDown,
    SpeedUp,
    SpeedDown,

    // ---- 지속 피해 ----
    Poison,
    Burn,
    Bleed,
    Frostbite,

    // ---- 확률적 행동 불가 ----
    Paralysis,
    Freeze,
    Petrify,

    // ---- 행동 종류 제한 ----
    Bind,
    Silence,

    // ---- 지시 불통 ----
    Fear,
    Confusion,

    // ---- 표적 조작 ----
    Taunt,
    Hidden,

    // ---- 방벽 ----
    Barrier,

    // ---- 회복 ----
    Regen,
    Curse
}

/// <summary>
/// 이름 하나의 설정. <b>기전 + 매개변수</b>입니다.
/// <para>
/// 매개변수는 기전마다 쓰이는 것이 다릅니다 — 예를 들어 <see cref="BlockChance"/>는
/// <see cref="EffectMechanism.Incapacitate"/>에서만 의미가 있습니다.
/// 한 표로 두는 편이 관리하기 쉬우므로 안 쓰는 칸은 기본값으로 둡니다.
/// </para>
/// </summary>
/// <param name="Name">이름.</param>
/// <param name="Mechanism">작동 방식.</param>
/// <param name="Korean">표시용 이름.</param>
/// <param name="Beneficial">
/// 이로운 효과인가. <b>해독·정화가 아군 강화까지 지우지 않게 하려면 필요합니다.</b>
/// </param>
/// <param name="Persists">
/// 전투가 끝나도 남는가.
/// <para><b>몸에 난 것은 남고, 상황이 만든 것은 상황이 끝나면 풀립니다.</b></para>
/// </param>
/// <param name="Target">수치 증감의 대상.</param>
/// <param name="Growth">지속 피해가 커지는 방식.</param>
/// <param name="BlocksRecovery">자연회복을 막는가.</param>
/// <param name="StacksScaleDamage">
/// 스택이 <b>피해</b>를 키우는가.
/// <para>
/// 스택이 쌓이는 것과 피해가 커지는 것은 다른 일입니다. 동상은 스택을 쌓지만
/// (<see cref="TransitionThreshold"/>에 쓰입니다) 피해는 커지지 않습니다 —
/// 그러지 않으면 동상이 "중독 + 둔화 + 전이"가 되어 중독과 축이 겹칩니다 (§18.4).
/// </para>
/// </param>
/// <param name="Companion">
/// 같이 걸리는 효과. 동상이 속도 저하를 동반하는 식입니다.
/// <para>이렇게 두면 "지속 피해 + 둔화" 같은 조합에 새 기전이 필요 없습니다.</para>
/// </param>
/// <param name="TransitionsTo">
/// 임계에 닿으면 전이할 상태. 동상이 쌓이면 빙결이 되는 식입니다.
/// <para>
/// ⚠️ <b>이것을 특정 효과의 특권으로 두지 않았습니다.</b> 매개변수로 두고 동상만
/// 값을 채웁니다. 그러면 "중독이 쌓이면 마비 아닌가"라는 요구가 와도 일관성이
/// 깨지지 않고, 필요해지면 값만 넣으면 됩니다.
/// </para>
/// </param>
/// <param name="TransitionThreshold">전이 임계 스택.</param>
/// <param name="BlockChance">행동이 막힐 확률. 1.0이면 완전 행동 불가.</param>
/// <param name="Restriction">막는 행동 종류.</param>
/// <param name="Cure">푸는 소모품.</param>
/// <param name="DefaultMagnitude">
/// 세기의 기본값. 거는 쪽이 지정하지 않으면 이 값을 씁니다.
/// <para>
/// ⚠️ <b>여기 있는 수치는 전부 임시값입니다.</b> 능력치 999 스케일 전환 뒤에
/// 배치 시뮬레이션으로 다시 재야 합니다. 감으로 고치지 말고 근거를
/// docs/08-balance-log.md에 남기세요.
/// </para>
/// </param>
public sealed record EffectProfile(
    EffectName Name,
    EffectMechanism Mechanism,
    string Korean,
    bool Beneficial = false,
    bool Persists = false,
    ShiftTarget Target = ShiftTarget.None,
    GrowthMode Growth = GrowthMode.None,
    bool BlocksRecovery = false,
    bool StacksScaleDamage = true,
    EffectName? Companion = null,
    EffectName? TransitionsTo = null,
    int TransitionThreshold = 0,
    double BlockChance = 0.0,
    ActionRestriction Restriction = ActionRestriction.None,
    CureItem Cure = CureItem.None,
    double DefaultMagnitude = 0.0);

/// <summary>
/// 한 캐릭터에게 걸려 있는 상태 효과 하나.
/// </summary>
/// <param name="Name">무엇인가.</param>
/// <param name="RemainingRounds">
/// 남은 라운드. <see cref="EffectProfile.Persists"/>가 참이면 이 값이 0이 되어도
/// 사라지지 않습니다 — 치료해야 풀립니다.
/// </param>
/// <param name="Magnitude">세기. 배율 계열은 0.25면 25% 변화.</param>
/// <param name="Stacks">쌓인 수. <see cref="GrowthMode.PerStack"/>과 임계 전이에 씁니다.</param>
/// <param name="SourceId">건 사람. 도발 대상 추적에 씁니다.</param>
public sealed record StatusEffect(
    EffectName Name,
    int RemainingRounds,
    double Magnitude,
    int Stacks = 1,
    string? SourceId = null)
{
    public EffectProfile Profile => StatusEffects.ProfileOf(Name);

    /// <summary>기전이 무엇인지.</summary>
    public EffectMechanism Mechanism => Profile.Mechanism;

    public StatusEffect Tick() => this with { RemainingRounds = RemainingRounds - 1 };

    /// <summary>
    /// 지속시간이 다 됐는가.
    /// <para><b>남는 효과(상처)는 지속시간으로 사라지지 않습니다.</b> 치료가 필요합니다.</para>
    /// </summary>
    public bool IsExpired => !Profile.Persists && RemainingRounds <= 0;

    /// <summary>행동할 때마다 커지는 효과(출혈)를 한 단계 키웁니다.</summary>
    public StatusEffect Grow() =>
        Profile.Growth == GrowthMode.PerAction
            ? this with { Stacks = Math.Min(Stacks + 1, StatusEffects.MaxStacks) }
            : this;

    /// <summary>다시 걸렸을 때 쌓거나 덮어씁니다.</summary>
    public StatusEffect Reapply(StatusEffect incoming) =>
        Profile.Growth == GrowthMode.PerStack
            ? incoming with { Stacks = Math.Min(Stacks + incoming.Stacks, StatusEffects.MaxStacks) }
            : incoming;

    /// <summary>임계에 닿아 다른 상태로 넘어가야 하는가.</summary>
    public bool ShouldTransition =>
        Profile.TransitionsTo is not null &&
        Profile.TransitionThreshold > 0 &&
        Stacks >= Profile.TransitionThreshold;

    public override string ToString() =>
        Stacks > 1 ? $"{Profile.Korean}×{Stacks}" : Profile.Korean;
}

/// <summary>
/// 상태 효과 목록과 조회.
/// <para>
/// ⚠️ <b>수치는 전부 임시값입니다.</b> 능력치 999 스케일 전환 뒤에 배치 시뮬레이션으로
/// 다시 잡아야 합니다. 근거: docs/07-decisions.md §18.9
/// </para>
/// </summary>
public static class StatusEffects
{
    /// <summary>
    /// 스택 상한. <b>없으면 긴 전투에서 그냥 죽습니다.</b>
    /// </summary>
    public const int MaxStacks = 5;

    /// <summary>지속 피해 1스택이 라운드마다 깎는 최대 HP 비율의 기준.</summary>
    public const double DamageOverTimeScale = 0.04;

    private static readonly EffectProfile[] Table =
    [
        // ---- 수치 증감 ----
        new(EffectName.PowerUp,      EffectMechanism.StatShift, "공격 강화", Beneficial: true,
            Target: ShiftTarget.Power,    DefaultMagnitude: 0.30),
        new(EffectName.PowerDown,    EffectMechanism.StatShift, "공격 약화",
            Target: ShiftTarget.Power,    DefaultMagnitude: 0.30),
        new(EffectName.GuardUp,      EffectMechanism.StatShift, "방어 강화", Beneficial: true,
            Target: ShiftTarget.Guard,    DefaultMagnitude: 0.30),
        new(EffectName.GuardDown,    EffectMechanism.StatShift, "방어 약화",
            Target: ShiftTarget.Guard,    DefaultMagnitude: 0.30),
        new(EffectName.AccuracyUp,   EffectMechanism.StatShift, "명중 강화", Beneficial: true,
            Target: ShiftTarget.Accuracy, DefaultMagnitude: 0.20),
        new(EffectName.AccuracyDown, EffectMechanism.StatShift, "명중 약화",
            Target: ShiftTarget.Accuracy, DefaultMagnitude: 0.20),
        new(EffectName.EvasionUp,    EffectMechanism.StatShift, "회피 강화", Beneficial: true,
            Target: ShiftTarget.Evasion,  DefaultMagnitude: 0.20),
        new(EffectName.EvasionDown,  EffectMechanism.StatShift, "회피 약화",
            Target: ShiftTarget.Evasion,  DefaultMagnitude: 0.20),
        new(EffectName.SpeedUp,      EffectMechanism.StatShift, "가속", Beneficial: true,
            Target: ShiftTarget.Speed,    DefaultMagnitude: 0.25),
        new(EffectName.SpeedDown,    EffectMechanism.StatShift, "둔화",
            Target: ShiftTarget.Speed,    DefaultMagnitude: 0.25),

        // ---- 지속 피해 — 넷 다 자연 해제가 없고, 커지는 조건으로 갈립니다 ----

        // 다시 걸릴 때 쌓입니다. 적이 반복해야 위험해집니다.
        new(EffectName.Poison, EffectMechanism.DamageOverTime, "중독", Persists: true,
            Growth: GrowthMode.PerStack, Cure: CureItem.Antidote, DefaultMagnitude: 1.0),

        // 안 커집니다. 대신 한 스택이 무겁습니다.
        new(EffectName.Burn, EffectMechanism.DamageOverTime, "화상", Persists: true,
            Growth: GrowthMode.None, Cure: CureItem.BurnSalve, DefaultMagnitude: 2.0),

        // 행동할 때마다 커집니다. 포지션 이동도 행동이라, 후열로 빼려면 피가 더 납니다.
        new(EffectName.Bleed, EffectMechanism.DamageOverTime, "출혈", Persists: true,
            Growth: GrowthMode.PerAction, Cure: CureItem.Bandage, DefaultMagnitude: 1.0),

        // 속도 저하를 동반하고, 쌓이면 빙결로 넘어갑니다.
        // 스택은 쌓이지만 피해에는 곱하지 않습니다 — 스택이 필요한 것은 임계 전이(빙결)
        // 때문입니다. 곱하면 동상이 "중독 + 둔화 + 전이"가 되어 중독과 축이 겹칩니다.
        // §18.4는 동상을 "안 커짐 + 느려짐"으로, 중독만 "다시 걸릴 때 커짐"으로 규정합니다.
        new(EffectName.Frostbite, EffectMechanism.DamageOverTime, "동상", Persists: true,
            Growth: GrowthMode.PerStack, StacksScaleDamage: false,
            Companion: EffectName.SpeedDown,
            TransitionsTo: EffectName.Freeze, TransitionThreshold: 4,
            Cure: CureItem.FrostSalve, DefaultMagnitude: 1.0),

        // ---- 확률적 행동 불가 — 확률만 다릅니다 ----

        // 지시를 듣는데 못 합니다. 지시 불통과 대비되는 답답함입니다.
        new(EffectName.Paralysis, EffectMechanism.Incapacitate, "마비", Persists: true,
            BlockChance: 0.30, Cure: CureItem.ParalysisCure),

        // 몇 라운드 뒤 자연 해제. 동상이 쌓여서 걸립니다.
        new(EffectName.Freeze, EffectMechanism.Incapacitate, "빙결",
            BlockChance: 1.0),

        // 치료할 때까지. 성수 또는 정화 스킬로 풀립니다.
        new(EffectName.Petrify, EffectMechanism.Incapacitate, "석화", Persists: true,
            BlockChance: 1.0, Cure: CureItem.HolyWater),

        // ---- 행동 종류 제한 ----
        new(EffectName.Bind, EffectMechanism.RestrictAction, "속박",
            Restriction: ActionRestriction.Movement),
        new(EffectName.Silence, EffectMechanism.RestrictAction, "침묵",
            Restriction: ActionRestriction.ManaSkills),

        // ---- 지시 불통 ----
        new(EffectName.Fear, EffectMechanism.LoseControl, "공포"),
        new(EffectName.Confusion, EffectMechanism.LoseControl, "혼란"),

        // ---- 표적 조작 ----
        new(EffectName.Taunt, EffectMechanism.TargetShift, "도발됨"),
        new(EffectName.Hidden, EffectMechanism.TargetShift, "은신", Beneficial: true),

        // ---- 방벽 ----
        new(EffectName.Barrier, EffectMechanism.Barrier, "보호막", Beneficial: true,
            DefaultMagnitude: 0.25),

        // ---- 회복 ----
        new(EffectName.Regen, EffectMechanism.Recovery, "재생", Beneficial: true,
            DefaultMagnitude: 0.06),
        new(EffectName.Curse, EffectMechanism.Recovery, "저주", Persists: true,
            BlocksRecovery: true, Cure: CureItem.HolyWater, DefaultMagnitude: 0.50)
    ];

    private static readonly Dictionary<EffectName, EffectProfile> ByName =
        Table.ToDictionary(p => p.Name);

    /// <summary>전체 목록. 표시·검증용입니다.</summary>
    public static IReadOnlyList<EffectProfile> Catalogue => Table;

    public static EffectProfile ProfileOf(EffectName name) => ByName[name];

    public static string ToKorean(EffectName name) => ByName[name].Korean;

    /// <summary>기본 세기로 효과 하나를 만듭니다.</summary>
    public static StatusEffect Create(EffectName name, int rounds, string? sourceId = null) =>
        new(name, rounds, ByName[name].DefaultMagnitude, 1, sourceId);
}
