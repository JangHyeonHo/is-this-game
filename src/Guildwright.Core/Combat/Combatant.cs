using Guildwright.Core.Adventurers;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Combat;

public enum Team
{
    Player,
    Enemy
}

/// <summary>
/// 전투에 참여하는 한 명. 전투 중에만 존재하며, 영속 데이터(모험가 본체)와 분리되어 있습니다.
/// <para>
/// 원천 능력치와 파생 보정을 받아, 전투에 실제로 쓰이는 수치를 스스로 계산합니다.
/// </para>
/// </summary>
public sealed class Combatant
{
    private readonly List<StatusEffect> _effects = [];

    public Combatant(
        string id,
        string name,
        Team team,
        PrimaryStats stats,
        int judgement,
        Loadout loadout,
        double weaponEffectiveness,
        Row row,
        IReadOnlyList<TacticRule> tactics,
        int potions = 2,
        DerivedBonuses? bonuses = null,
        IReadOnlyList<SkillId>? passives = null,
        IReadOnlyList<SkillId>? actives = null,
        int? startingMana = null)
    {
        Id = id;
        Name = name;
        Team = team;
        Stats = stats;
        Bonuses = bonuses;
        Judgement = Math.Clamp(judgement, 0, 100);
        Loadout = loadout;
        WeaponEffectiveness = weaponEffectiveness;
        Passives = passives ?? [];
        Actives = actives ?? [];
        Row = row;
        Tactics = tactics;
        Potions = potions;

        MaxHp = DerivedStats.MaxHp(stats, bonuses)
            + (int)Math.Round(PassiveBonusOf(passives, DerivedStat.MaxHp));
        Hp = MaxHp;
        MaxMana = DerivedStats.MaxMana(stats, bonuses);

        // 마나는 파견 단위 자원입니다 — 전투마다 채워지면 사실상 무한이 됩니다 (§17.5b).
        Mana = startingMana is { } m ? Math.Clamp(m, 0, MaxMana) : MaxMana;

        BasePhysicalPower = DerivedStats.PhysicalPower(stats, bonuses);
        BasePhysicalGuard = DerivedStats.PhysicalGuard(stats, bonuses);
        BaseMagicPower = DerivedStats.MagicPower(stats, bonuses);
        BaseMagicGuard = DerivedStats.MagicGuard(stats, bonuses);
        BaseActionSpeed = DerivedStats.ActionSpeed(stats, bonuses);
        BaseCritChance = DerivedStats.CritChance(stats, bonuses);
        BaseEvasionChance = DerivedStats.EvasionChance(stats, bonuses);
    }

    public string Id { get; }
    public string Name { get; }
    public Team Team { get; }

    /// <summary>원천 능력치. 전투 계산은 아래 파생 수치를 씁니다.</summary>
    public PrimaryStats Stats { get; }

    public DerivedBonuses? Bonuses { get; }

    /// <summary>
    /// 판단력. 전투 중 AI 결정 품질을 좌우합니다.
    /// <para>포지션 판단에도 쓰입니다 — 물러설 때를 아는 것이 곧 판단력입니다.</para>
    /// </summary>
    public int Judgement { get; }

    /// <summary>장착 4칸. <b>손에 무엇을 들었는지가 곧 스타일입니다.</b></summary>
    public Loadout Loadout { get; }

    /// <summary>
    /// 가진 패시브. <b>슬롯 없이 전부 적용됩니다.</b>
    /// <para>파티 오라(태생 패시브)는 파티를 조립할 때 여기에 합쳐서 넣습니다.</para>
    /// </summary>
    public IReadOnlyList<SkillId> Passives { get; }

    /// <summary>장착한 액티브. 슬롯 수는 직업이 정합니다.</summary>
    public IReadOnlyList<SkillId> Actives { get; }

    /// <summary>무기 숙련도에서 오는 전투 효율 배율.</summary>
    public double WeaponEffectiveness { get; }

    // ---- 무기가 정하는 것 — 위력 · 속도 · 사거리. 그게 전부입니다 ----

