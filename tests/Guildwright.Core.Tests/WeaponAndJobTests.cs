using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 무기 · 숙련도 · 적성 · 직업 · 스킬이 <b>서로 겹치지 않는 다섯 축</b>임을 지킵니다.
/// <para>
/// 예전에는 <c>WeaponStyle</c> 하나가 위력·속도·사거리·회복·도발·광역·마법·치명타를
/// 전부 물고 있었습니다. 그러면 <b>무기가 "파티에서 무슨 역할이냐"까지 정해서</b>
/// 직업·스킬과 축이 겹칩니다.
/// </para>
/// <para>이 파일은 그 분리를 고정합니다 — <b>무기는 위력·속도·사거리, 나머지는 스킬</b>.</para>
/// 근거: docs/08-design-revision.md §16, §10
/// </summary>
public class WeaponAndJobTests(ITestOutputHelper output)
{
    private static readonly PrimaryStats WarriorStats = new(
        Strength: 30, Agility: 14, Finesse: 16, Vitality: 28, Intellect: 8, Spirit: 10);

    private static Adventurer Make(
        WeaponKind? weapon = null,
        JobId job = JobId.SwordApprentice,
        IReadOnlyList<SkillId>? innate = null,
        ulong seed = 7) =>
        new("T", "시험체", WarriorStats, 20,
            GrowthProfile.Roll(new DeterministicRandom(seed), 3),
            aptitudes: WeaponAptitudes.Uniform(AptitudeGrade.B),
            loadout: weapon is null ? null : Loadout.Single(weapon.Value),
            job: job,
            innate: innate);

