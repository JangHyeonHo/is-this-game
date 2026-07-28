namespace Guildwright.Core.Combat;

public enum Team
{
    Player,
    Enemy
}

/// <summary>
/// 전투에 참여하는 한 명. 전투 중에만 존재하는 상태이며,
/// 영속 데이터(모험가 본체)와는 분리되어 있습니다.
/// </summary>
public sealed class Combatant
{
    public Combatant(
        string id,
        string name,
        Team team,
        int maxHp,
        int attack,
        int defense,
        int agility,
        int judgement,
        int potions,
        IReadOnlyList<TacticRule> tactics)
    {
        Id = id;
        Name = name;
        Team = team;
        MaxHp = maxHp;
        Hp = maxHp;
        Attack = attack;
        Defense = defense;
        Agility = agility;
        Judgement = Math.Clamp(judgement, 0, 100);
        Potions = potions;
        Tactics = tactics;
    }

    public string Id { get; }
    public string Name { get; }
    public Team Team { get; }

    public int MaxHp { get; }
    public int Hp { get; private set; }
    public int Attack { get; }
    public int Defense { get; }
    public int Agility { get; }

    /// <summary>
    /// 판단력 (0~100). 이 게임의 중심 스탯.
    /// <para>
    /// 전투 중 AI가 얼마나 좋은 선택을 하는지를 결정합니다.
    /// 낮으면 편성된 전술 규칙을 자주 무시하고, 행동 평가에 노이즈가 크게 낍니다.
    /// 육성의 결과가 전투에서 직접 드러나는 통로입니다.
    /// </para>
    /// 근거: docs/04-game-design.md §4.2
    /// </summary>
    public int Judgement { get; }

    /// <summary>남은 회복약 수.</summary>
    public int Potions { get; private set; }

    /// <summary>전술 규칙. 위에서부터 순서대로 평가합니다.</summary>
    public IReadOnlyList<TacticRule> Tactics { get; }

    /// <summary>이번 턴에 방어 태세인지. 턴 시작 시 해제됩니다.</summary>
    public bool IsDefending { get; private set; }

    public bool IsAlive => Hp > 0;
    public double HpRatio => (double)Hp / MaxHp;

    public void TakeDamage(int amount) => Hp = Math.Max(0, Hp - amount);

    public void Heal(int amount) => Hp = Math.Min(MaxHp, Hp + amount);

    public void ConsumePotion() => Potions = Math.Max(0, Potions - 1);

    public void BeginDefending() => IsDefending = true;

    public void ClearDefending() => IsDefending = false;
}