    public bool CanStrikeBackRow => Loadout.CanStrikeBackRow;
    public bool CanActFromBackRow => Loadout.CanActFromBackRow;
    public bool UsesMagicPower => Loadout.UsesMagicPower;

    // ---- 스킬이 정하는 것 — 무기가 아니라 여기입니다 ----

    /// <summary>그 행동을 여는 액티브를 장착하고 있고, 요구 무기도 들고 있는가.</summary>
    public bool CanDo(TacticAction action) => SkillFor(action) is not null;

    /// <summary>
    /// 그 행동을 지금 쓸 수 있는가 — <b>스킬 보유 + 무기 + 쿨다운 + 마나</b>를 모두 봅니다.
    /// <para>
    /// 마나 소모량은 <b>스킬마다 다릅니다</b> (§10.0b). 예전에는 어디서나
    /// <c>DamageModel.ManaPerSpell</c> 고정값을 써서 <see cref="Skill.ManaCost"/>가
    /// 읽히지 않는 죽은 데이터였고, 물리 기술은 싼 것이 아니라 <b>공짜</b>였습니다.
    /// </para>
    /// </summary>
    public bool CanAfford(TacticAction action)
    {
        var skill = SkillFor(action);
        return skill is not null && Mana >= skill.ManaCost;
    }

    /// <summary>그 행동의 마나 비용. 스킬이 없으면 0입니다.</summary>
    public int ManaCostOf(TacticAction action) => SkillFor(action)?.ManaCost ?? 0;

    /// <summary>
    /// 그 행동을 실제로 씁니다 — <b>마나를 내고 쿨다운을 시작합니다.</b>
    /// <para>
    /// 이걸 부르지 않으면 <see cref="Skill.Cooldown"/>이 죽은 데이터가 됩니다. 실제로
    /// <c>StartCooldown</c>·<c>TickCooldowns</c>가 한 번도 호출되지 않던 기간이 있었습니다.
    /// </para>
    /// </summary>
    internal void PaySkillCost(TacticAction action)
    {
        var skill = SkillFor(action);
        if (skill is null) return;

        if (skill.ManaCost > 0) SpendMana(skill.ManaCost);
        if (skill.Cooldown > 0) StartCooldown(skill.Id, skill.Cooldown);
    }

    /// <summary>
    /// 그 행동을 여는 스킬. 없으면 null.
    /// <para>순회 순서는 <see cref="Actives"/>의 순서이므로 결정적입니다.</para>
    /// </summary>
    public Skill? SkillFor(TacticAction action)
    {
        foreach (var id in Actives)
        {
            var skill = SkillBook.Of(id);
            if (skill.Action == action && skill.UsableWith(Loadout) && !OnCooldown(id)) return skill;
        }
        return null;
    }

    /// <summary>패시브가 그 파생 수치에 더하는 양.</summary>
    private double PassiveBonus(DerivedStat stat)
    {
        double sum = 0.0;
        foreach (var id in Passives)
        {
            var skill = SkillBook.Of(id);
            if (skill.Boosts == stat) sum += skill.BoostAmount;
            if (skill.Costs == stat) sum -= skill.CostAmount;
        }
        return sum;
    }

    /// <summary>치명타 배율. 숙련 패시브가 좌우합니다 — 무기가 아닙니다.</summary>
    public double CritMultiplier
    {
        get
        {
            double bonus = 0.0;
            foreach (var id in Passives) bonus += SkillBook.Of(id).CritMultiplierBonus;
            return DamageModel.BaseCritMultiplier + bonus;
        }
    }

    // ---- 쿨다운 ----

    private readonly Dictionary<SkillId, int> _cooldowns = [];

    public bool OnCooldown(SkillId id) => _cooldowns.TryGetValue(id, out int left) && left > 0;

    internal void StartCooldown(SkillId id, int rounds)
    {
        if (rounds > 0) _cooldowns[id] = rounds;
    }

    internal void TickCooldowns()
    {
        // 키 목록을 먼저 고정합니다 — 순회 중 수정을 피하고 순서를 정합니다.
        foreach (var id in _cooldowns.Keys.OrderBy(k => k).ToList())
        {
            if (_cooldowns[id] > 0) _cooldowns[id]--;
        }
    }

