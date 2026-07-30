using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Skills;

/// <summary>
/// 스킬이 어디서 오는가.
/// <para>
/// <b>효과 표현은 공유하고 획득 규칙만 분리합니다.</b> 셋이 정반대라 한 그릇에 못 담습니다 —
/// 획득(타고남 / 육성의 보상) · 제거(불가 / 액티브만 교체) · 대상(파티 / 자신).
/// </para>
/// <para>
/// 갈라두지 않으면 <b>"성격을 훈련으로 바꿀 수 있나"</b>에 답할 수 없습니다.
/// 예라면 성격이 정체성이 아니게 되고, 아니라면 왜 같은 시스템인지 설명이 안 됩니다.
/// </para>
/// 근거: docs/08-design-revision.md §10
/// </summary>
public enum SkillSource
{
    /// <summary>타고남. 플레이어 통제 밖이고 제거할 수 없습니다. 성격이 여기 들어갑니다.</summary>
    Innate,
    /// <summary>직업이 줍니다. 전직해도 패시브는 남습니다.</summary>
    Job
}

/// <summary>
/// 패시브인가 액티브인가. <b>다루는 방식이 다릅니다.</b>
/// </summary>
public enum SkillForm
{
    /// <summary>
    /// 슬롯 없이 <b>가진 것 전부 적용</b>됩니다.
    /// <para>전투에 도움이 되는 것도 안 되는 것도 많아 골라 낄 성질이 아닙니다.</para>
    /// </summary>
    Passive,
    /// <summary>
    /// <b>골라서 장착</b>합니다. 슬롯 수는 직업이 정하고, 특정 무기를 요구합니다.
    /// <para>이것이 빌드를 만듭니다.</para>
    /// </summary>
    Active
}

/// <summary>
/// 스킬 이름. <b>길어지는 것은 문제가 아닙니다</b> — 코드 경로가 아니라 표의 행입니다.
/// </summary>
public enum SkillId
{
    // ---- 직업 액티브 — 예전에 무기 스타일이 열던 것들 ----
    /// <summary>회복. 지팡이 숙련이 엽니다.</summary>
    Cure,
    /// <summary>아군 강화.</summary>
    Empower,
    /// <summary>적 약화.</summary>
    Enfeeble,
    /// <summary>도발. 방패 숙련이 엽니다.</summary>
    Provoke,
    /// <summary>광역 공격. 대검 숙련이 엽니다.</summary>
    Sweep,
    /// <summary>회복약을 아군에게 씁니다. <b>짐꾼의 핵심</b>입니다.</summary>
    HandPotion,
    /// <summary>
    /// 관통 사격 — <b>적 후열을 노립니다.</b>
    /// <para>
    /// 원거리 무기를 들었다고 후열이 열리는 게 아니라 <b>이걸 배워야</b> 열립니다.
    /// §10 "[확정] 스킬이 떠맡게 된 것" 표의 <c>CanStrikeBackRow → 스킬</c>이 이 줄입니다.
    /// </para>
    /// </summary>
    PiercingShot,

    // ---- 직업 패시브 — 숙련도가 여는 것 ----
    /// <summary>쌍수 숙달. 치명타가 자잘하게 자주 터집니다.</summary>
    TwinStrike,
    /// <summary>양손 숙달. 치명타가 드물지만 한 방이 큽니다.</summary>
    HeavyBlow,
    /// <summary>방패술. 방패를 오래 들어 얻은 방어.</summary>
    Shielding,
    /// <summary>조준. 활을 오래 쏴서 얻은 명중.</summary>
    SteadyAim,
    /// <summary>짐 다루기. 무거운 짐을 지고도 버팁니다.</summary>
    Packcraft,

    // ---- 태생 패시브 (성격) ----
    // ⚠️ 효과가 파티 전체인지 자신인지는 [제안]일 뿐 승인되지 않았습니다 (docs/08 §10).
    /// <summary>신중. 방어가 오르되 느려집니다.</summary>
    Careful,
    /// <summary>막무가내. 위력이 오르되 회피가 떨어집니다.</summary>
    Reckless,
    /// <summary>분위기 메이커. 마법 방어가 오르되 물리 위력이 떨어집니다.</summary>
    Cheerful,
    /// <summary>고집. <b>플레이어의 전직 권유를 듣지 않습니다.</b></summary>
    Stubborn
}

