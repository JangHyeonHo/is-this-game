namespace Guildwright.Core.Adventurers;

/// <summary>
/// 원천 능력치. <b>훈련으로 직접 올리는 것은 이 여섯 개뿐입니다.</b>
/// <para>
/// 전투용 수치(물리 위력·최대 HP 등)를 직접 훈련하지 않는 이유는,
/// 하나를 올렸을 때 여러 곳이 움직여야 육성 선택이 무거워지기 때문입니다.
/// 그리고 전투 밖의 것(운반·함정 해제·감정)과 연결할 고리가 필요합니다.
/// </para>
/// 근거: docs/01-game-design.md §3.3
/// </summary>
public enum PrimaryStat
{
    /// <summary>힘. 물리 위력과 운반.</summary>
    Strength,
    /// <summary>민첩. 행동 속도와 회피.</summary>
    Agility,
    /// <summary>기교. 치명타, 함정 해제, 채집.</summary>
    Finesse,
    /// <summary>활력. 최대 HP와 물리 방어, 부상 저항.</summary>
    Vitality,
    /// <summary>지능. 마법 위력, 감정, 함정 감지.</summary>
    Intellect,
    /// <summary>정신. 최대 마나와 마법 방어.</summary>
    Spirit
}

/// <summary>
/// 원천 능력치 묶음. 불변이며 연산은 새 인스턴스를 반환합니다.
/// </summary>
public readonly record struct PrimaryStats(
    int Strength,
    int Agility,
    int Finesse,
    int Vitality,
    int Intellect,
    int Spirit)
{
    public static readonly PrimaryStats Zero = new(0, 0, 0, 0, 0, 0);

    /// <summary>모든 능력치를 같은 값으로 채웁니다.</summary>
    public static PrimaryStats Uniform(int value) => new(value, value, value, value, value, value);

    public int this[PrimaryStat stat] => stat switch
    {
        PrimaryStat.Strength => Strength,
        PrimaryStat.Agility => Agility,
        PrimaryStat.Finesse => Finesse,
        PrimaryStat.Vitality => Vitality,
        PrimaryStat.Intellect => Intellect,
        PrimaryStat.Spirit => Spirit,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    public PrimaryStats With(PrimaryStat stat, int value) => stat switch
    {
        PrimaryStat.Strength => this with { Strength = value },
        PrimaryStat.Agility => this with { Agility = value },
        PrimaryStat.Finesse => this with { Finesse = value },
        PrimaryStat.Vitality => this with { Vitality = value },
        PrimaryStat.Intellect => this with { Intellect = value },
        PrimaryStat.Spirit => this with { Spirit = value },
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    /// <summary>모든 능력치의 합. 캐릭터의 대략적인 그릇을 나타냅니다.</summary>
    public int Total => Strength + Agility + Finesse + Vitality + Intellect + Spirit;

    public static PrimaryStats operator +(PrimaryStats a, PrimaryStats b) => new(
        a.Strength + b.Strength,
        a.Agility + b.Agility,
        a.Finesse + b.Finesse,
        a.Vitality + b.Vitality,
        a.Intellect + b.Intellect,
        a.Spirit + b.Spirit);

    public static PrimaryStats operator -(PrimaryStats a, PrimaryStats b) => new(
        a.Strength - b.Strength,
        a.Agility - b.Agility,
        a.Finesse - b.Finesse,
        a.Vitality - b.Vitality,
        a.Intellect - b.Intellect,
        a.Spirit - b.Spirit);

    /// <summary>각 능력치를 0 이상으로 자릅니다.</summary>
    public PrimaryStats ClampToZero() => new(
        Math.Max(0, Strength),
        Math.Max(0, Agility),
        Math.Max(0, Finesse),
        Math.Max(0, Vitality),
        Math.Max(0, Intellect),
        Math.Max(0, Spirit));

    public static IReadOnlyList<PrimaryStat> AllStats { get; } = Enum.GetValues<PrimaryStat>();

    public override string ToString() =>
        $"힘{Strength} 민{Agility} 기{Finesse} 활{Vitality} 지{Intellect} 정{Spirit}";
}

public static class PrimaryStatNames
{
    public static string ToKorean(this PrimaryStat stat) => stat switch
    {
        PrimaryStat.Strength => "힘",
        PrimaryStat.Agility => "민첩",
        PrimaryStat.Finesse => "기교",
        PrimaryStat.Vitality => "활력",
        PrimaryStat.Intellect => "지능",
        PrimaryStat.Spirit => "정신",
        _ => "?"
    };
}