    private static void Train(Adventurer a, int years)
    {
        for (int y = 0; y < years; y++)
        {
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom((ulong)y * 31 + 1));
        }
    }

    // ---- 무기는 셋만 정합니다 ----

    [Fact]
    public void 무기는_위력_속도_사거리만_정한다()
    {
        var spec = Weaponry.Of(WeaponKind.Staff);

        Assert.Equal(Reach.Ranged, spec.Reach);
        Assert.True(spec.Power > 0.0);
        Assert.True(spec.Speed > 0.0);

        // 지팡이가 마법 위력을 쓰는 것은 "위력의 종류"이고 능력이 아닙니다.
        Assert.True(spec.UsesMagicPower);
    }

    [Fact]
    public void 후열_타격은_사거리에서_나온다()
    {
        // 스킬이 아니라 물건의 성질입니다 — 활은 멀리 닿습니다.
        Assert.True(Weaponry.Of(WeaponKind.Bow).CanStrikeBackRow);
        Assert.False(Weaponry.Of(WeaponKind.Sword).CanStrikeBackRow);

        // 창은 적 후열까지는 못 닿지만 아군 후열에 서서 전열을 칩니다.
        var spear = Weaponry.Of(WeaponKind.Spear);
        Assert.False(spear.CanStrikeBackRow);
        Assert.True(spear.CanActFromBackRow);
    }

    [Fact]
    public void 방패와_가방은_때리는_물건이_아니다()
    {
        Assert.False(Weaponry.Of(WeaponKind.Shield).IsWeapon);
        Assert.False(Weaponry.Of(WeaponKind.Backpack).IsWeapon);
    }

    // ---- 손 배치가 곧 스타일 ----

    [Fact]
    public void 손_배치가_스타일을_만든다()
    {
        var shielded = Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield);
        var twin = Loadout.Pair(WeaponKind.Sword, WeaponKind.Sword);

        Assert.Equal(WeaponKind.Sword, shielded.MainWeapon);
        Assert.Equal(WeaponKind.Sword, twin.MainWeapon);

        // 쌍수가 이득인 이유 — 보조손 위력이 절반 더해집니다.
        output.WriteLine($"검+방패 {shielded.Power:F2} vs 쌍수 {twin.Power:F2}");
        Assert.True(twin.Power > shielded.Power);
    }

    [Fact]
    public void 양손_무기는_같은_세트의_다른_칸을_비운다()
    {
        var loadout = Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield);
        loadout.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Bow);

        Assert.Equal(WeaponKind.Bow, loadout[WeaponSet.Primary, Hand.Right]);
        Assert.Equal(WeaponKind.None, loadout[WeaponSet.Primary, Hand.Left]);
    }

    [Fact]
    public void 보조무기가_있으면_전환할_수_있다()
    {
        var loadout = Loadout.Single(WeaponKind.Bow);
        Assert.False(loadout.CanSwitch);

        loadout.Equip(WeaponSet.Secondary, Hand.Right, WeaponKind.Sword);
        Assert.True(loadout.CanSwitch);

        loadout.Switch();
        Assert.Equal(WeaponKind.Sword, loadout.MainWeapon);
    }

    // ---- 짐꾼 ----

    [Fact]
    public void 가방을_들면_보조무기_칸을_쓸_수_없다()
    {
        // 그래서 짐꾼은 그 파견 동안 무방비이고, 파티가 지켜야 합니다.
        var loadout = new Loadout();
        loadout.Equip(WeaponSet.Secondary, Hand.Right, WeaponKind.Sword);
        loadout.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Backpack);

        Assert.True(loadout.CarryingPack);
        Assert.False(loadout.CanSwitch);
        Assert.Equal(WeaponKind.None, loadout[WeaponSet.Secondary, Hand.Right]);
    }

    [Fact]
    public void 짐_용량은_근력이_아니라_가방이_정한다()
    {
        // 짐 드는 것 자체는 근력만 있으면 누구나 하는 일이라
        // 그것만으로는 직업이 성립하지 않습니다. 칸을 내주는 것이 대가입니다.
        Assert.Equal(0, Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield).Load);
        Assert.True(Loadout.Single(WeaponKind.Backpack).Load > 0);
    }

    [Fact]
    public void 짐꾼은_전투_직업이_아니다()
    {
        // 파티 구성 규칙(짐꾼 최대 1명 · 짐꾼만으로 구성 불가)의 근거입니다.
        Assert.False(Jobs.Of(JobId.Porter).Combat);
        Assert.True(Jobs.Of(JobId.Swordsman).Combat);
    }

    [Fact]
    public void 기본_장비는_직업에서_나온다()
    {
        // 예전 기본값은 직업과 무관하게 검+방패였습니다. 그러면 짐꾼이 가방 없이
        // 태어나고, 가방을 요구하는 액티브가 조용히 사라집니다 —
        // 터지지 않고 그냥 아무 일도 안 일어나므로 테스트로만 잡힙니다.
        var porter = Make(job: JobId.Porter);

        Assert.True(porter.Loadout.CarryingPack);
        Assert.Contains(SkillId.HandPotion, porter.Actives);

        // 검+방패로 태어났다면 짐 건네기를 쓸 수 없습니다.
        var swordsman = Make(job: JobId.SwordApprentice);
        Assert.False(swordsman.Loadout.CarryingPack);
        Assert.DoesNotContain(SkillId.HandPotion, swordsman.Actives);
    }

    [Fact]
    public void 방패병은_방패를_양손에_들지_않는다()
    {
        // 방패는 때리는 물건이 아니라서 양손에 들면 아무것도 못 합니다.
        var shield = Make(job: JobId.ShieldApprentice);

        Assert.True(shield.Loadout.Holding(WeaponKind.Shield));
        Assert.False(shield.Loadout.Unarmed);

        // 그래도 방패 숙련은 쌓입니다 — 든 것 전부가 오르므로 사다리가 막히지 않습니다.
        Train(shield, 3);
        Assert.True(shield.Proficiency[WeaponKind.Shield] > 0);
    }

    [Fact]
    public void 요구_무기가_여러개면_기본_장비가_시드에_흔들리지_않는다()
    {
        // Dictionary 순회 순서에 기대면 같은 입력이 다른 손 배치를 낼 수 있습니다.
        var first = Make(job: JobId.SpellArcher).Loadout;
        var second = Make(job: JobId.SpellArcher).Loadout;

        Assert.Equal(first.ToString(), second.ToString());
        Assert.Equal(WeaponKind.Bow, first.MainWeapon);
    }

    // ---- 숙련도와 적성 ----

    [Fact]
    public void 숙련도는_무기_종류별로_쌓인다()
    {
        var a = Make(WeaponKind.Greatsword, JobId.GreatApprentice);
        Assert.Equal(0, a.Proficiency[WeaponKind.Greatsword]);

        Train(a, 5);

        int great = a.Proficiency[WeaponKind.Greatsword];
        output.WriteLine($"대검 5년: {great}");
        Assert.True(great > 0);

        // 다른 무기는 안 늘었습니다 — 무기를 바꾸는 데 시간이라는 대가가 있습니다.
        Assert.Equal(0, a.Proficiency[WeaponKind.Bow]);
    }

    [Fact]
    public void 든_것_모두의_숙련도가_오른다()
    {
        // 검+방패면 둘 다 늡니다. 방패술 패시브가 방패 숙련에서 나오는 근거입니다.
        var a = Make(seed: 3);   // 기본 구성이 검+방패
        Train(a, 4);

        Assert.True(a.Proficiency[WeaponKind.Sword] > 0);
        Assert.True(a.Proficiency[WeaponKind.Shield] > 0);
    }

    [Fact]
    public void 적성은_잠재력과_상관관계를_갖는다()
    {
        // 이 상관이 없으면 "마법 잠재력 최고인데 대검 적성"같은 모순이 흔해집니다.
        // 다만 노이즈가 있어서 가끔은 나오고, 그게 발견의 재미입니다.
        var potential = new PrimaryStats(
            Strength: 30, Agility: 40, Finesse: 40, Vitality: 40, Intellect: 95, Spirit: 90);

        int staffWins = 0;
        const int trials = 200;

        for (ulong seed = 0; seed < trials; seed++)
        {
            var aptitudes = WeaponAptitudes.Roll(potential, new DeterministicRandom(seed));
            if (aptitudes[WeaponKind.Staff] >= aptitudes[WeaponKind.Greatsword]) staffWins++;
        }

        double rate = staffWins / (double)trials;
        output.WriteLine($"마법 잠재력이 높을 때 지팡이 적성 ≥ 대검 적성: {rate:P0}");
        Assert.True(rate > 0.7, "적성이 잠재력과 거의 무관하게 굴러갑니다.");
    }

    // ---- 직업 ----

    [Fact]
    public void 계급이_직업_표에_흡수되었다()
    {
        // 예전에는 JobRank 열거형(견습~대가)이 따로 있었고 연봉·수주 난이도까지
        // 거기 걸려 있었습니다. 지금은 요구 숙련만 다른 직업 행입니다.
        var apprentice = Jobs.Of(JobId.SwordApprentice);
        var saint = Jobs.Of(JobId.SwordSaint);

        Assert.Empty(apprentice.Requires);
        Assert.NotEmpty(saint.Requires);

        Assert.True(saint.MaxContractDifficulty > apprentice.MaxContractDifficulty);
        Assert.True(saint.Upkeep > apprentice.Upkeep);
        Assert.True(saint.ActiveSlots > apprentice.ActiveSlots);
    }

    [Fact]
    public void 유지비는_그_등급의_최대_수주_보수보다_낮다()
    {
        // 예전에 최고 등급을 1,300으로 잡았다가 최대 보수 1,200을 넘겨
        // 최고 등급이 순수 적자가 되는 문제가 있었습니다. 테스트로 고정합니다.
        foreach (var job in Jobs.Catalogue)
        {
            int maxIncome = job.MaxContractDifficulty * CareerRules.IncomePerDifficulty;
            Assert.True(job.Upkeep < maxIncome,
                $"{job.Korean}: 유지비 {job.Upkeep} ≥ 최대 보수 {maxIncome}. 순수 적자가 됩니다.");
        }
    }

    [Fact]
    public void 요구_숙련이_없는_직업만_처음부터_고를_수_있다()
    {
        Assert.NotEmpty(Jobs.Starting);
        foreach (var id in Jobs.Starting) Assert.Empty(Jobs.Of(id).Requires);
    }

    [Fact]
    public void 히든_직업은_여러_무기_숙련을_요구한다()
    {
        var hidden = Jobs.Catalogue.Where(j => j.IsHidden).ToList();

        Assert.NotEmpty(hidden);
        foreach (var job in hidden) Assert.True(job.Requires.Count > 1);
    }

    [Fact]
    public void 조합_직업은_평균이_아니라_합집합이다()
    {
        // 양쪽 직업이 각각 가진 장점을 흡수합니다. 대가는 규칙이 아니라 시간입니다 —
        // 두 숙련을 올리는 데 배로 걸리고 경력이 유한하므로 못 닿을 수도 있습니다.
        var spellArcher = Jobs.Of(JobId.SpellArcher);

        Assert.Contains(SkillId.SteadyAim, spellArcher.Grants);   // 활 계열의 것
        Assert.Contains(SkillId.Cure, spellArcher.Grants);        // 마법 계열의 것

        Assert.Contains(SkillId.SteadyAim, Jobs.Of(JobId.Marksman).Grants);
        Assert.Contains(SkillId.Cure, Jobs.Of(JobId.HighMage).Grants);

        // 그리고 슬롯이 충분해야 실제로 둘 다 쓸 수 있습니다.
        Assert.True(spellArcher.ActiveSlots >= Jobs.Of(JobId.Marksman).ActiveSlots);
    }

    [Fact]
    public void 숙련도가_모자라면_직업이_해금되지_않는다()
    {
        var rookie = Make();
        var available = rookie.AvailableJobs.Select(j => j.Id).ToList();

        Assert.Contains(JobId.SwordApprentice, available);
        Assert.DoesNotContain(JobId.SwordSaint, available);
        Assert.DoesNotContain(JobId.SpellArcher, available);
    }

    [Fact]
    public void 전직은_자유지만_해금되지_않은_직업으로는_못_간다()
    {
        var a = Make();

        Assert.False(a.ChangeJob(JobId.SwordSaint), "숙련도 없이 검성이 됐습니다.");
        Assert.True(a.ChangeJob(JobId.Porter), "요구 숙련이 없는 직업으로도 못 갑니다.");
        Assert.Equal(JobId.Porter, a.Job);
    }

    [Fact]
    public void 고집을_타고나면_전직_권유를_듣지_않는다()
    {
        // 태생 패시브가 수치 보정만이 아니라 지시를 제약합니다.
        // 전투 중 상태이상과 짝을 이룹니다 — 자리만 다르고 성질은 같습니다.
        var stubborn = Make(innate: [SkillId.Stubborn], seed: 11);

        var before = stubborn.Job;
        Assert.False(stubborn.ChangeJob(JobId.Porter));
        Assert.Equal(before, stubborn.Job);
    }

    // ---- 스킬 ----

    [Fact]
    public void 회복과_도발은_무기가_아니라_스킬이_정한다()
    {
        // 예전에는 StyleCapability.CanHeal / CanTaunt 였습니다.
        Assert.Equal(TacticAction.HealAlly, SkillBook.Of(SkillId.Cure).Action);
        Assert.Equal(TacticAction.Taunt, SkillBook.Of(SkillId.Provoke).Action);
    }

    [Fact]
    public void 액티브는_특정_무기를_들어야_쓸_수_있다()
    {
        // 이것이 "궁수가 창을 들 수 있지만 손해"를 실제 규칙으로 만듭니다.
        var cure = SkillBook.Of(SkillId.Cure);

        Assert.True(cure.UsableWith(Loadout.Single(WeaponKind.Staff)));
        Assert.False(cure.UsableWith(Loadout.Single(WeaponKind.Spear)));
    }

    [Fact]
    public void 무기를_바꾸면_그_무기의_액티브가_죽는다()
    {
        var mage = Make(WeaponKind.Staff, JobId.Mage, seed: 17);
        Assert.Contains(SkillId.Cure, mage.Actives);

        mage.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Spear);
        Assert.DoesNotContain(SkillId.Cure, mage.Actives);
    }

    [Fact]
    public void 액티브는_슬롯_수만큼만_장착된다()
    {
        var sage = Make(WeaponKind.Staff, JobId.Sage, seed: 19);
        Assert.True(sage.Actives.Count <= Jobs.Of(JobId.Sage).ActiveSlots);
    }

    [Fact]
    public void 패시브는_슬롯_없이_전부_적용된다()
    {
        // 전투에 도움이 되는 것도 안 되는 것도 많아 골라 낄 성질이 아닙니다.
        var knight = new Adventurer("K", "기사", WarriorStats, 20,
            GrowthProfile.Roll(new DeterministicRandom(23), 3),
            aptitudes: WeaponAptitudes.Uniform(AptitudeGrade.B),
            loadout: Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield),
            job: JobId.Knight,
            innate: [SkillId.Careful]);

        Assert.Contains(SkillId.Shielding, knight.Passives);   // 직업
        Assert.Contains(SkillId.Careful, knight.Passives);     // 태생
    }

    [Fact]
    public void 치명타_배율은_무기가_아니라_숙련_패시브에서_나온다()
    {
        // 초보가 대검을 들었다고 바로 한 방이 커지는 건 이상합니다.
        var plain = TestParty.Make("A", Team.Player, 50);
        Assert.Equal(DamageModel.BaseCritMultiplier, plain.CritMultiplier);

        var heavy = new Combatant("C", "숙련자", Team.Player, WarriorStats, 50,
            Loadout.Single(WeaponKind.Greatsword), 1.0, Row.Front, TestParty.SensibleTactics,
            passives: [SkillId.HeavyBlow]);

        output.WriteLine($"기본 {plain.CritMultiplier:F2} vs 양손 숙달 {heavy.CritMultiplier:F2}");
        Assert.True(heavy.CritMultiplier > plain.CritMultiplier);
    }

    [Fact]
    public void 태생_패시브는_이득에_대가가_붙는다()
    {
        // 이득만 있으면 모두가 같은 성격을 원하게 되어 성격이 서열이 됩니다.
        var auras = SkillBook.Catalogue
            .Where(s => s.Source == SkillSource.Innate && s.Boosts is not null)
            .ToList();

        Assert.NotEmpty(auras);
        foreach (var skill in auras) Assert.NotNull(skill.Costs);
    }

    [Fact]
    public void 태생과_직업은_획득_경로가_다른_축이다()
    {
        // ⚠️ 예전 이 테스트는 "태생은 파티 전체, 직업은 자신에게만"을 고정했습니다.
        //    그것은 docs/08 §10에 "[제안] 밸런스 축 — 승인 안 됨"으로 적힌
        //    에이전트 안이고, 테스트로 고정하면 다음 세션이 주인님의 결정으로 읽습니다.
        //    승인 안 된 배치를 고정하지 않고, 확정된 것만 고정합니다.
        //
        // 확정된 것: 태생은 타고나는 것이고 직업은 배우는 것 — 획득 경로가 다릅니다 (§10).
        foreach (var id in SkillBook.InnatePool)
        {
            Assert.Equal(SkillSource.Innate, SkillBook.Of(id).Source);
        }

        // 직업이 주는 것은 직업 스킬이어야 합니다. 직업이 태생을 주면 두 축이 붕괴합니다 —
        // "타고난다"와 "배운다"가 같아지기 때문입니다.
        foreach (var job in Jobs.Catalogue)
        {
            foreach (var id in job.Grants)
            {
                Assert.Equal(SkillSource.Job, SkillBook.Of(id).Source);
            }
        }
    }

    [Fact]
    public void 모든_직업이_주는_스킬은_표에_있다()
    {
        // 표에서 빠진 스킬을 참조하면 조회 시점에 터집니다. 시작할 때 잡습니다.
        foreach (var job in Jobs.Catalogue)
        {
            foreach (var id in job.Grants) Assert.NotNull(SkillBook.Of(id));
        }
    }
}