    /// <summary>
    /// 현재 위치. <b>전투 중에 바뀝니다.</b>
    /// <para>전열/후열은 고정된 역할이 아니라 매 순간의 선택입니다.</para>
    /// </summary>
    public Row Row { get; private set; }

    public int MaxHp { get; }
    public int Hp { get; private set; }
    public int MaxMana { get; }
    public int Mana { get; private set; }
    public int Potions { get; private set; }

    // ---- 파생 기본값 (상태 효과 적용 전) ----

    public int BasePhysicalPower { get; }
    public int BasePhysicalGuard { get; }
    public int BaseMagicPower { get; }
    public int BaseMagicGuard { get; }
    public double BaseActionSpeed { get; }
    public double BaseCritChance { get; }
    public double BaseEvasionChance { get; }

    public IReadOnlyList<TacticRule> Tactics { get; }
    public IReadOnlyList<StatusEffect> Effects => _effects;

    /// <summary>이 전투에서 실제로 무엇을 했는지. 전투가 끝나면 성장 데이터가 됩니다.</summary>
    public CombatContribution Contribution { get; } = new();

    public bool IsDefending { get; private set; }
    public bool IsAlive => Hp > 0;
    public double HpRatio => (double)Hp / MaxHp;

    // ---- 상태 효과가 반영된 실효 수치 ----

    public int EffectivePhysicalPower =>
        Shifted(BasePhysicalPower + PassiveBonus(DerivedStat.PhysicalPower), ShiftTarget.Power);

    public int EffectiveMagicPower =>
        Shifted(BaseMagicPower + PassiveBonus(DerivedStat.MagicPower), ShiftTarget.Power);

    public int EffectivePhysicalGuard =>
        Shifted(BasePhysicalGuard + PassiveBonus(DerivedStat.PhysicalGuard), ShiftTarget.Guard);

    public int EffectiveMagicGuard =>
        Shifted(BaseMagicGuard + PassiveBonus(DerivedStat.MagicGuard), ShiftTarget.Guard);

    /// <summary>공격 위력. 지팡이를 들면 마법 위력을 씁니다.</summary>
    public int EffectiveOffense =>
        UsesMagicPower ? EffectiveMagicPower : EffectivePhysicalPower;

    /// <summary>치명타 확률. <b>숙련 패시브가 좌우합니다</b> — 무기가 아닙니다.</summary>
    public double CritChance =>
        Math.Clamp(BaseCritChance + PassiveBonus(DerivedStat.CritChance), 0.0, 0.45);

    /// <summary>회피 확률.</summary>
    public double EvasionChance =>
        Math.Max(0.0, BaseEvasionChance + PassiveBonus(DerivedStat.EvasionChance))
        * ShiftFactor(ShiftTarget.Evasion);

    /// <summary>명중 보정 배율. 상태 효과만 반영합니다.</summary>
    public double AccuracyFactor => ShiftFactor(ShiftTarget.Accuracy);

    /// <summary>행동 순서에 쓰이는 실효 속도. 무기 무게와 상태 효과가 반영됩니다.</summary>
    public double EffectiveSpeed =>
        Math.Max(0.1, BaseActionSpeed + PassiveBonus(DerivedStat.ActionSpeed))
        * Loadout.Speed * ShiftFactor(ShiftTarget.Speed);

    /// <summary>
    /// 그 수치에 걸린 증감을 모아 배율로 만듭니다.
    /// <para>
    /// <b>덧셈으로 모아 마지막에 한 번 곱합니다.</b> 곱셈을 누적하면 적용 순서에 따라
    /// 부동소수점 끝자리가 달라져 배치 시뮬레이션 재현성이 깨집니다.
    /// </para>
    /// </summary>
    private double ShiftFactor(ShiftTarget target)
    {
        double sum = 0.0;

        foreach (var effect in _effects)
        {
            var profile = effect.Profile;
            if (profile.Mechanism != EffectMechanism.StatShift || profile.Target != target) continue;

            sum += profile.Beneficial ? effect.Magnitude : -effect.Magnitude;
        }

        // 배율이 0 이하로 떨어지면 수치가 사라지므로 하한을 둡니다.
        return Math.Max(0.1, 1.0 + sum);
    }

