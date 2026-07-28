namespace Guildwright.Core.Adventurers;

/// <summary>모험가의 7가지 능력치.</summary>
public enum StatKind
{
    /// <summary>체력. 최대 HP를 결정합니다.</summary>
    Vitality,
    /// <summary>마력. 마법 사용 자원을 결정합니다.</summary>
    Mana,
    /// <summary>물리 공격력.</summary>
    Attack,
    /// <summary>물리 방어력.</summary>
    Defense,
    /// <summary>마법 공격력.</summary>
    MagicAttack,
    /// <summary>마법 방어력.</summary>
    MagicDefense,
    /// <summary>속도. 행동 순서를 결정합니다.</summary>
    Speed
}

/// <summary>
/// 7 능력치 묶음. 불변이며 연산은 새 인스턴스를 반환합니다.
/// </summary>
public readonly record struct StatBlock(
    int Vitality,
    int Mana,
    int Attack,
    int Defense,
    int MagicAttack,
    int MagicDefense,
    int Speed)
{
    public static readonly StatBlock Zero = new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>모든 능력치를 같은 값으로 채웁니다.</summary>
    public static StatBlock Uniform(int value) => new(value, value, value, value, value, value, value);

    public int this[StatKind kind] => kind switch
    {
        StatKind.Vitality => Vitality,
        StatKind.Mana => Mana,
        StatKind.Attack => Attack,
        StatKind.Defense => Defense,
        StatKind.MagicAttack => MagicAttack,
        StatKind.MagicDefense => MagicDefense,
        StatKind.Speed => Speed,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public StatBlock With(StatKind kind, int value) => kind switch
    {
        StatKind.Vitality => this with { Vitality = value },
        StatKind.Mana => this with { Mana = value },
        StatKind.Attack => this with { Attack = value },
        StatKind.Defense => this with { Defense = value },
        StatKind.MagicAttack => this with { MagicAttack = value },
        StatKind.MagicDefense => this with { MagicDefense = value },
        StatKind.Speed => this with { Speed = value },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>모든 능력치의 합. 캐릭터의 대략적인 강함을 나타냅니다.</summary>
    public int Total => Vitality + Mana + Attack + Defense + MagicAttack + MagicDefense + Speed;

    public static StatBlock operator +(StatBlock a, StatBlock b) => new(
        a.Vitality + b.Vitality,
        a.Mana + b.Mana,
        a.Attack + b.Attack,
        a.Defense + b.Defense,
        a.MagicAttack + b.MagicAttack,
        a.MagicDefense + b.MagicDefense,
        a.Speed + b.Speed);

    public static StatBlock operator -(StatBlock a, StatBlock b) => new(
        a.Vitality - b.Vitality,
        a.Mana - b.Mana,
        a.Attack - b.Attack,
        a.Defense - b.Defense,
        a.MagicAttack - b.MagicAttack,
        a.MagicDefense - b.MagicDefense,
        a.Speed - b.Speed);

    /// <summary>각 능력치를 0 이상으로 자릅니다.</summary>
    public StatBlock ClampToZero() => new(
        Math.Max(0, Vitality),
        Math.Max(0, Mana),
        Math.Max(0, Attack),
        Math.Max(0, Defense),
        Math.Max(0, MagicAttack),
        Math.Max(0, MagicDefense),
        Math.Max(0, Speed));

    public static IReadOnlyList<StatKind> AllKinds { get; } = Enum.GetValues<StatKind>();

    public override string ToString() =>
        $"체{Vitality} 마{Mana} 공{Attack} 방{Defense} 마공{MagicAttack} 마방{MagicDefense} 속{Speed}";
}