/// <summary>
/// 스킬 하나의 정의.
/// <para>
/// 매개변수는 형태마다 쓰이는 것이 다릅니다 — <see cref="RequiresWeapon"/>과
/// <see cref="ManaCost"/>는 액티브에서만 의미가 있습니다.
/// </para>
/// </summary>
/// <param name="Id">이름.</param>
/// <param name="Korean">표시 이름.</param>
/// <param name="Source">타고났는가 직업이 줬는가.</param>
/// <param name="Form">패시브인가 액티브인가.</param>
/// <param name="Action">액티브가 실행하는 행동.</param>
/// <param name="RequiresWeapon">
/// 이 무기를 들고 있어야 쓸 수 있습니다.
/// <para>
/// 이것이 §16의 <b>"궁수가 창을 들 수 있지만 손해"</b>를 실제 규칙으로 만듭니다 —
/// 창을 들면 궁수 액티브가 통째로 죽습니다.
/// </para>
/// </param>
/// <param name="ManaCost">
/// 마나 소모.
/// <para>
/// <b>물리 기술은 싸고 마법은 비쌉니다.</b> 자원을 마나 하나로 통일하되 소모량을 다르게
/// 두는 이유는, 최대 마나가 정신·지능에서만 나와 특화 전사가 마법사의 1/4밖에
/// 못 가지기 때문입니다 (측정: 정신 12 vs 50).
/// </para>
/// </param>
/// <param name="Cooldown">재사용까지 필요한 라운드.</param>
/// <param name="Boosts">패시브가 올리는 수치.</param>
/// <param name="BoostAmount">올리는 양.</param>
/// <param name="Costs">그 대가로 내리는 수치. <b>이득만 있으면 성격이 서열이 됩니다.</b></param>
/// <param name="CostAmount">내리는 양.</param>
/// <param name="CritMultiplierBonus">
/// 치명타 배율 보정. <b>확률과 배율을 반대로 배치해 성격이 갈리게 합니다</b> —
/// 쌍수는 자잘하게 자주, 양손은 드물지만 한 방이 큽니다.
/// </param>
/// <param name="PartyWide">
/// 파티 전체에 걸리는가.
/// <para>
/// ⚠️ <b>[제안] — 승인되지 않았습니다.</b> "태생은 약하지만 파티 전체, 직업은 강하지만
/// 자신"은 <b>에이전트가 낸 밸런스 안</b>이고, docs/08 §10에 <b>"[제안] 밸런스 축 —
/// 승인 안 됨"</b>으로 명시되어 있습니다. 지금 표의 값이 그 안대로 들어가 있으나
/// <b>주인님의 결정으로 인용하지 마세요.</b>
/// </para>
/// <para>
/// 확정된 것은 §10의 <b>"오라 = 파티 전체 패시브"</b>라는 형태뿐이고, 태생과 직업 중
/// 어느 쪽이 오라를 갖는지·세기가 어떻게 갈리는지는 정해지지 않았습니다.
/// </para>
/// </param>
public sealed record Skill(
    SkillId Id,
    string Korean,
    SkillSource Source,
    SkillForm Form,
    TacticAction? Action = null,
    WeaponKind RequiresWeapon = WeaponKind.None,
    int ManaCost = 0,
    int Cooldown = 0,
    DerivedStat? Boosts = null,
    double BoostAmount = 0.0,
    DerivedStat? Costs = null,
    double CostAmount = 0.0,
    double CritMultiplierBonus = 0.0,
    bool PartyWide = false)
{
    /// <summary>이 무기 구성으로 쓸 수 있는가.</summary>
    public bool UsableWith(Loadout loadout) =>
        RequiresWeapon == WeaponKind.None || loadout.Holding(RequiresWeapon);

    public override string ToString() => Korean;
}

