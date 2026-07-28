using Guildwright.Core.Adventurers;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

public enum Team
{
    Player,
    Enemy
}

/// <summary>
/// 전투에 참여하는 한 명. 전투 중에만 존재하며, 영속 데이터(모험가 본체)와 분리되어 있습니다.
/// </summary>
public sealed class Combatant
{
    private readonly List<StatusEffect> _effects = [];

    public Combatant(
        string id,
        string name,
        Team team,
        StatBlock stats,
        int judgement,
        WeaponStyle style,
        double weaponEffectiveness,
        Row row,
        IReadOnlyList<TacticRule> tactics,
        int potions = 2)
    {
        Id = id;
        Name = name;
        Team = team;
        Stats = stats;
        Judgement = Math.Clamp(judgement, 0, 100);
        Style = style;
        WeaponEffectiveness = weaponEffectiveness;
        Row = row;
        Tactics = tactics;
        Potions = potions;

        MaxHp = Math.Max(1, stats.Vitality * 3);
        Hp = MaxHp;
        MaxMana = Math.Max(0, stats.Mana);
        Mana = MaxMana;
    }

    public string Id { get; }
    public string Name { get; }
    public Team Team { get; }
    public StatBlock Stats { get; }

    /// <summary>
    /// 판단력. 전투 중 AI 결정 품질을 좌우합니다.
    /// <para>포지션 판단에도 쓰입니다 — 물러설 때를 아는 것이 곧 판단력입니다.</para>
    /// </summary>
    public int Judgement { get; }

    public WeaponStyle Style { get; }

    /// <summary>무기 숙련도에서 오는 전투 효율 배율.</summary>
    public double WeaponEffectiveness { get; }

    public StyleCapability Capability => WeaponStyles.CapabilityOf(Style);

    /// <summary>
    /// 현재 위치. <b>전투 중에 바뀝니다.</b>
    /// <para>
    /// 전열/후열은 고정된 역할이 아니라 매 순간의 선택입니다.
    /// 다친 전사를 뒤로 물릴 것인가, 각오하고 남길 것인가.
    /// </para>
    /// </summary>
    public Row Row { get; private set; }

    public int MaxHp { get; }
    public int Hp { get; private set; }
    public int MaxMana { get; }
    public int Mana { get; private set; }
    public int Potions { get; private set; }

    public IReadOnlyList<TacticRule> Tactics { get; }
    public IReadOnlyList<StatusEffect> Effects => _effects;

    /// <summary>
    /// 이 전투에서 실제로 무엇을 했는지. <b>전투가 끝나면 성장 데이터가 됩니다.</b>
    /// </summary>
    public CombatContribution Contribution { get; } = new();

    public bool IsDefending { get; private set; }
    public bool IsAlive => Hp > 0;
    public double HpRatio => (double)Hp / MaxHp;

    // ---- 상태 효과가 반영된 실효 능력치 ----

    public int EffectiveAttack => ApplyModifiers(Stats.Attack, StatusEffectKind.Empowered, StatusEffectKind.Weakened);
    public int EffectiveMagicAttack => ApplyModifiers(Stats.MagicAttack, StatusEffectKind.Empowered, StatusEffectKind.Weakened);
    public int EffectiveDefense => ApplyModifiers(Stats.Defense, StatusEffectKind.Warded, StatusEffectKind.Sundered);
    public int EffectiveMagicDefense => ApplyModifiers(Stats.MagicDefense, StatusEffectKind.Warded, StatusEffectKind.Sundered);

    /// <summary>행동 순서에 쓰이는 실효 속도. 무기 무게와 둔화가 반영됩니다.</summary>
    public double EffectiveSpeed
    {
        get
        {
            double speed = Stats.Speed * Capability.SpeedModifier;
            foreach (var effect in _effects)
            {
                if (effect.Kind == StatusEffectKind.Slowed) speed *= 1.0 - effect.Magnitude;
            }
            return speed;
        }
    }

    private int ApplyModifiers(int baseValue, StatusEffectKind up, StatusEffectKind down)
    {
        double value = baseValue;
        foreach (var effect in _effects)
        {
            if (effect.Kind == up) value *= 1.0 + effect.Magnitude;
            else if (effect.Kind == down) value *= 1.0 - effect.Magnitude;
        }
        return Math.Max(1, (int)Math.Round(value));
    }

    /// <summary>도발당한 대상의 Id. 없으면 null.</summary>
    public string? TauntedBy =>
        _effects.FirstOrDefault(e => e.Kind == StatusEffectKind.Taunted)?.SourceId;

    public bool HasEffect(StatusEffectKind kind) => _effects.Any(e => e.Kind == kind);

    // ---- 상태 변경 ----

    public void TakeDamage(int amount) => Hp = Math.Max(0, Hp - amount);

    /// <summary>피해를 입고 그 사실을 기록합니다. 어떤 종류로 맞았는지가 성장에 영향을 줍니다.</summary>
    internal void TakeDamage(int amount, bool magic)
    {
        TakeDamage(amount);
        Contribution.RecordDamageTaken(amount, magic);
    }

    public void Heal(int amount) => Hp = Math.Min(MaxHp, Hp + amount);

    public void SpendMana(int amount) => Mana = Math.Max(0, Mana - amount);

    public void ConsumePotion() => Potions = Math.Max(0, Potions - 1);

    public void BeginDefending() => IsDefending = true;

    public void ClearDefending() => IsDefending = false;

    public void MoveTo(Row row) => Row = row;

    /// <summary>같은 종류의 효과는 덮어씁니다. 중첩을 허용하면 조합 폭발이 일어납니다.</summary>
    public void ApplyEffect(StatusEffect effect)
    {
        _effects.RemoveAll(e => e.Kind == effect.Kind);
        _effects.Add(effect);
    }

    /// <summary>라운드 종료 시 지속시간을 깎고 만료된 것을 제거합니다.</summary>
    public void TickEffects()
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var ticked = _effects[i].Tick();
            if (ticked.IsExpired) _effects.RemoveAt(i);
            else _effects[i] = ticked;
        }
    }

    public override string ToString() =>
        $"{Name}({Style.ToKorean()}, {(Row == Row.Front ? "전열" : "후열")}) HP {Hp}/{MaxHp}";
}