    private int Shifted(double baseValue, ShiftTarget target) =>
        Math.Max(1, (int)Math.Round(baseValue * ShiftFactor(target)));

    /// <summary>도발당한 대상의 Id. 없으면 null.</summary>
    public string? TauntedBy =>
        _effects.FirstOrDefault(e => e.Name == EffectName.Taunt)?.SourceId;

    public bool HasEffect(EffectName name) => _effects.Any(e => e.Name == name);

    /// <summary>그 기전의 효과가 걸려 있는가.</summary>
    public bool HasMechanism(EffectMechanism mechanism) =>
        _effects.Any(e => e.Mechanism == mechanism);

    /// <summary>
    /// <b>플레이어의 지시가 통하는가.</b> 공포나 혼란에 걸려 있으면 통하지 않습니다.
    /// <para>
    /// 지휘에 횟수 제한이 없는 대신 이것이 유일한 제약입니다.
    /// 근거: docs/08-design-revision.md §14, §18.7
    /// </para>
    /// </summary>
    public bool AcceptsOrders => !HasMechanism(EffectMechanism.LoseControl);

    /// <summary>그 종류의 행동이 막혀 있는가.</summary>
    public bool IsRestricted(ActionRestriction restriction) =>
        _effects.Any(e => e.Profile.Restriction == restriction);

    /// <summary>
    /// 행동이 막힐 확률. 마비는 0.3쯤, 빙결·석화는 1.0입니다.
    /// <para>여러 개가 걸려 있으면 가장 높은 것을 씁니다 — 곱하면 순서에 의존합니다.</para>
    /// </summary>
    public double IncapacitateChance
    {
        get
        {
            double worst = 0.0;
            foreach (var effect in _effects)
            {
                if (effect.Mechanism != EffectMechanism.Incapacitate) continue;
                worst = Math.Max(worst, effect.Profile.BlockChance);
            }
            return worst;
        }
    }

    /// <summary>자연회복이 막혀 있는가 (저주).</summary>
    public bool RecoveryBlocked => _effects.Any(e => e.Profile.BlocksRecovery);

    /// <summary>표적이 되지 않는가 (은신).</summary>
    public bool IsHidden => HasEffect(EffectName.Hidden);

    // ---- 상태 변경 ----

    /// <summary>
    /// 피해를 입습니다. <b>보호막이 있으면 먼저 깎입니다.</b>
    /// <para>회복과 달리 남은 보호막은 사라지므로, 미리 걸어두는 판단이 됩니다.</para>
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (Barrier > 0)
        {
            int absorbed = Math.Min(Barrier, amount);
            Barrier -= absorbed;
            amount -= absorbed;
            if (Barrier == 0) _effects.RemoveAll(e => e.Mechanism == EffectMechanism.Barrier);
        }