/// <summary>
/// 스킬 목록과 조회.
/// <para>
/// ⚠️ <b>수치는 전부 임시값입니다.</b> 999 스케일 전환 뒤 배치 시뮬레이션으로 잡습니다.
/// </para>
/// <para>
/// ⚠️ <b>태생 패시브(성격) 4종은 기전 검증용 최소 구성입니다.</b> 주인님이 종류 수를
/// "36가지여도 되고 더 줄여도 되고"로 열어두셨으므로, 개수와 내용은 아직 정해지지
/// 않았습니다 — docs/08 §10 [검토중].
/// </para>
/// </summary>
public static class SkillBook
{
    private static readonly Skill[] Table =
    [
        // ---- 직업 액티브 ----
        // 예전에는 무기가 이 능력을 물고 있었습니다(StyleCapability.CanHeal 등).
        // 지금은 스킬이 정하고, 무기는 "그 스킬을 쓸 수 있는 조건"일 뿐입니다.

        new(SkillId.Cure, "치유", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.HealAlly, RequiresWeapon: WeaponKind.Staff,
            ManaCost: 10, Cooldown: 1),

        new(SkillId.Empower, "축복", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.BuffAlly, RequiresWeapon: WeaponKind.Staff,
            ManaCost: 8, Cooldown: 2),

        new(SkillId.Enfeeble, "저하", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.DebuffEnemy, RequiresWeapon: WeaponKind.Staff,
            ManaCost: 8, Cooldown: 2),

        // 방패를 들어야 몸으로 막을 수 있습니다.
        new(SkillId.Provoke, "도발", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.Taunt, RequiresWeapon: WeaponKind.Shield,
            ManaCost: 2, Cooldown: 2),

        // 물리 기술은 마나가 쌉니다.
        new(SkillId.Sweep, "휘두르기", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.AttackAll, RequiresWeapon: WeaponKind.Greatsword,
            ManaCost: 3, Cooldown: 2),

        // 짐꾼의 핵심. 위기의 순간에 아군에게 회복약을 건넵니다.
        // 짐은 아무나 들 수 있지만 제때 쓰는 것은 다릅니다.
        new(SkillId.HandPotion, "회복약 건네기", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.GivePotion, RequiresWeapon: WeaponKind.Backpack,
            ManaCost: 0, Cooldown: 1),

        // 후열 타격은 무기가 아니라 이 스킬이 엽니다 (§10).
        // 사거리는 무기가 정하지만, 그 사거리로 뒤를 노리는 것은 배워야 합니다.
        new(SkillId.PiercingShot, "관통 사격", SkillSource.Job, SkillForm.Active,
            Action: TacticAction.AttackBackRow, ManaCost: 4, Cooldown: 1),

        // ---- 직업 패시브 — 숙련도가 여는 것 ----
        // 치명타 특성이 여기 있는 이유: 초보가 쌍수를 들었다고 바로 자잘하게 잘 터지는 건
        // 이상합니다. 그건 무기의 성질이 아니라 오래 써서 얻은 것입니다.

        new(SkillId.TwinStrike, "쌍수 숙달", SkillSource.Job, SkillForm.Passive,
            Boosts: DerivedStat.CritChance, BoostAmount: 0.06),

        new(SkillId.HeavyBlow, "양손 숙달", SkillSource.Job, SkillForm.Passive,
            Boosts: DerivedStat.PhysicalPower, BoostAmount: 6.0,
            CritMultiplierBonus: 0.4),

        new(SkillId.Shielding, "방패술", SkillSource.Job, SkillForm.Passive,
            Boosts: DerivedStat.PhysicalGuard, BoostAmount: 8.0),

        new(SkillId.SteadyAim, "조준", SkillSource.Job, SkillForm.Passive,
            Boosts: DerivedStat.CritChance, BoostAmount: 0.04),

        new(SkillId.Packcraft, "짐 다루기", SkillSource.Job, SkillForm.Passive,
            Boosts: DerivedStat.MaxHp, BoostAmount: 6.0),

        // ---- 태생 패시브 (성격) ----
        // 이득에는 대가가 붙습니다 — 대가가 없으면 모두가 같은 성격을 원해 성격이 서열이 됩니다.
        // ⚠️ PartyWide 배치와 세기는 [제안]이며 승인되지 않았습니다 (docs/08 §10).

        new(SkillId.Careful, "신중", SkillSource.Innate, SkillForm.Passive,
            Boosts: DerivedStat.PhysicalGuard, BoostAmount: 4.0,
            Costs: DerivedStat.ActionSpeed, CostAmount: 0.4,
            PartyWide: true),

        new(SkillId.Reckless, "막무가내", SkillSource.Innate, SkillForm.Passive,
            Boosts: DerivedStat.PhysicalPower, BoostAmount: 4.0,
            Costs: DerivedStat.EvasionChance, CostAmount: 0.02,
            PartyWide: true),

        new(SkillId.Cheerful, "분위기 메이커", SkillSource.Innate, SkillForm.Passive,
            Boosts: DerivedStat.MagicGuard, BoostAmount: 5.0,
            Costs: DerivedStat.PhysicalPower, CostAmount: 2.0,
            PartyWide: true),

        // 수치를 안 건드리는 대신 플레이어 지시를 제약합니다.
        // 전투 중 상태이상과 짝을 이룹니다 — 자리만 다르고 성질은 같습니다.
        new(SkillId.Stubborn, "고집", SkillSource.Innate, SkillForm.Passive)
    ];

    private static readonly Dictionary<SkillId, Skill> ById = Table.ToDictionary(s => s.Id);

    public static IReadOnlyList<Skill> Catalogue => Table;

    public static Skill Of(SkillId id) => ById[id];

    /// <summary>태생 패시브 풀. 모집할 때 여기서 뽑습니다.</summary>
    public static IReadOnlyList<SkillId> InnatePool { get; } =
        Table.Where(s => s.Source == SkillSource.Innate).Select(s => s.Id).ToArray();
}
