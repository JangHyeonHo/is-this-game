namespace Guildwright.Core.Combat;

/// <summary>
/// 한 전투에서 이 캐릭터가 실제로 무엇을 얼마나 했는지.
/// <para>
/// <b>전투 기록이 그대로 성장 데이터가 됩니다.</b> 방패 들고 앞에서 두들겨 맞은 캐릭터와
/// 뒤에서 활만 쏜 캐릭터가 똑같이 자라면, 파티 편성과 전술 편성이 육성과 무관해집니다.
/// 이 기록이 있어야 <b>"어떻게 싸웠는가"가 "어떻게 자라는가"로 이어집니다.</b>
/// </para>
/// 근거: docs/04-game-design.md §5.7
/// </summary>
public sealed class CombatContribution
{
    public int PhysicalDamageDealt { get; private set; }
    public int MagicDamageDealt { get; private set; }
    public int PhysicalDamageTaken { get; private set; }
    public int MagicDamageTaken { get; private set; }
    public int HealingDone { get; private set; }

    /// <summary>회복·강화·약화·도발 등 지원 행동 횟수.</summary>
    public int SupportActions { get; private set; }

    /// <summary>포지션 이동 횟수. 기동을 활용했다는 신호입니다.</summary>
    public int Repositions { get; private set; }

    /// <summary>쓰러뜨린 적 수.</summary>
    public int Kills { get; private set; }

    /// <summary>터뜨린 치명타 수. 급소를 노리다 보면 손에 익습니다.</summary>
    public int CriticalHits { get; private set; }

    /// <summary>피한 공격 수. 계속 피하다 보면 몸이 반응합니다.</summary>
    public int Evasions { get; private set; }

    /// <summary>총 행동 횟수.</summary>
    public int Actions { get; private set; }

    public int TotalDamageDealt => PhysicalDamageDealt + MagicDamageDealt;
    public int TotalDamageTaken => PhysicalDamageTaken + MagicDamageTaken;

    internal void RecordDamageDealt(int amount, bool magic)
    {
        if (magic) MagicDamageDealt += amount;
        else PhysicalDamageDealt += amount;
    }

    internal void RecordDamageTaken(int amount, bool magic)
    {
        if (magic) MagicDamageTaken += amount;
        else PhysicalDamageTaken += amount;
    }

    internal void RecordHealing(int amount) => HealingDone += amount;
    internal void RecordSupport() => SupportActions++;
    internal void RecordReposition() => Repositions++;
    internal void RecordKill() => Kills++;
    internal void RecordAction() => Actions++;
    internal void RecordCritical() => CriticalHits++;
    internal void RecordEvasion() => Evasions++;

    /// <summary>여러 전투의 기록을 합칩니다.</summary>
    public static CombatContribution Merge(IEnumerable<CombatContribution> parts)
    {
        var merged = new CombatContribution();
        foreach (var part in parts)
        {
            merged.PhysicalDamageDealt += part.PhysicalDamageDealt;
            merged.MagicDamageDealt += part.MagicDamageDealt;
            merged.PhysicalDamageTaken += part.PhysicalDamageTaken;
            merged.MagicDamageTaken += part.MagicDamageTaken;
            merged.HealingDone += part.HealingDone;
            merged.SupportActions += part.SupportActions;
            merged.Repositions += part.Repositions;
            merged.Kills += part.Kills;
            merged.Actions += part.Actions;
            merged.CriticalHits += part.CriticalHits;
            merged.Evasions += part.Evasions;
        }
        return merged;
    }

    public override string ToString() =>
        $"가한 피해 물{PhysicalDamageDealt}/마{MagicDamageDealt} · " +
        $"받은 피해 물{PhysicalDamageTaken}/마{MagicDamageTaken} · " +
        $"회복 {HealingDone} · 지원 {SupportActions}회 · 이동 {Repositions}회 · 처치 {Kills}";
}