        Hp = Math.Max(0, Hp - amount);
    }

    /// <summary>남은 보호막. HP 위에 얹혀 있습니다.</summary>
    public int Barrier { get; private set; }

    /// <summary>생성자에서 쓰기 위한 정적 판본 — 최대 HP를 정할 때는 인스턴스가 아직 없습니다.</summary>
    private static double PassiveBonusOf(IReadOnlyList<SkillId>? passives, DerivedStat stat)
    {
        if (passives is null) return 0.0;

        double sum = 0.0;
        foreach (var id in passives)
        {
            var skill = SkillBook.Of(id);
            if (skill.Boosts == stat) sum += skill.BoostAmount;
            if (skill.Costs == stat) sum -= skill.CostAmount;
        }
        return sum;
    }

    /// <summary>피해를 입고 그 사실을 기록합니다. 어떤 종류로 맞았는지가 성장에 영향을 줍니다.</summary>
    internal void TakeDamage(int amount, bool magic)
    {
        TakeDamage(amount);
        Contribution.RecordDamageTaken(amount, magic);
    }

    /// <summary>회복합니다. <b>저주가 걸려 있으면 그만큼 덜 회복됩니다.</b></summary>
    public void Heal(int amount)
    {
        double kept = 1.0;
        foreach (var effect in _effects)
        {
            if (effect.Profile.BlocksRecovery) kept = Math.Min(kept, 1.0 - effect.Magnitude);
        }

        Hp = Math.Min(MaxHp, Hp + Math.Max(0, (int)Math.Round(amount * kept)));
    }

    public void SpendMana(int amount) => Mana = Math.Max(0, Mana - amount);

    public void ConsumePotion() => Potions = Math.Max(0, Potions - 1);

    public void BeginDefending() => IsDefending = true;

    public void ClearDefending() => IsDefending = false;

    public void MoveTo(Row row) => Row = row;

    /// <summary>
    /// 효과를 겁니다.
    /// <para>
    /// 같은 이름이 이미 걸려 있으면 <see cref="GrowthMode.PerStack"/>이면 쌓고,
    /// 아니면 덮어씁니다. <b>덮어쓰기가 기본입니다</b> — 무엇이든 쌓이게 두면
    /// 긴 전투에서 감당이 안 됩니다.
    /// </para>
    /// <para>동반 효과(동상의 둔화 등)도 같이 걸립니다.</para>
    /// </summary>
    public void ApplyEffect(StatusEffect effect)
    {
        int existing = _effects.FindIndex(e => e.Name == effect.Name);

        if (existing >= 0) _effects[existing] = _effects[existing].Reapply(effect);
        else _effects.Add(effect);

        if (effect.Profile.Companion is { } companion && !HasEffect(companion))
        {
            _effects.Add(StatusEffects.Create(companion, effect.RemainingRounds, effect.SourceId));
        }

        // 보호막은 흡수량을 따로 들고 있어야 해서 여기서 채웁니다.
        if (effect.Mechanism == EffectMechanism.Barrier)
        {
            Barrier = Math.Max(Barrier, (int)Math.Round(MaxHp * effect.Magnitude));
        }
    }

    /// <summary>행동할 때마다 커지는 효과(출혈)를 키웁니다.</summary>
    internal void GrowOnAction()
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            _effects[i] = _effects[i].Grow();
        }
    }

    /// <summary>
    /// 임계에 닿아 전이해야 하는 효과를 처리합니다. 전이한 이름을 돌려줍니다.
    /// <para>동상이 쌓이면 빙결이 되고, 원래 것은 사라집니다 — <b>파국이자 리셋</b>입니다.</para>
    /// </summary>
    internal EffectName? ResolveTransition()
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (!_effects[i].ShouldTransition) continue;

            var to = _effects[i].Profile.TransitionsTo!.Value;
            var companion = _effects[i].Profile.Companion;

            _effects.RemoveAt(i);
            if (companion is { } c) _effects.RemoveAll(e => e.Name == c);
            _effects.Add(StatusEffects.Create(to, TransitionRounds));

            return to;
        }

        return null;
    }

    /// <summary>전이해서 걸리는 상태의 지속 라운드.</summary>
    private const int TransitionRounds = 2;

    /// <summary>
    /// 라운드 종료 시 지속시간을 깎고 만료된 것을 제거합니다.
    /// <para><b>남는 효과(상처)는 여기서 사라지지 않습니다.</b> 치료해야 풀립니다.</para>
    /// </summary>
    public void TickEffects()
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var ticked = _effects[i].Tick();
            if (ticked.IsExpired) _effects.RemoveAt(i);
            else _effects[i] = ticked;
        }
    }

    /// <summary>
    /// 전투가 끝났습니다. <b>상황이 만든 것은 풀리고 몸에 난 것은 남습니다.</b>
    /// </summary>
    public void EndBattle()
    {
        _effects.RemoveAll(e => !e.Profile.Persists);
        ClearDefending();
    }

    /// <summary>치료 소모품으로 풀 수 있는 효과를 제거합니다. 푼 개수를 돌려줍니다.</summary>
    public int Cure(CureItem item)
    {
        if (item == CureItem.None) return 0;
        return _effects.RemoveAll(e => e.Profile.Cure == item);
    }

    public override string ToString() =>
        $"{Name}({Loadout}, {(Row == Row.Front ? "전열" : "후열")}) HP {Hp}/{MaxHp}";
}
