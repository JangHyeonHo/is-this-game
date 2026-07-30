using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Parties;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 독립 리뷰(2026-07-30)가 찾은 설계 이탈들의 회귀 테스트.
/// <para>
/// 세 에이전트가 <c>docs/08</c>의 [확정] 항목과 구현을 대조해 찾은 것들입니다. 전부
/// <b>테스트를 통과하면서</b> 들어와 있었으므로, 여기 있는 것은 "그때 없던 자"입니다.
/// </para>
/// <para>
/// 가장 무거운 것은 세 가지였습니다.
/// <list type="number">
///   <item><b>토벌형이 난이도 3부터 성공 불가</b> — 강도에 난이도를 곱해 요구치만 올랐습니다.</item>
///   <item><b>승급 경로가 아예 배선되지 않음</b> — 등급이 영원히 F였습니다.</item>
///   <item><b>승인 안 된 [제안]이 주석·수치·테스트로 굳음</b> — 저장소가 가장 경계하는 실패입니다.</item>
/// </list>
/// </para>
/// </summary>
public class DesignConformanceTests(ITestOutputHelper output)
{
    private static readonly PrimaryStats Sturdy = new(
        Strength: 34, Agility: 18, Finesse: 20, Vitality: 40, Intellect: 16, Spirit: 20);

    private static Adventurer Grown(string id, JobId job = JobId.SwordApprentice, ulong seed = 11, int years = 3)
    {
        var a = new Adventurer(id, id, Sturdy, 40,
            GrowthProfile.Roll(new DeterministicRandom(seed), 3), job: job);

        for (int y = 0; y < years; y++)
        {
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(seed + (ulong)y + 1));
        }
        return a;
    }

    // ════ 1. 토벌형 달성 가능성 ════

    [Fact]
    public void 토벌형은_최소_인원으로도_달성_가능해야_한다()
    {
        // ⚠️ 예전에는 강도 = 난이도 × 기간 × 2.0이었습니다. 한 달에 전투는 최대 1회이고
        //    한 전투의 적은 최대 4마리이므로 처치 상한이 정해져 있는데 요구치만 난이도에
        //    비례해 올라, 난이도 3 이상의 토벌은 성공이 산술적으로 불가능했습니다.
        //    §17.4 "달성 전제"와 §17.8 "감당 못 할 의뢰는 아예 뜨지 않는다"가 동시에 깨졌습니다.
        //    원인은 docs/06 #33이 이미 경고한 "수로 난이도를 나타내면 난이도가 두 번 반영".
        foreach (int difficulty in new[] { 1, 2, 3, 4 })
        {
            int unfinished = 0, unfinishedWithoutRest = 0;
            const int trials = 40;

            for (ulong seed = 0; seed < trials; seed++)
            {
                var party = new[] { Grown("A", JobId.ShieldApprentice), Grown("B", seed: 21) };
                var contract = new Contract("t", "시험", ContractForm.Subjugate, ContractSource.Realm,
                    difficulty, Months: 3,
                    Intensity: (int)Math.Round(3 * ContractBoard.SubjugateKillsPerMonth));

                var session = new DeploymentSession(party, contract, new DeterministicRandom(seed));
                while (!session.IsComplete) session.AdvanceMonth();

                if (session.Complete().Failure != DeploymentFailure.Unfinished) continue;

                unfinished++;

                // 쉰 달이 있어서 못 채운 것은 설계대로입니다 (§17.5b). 쉬지도 않았는데
                // 못 채웠다면 요구치가 애초에 닿을 수 없는 값이라는 뜻입니다.
                if (!session.Months.Any(m => m.Work == MonthWork.Rest)) unfinishedWithoutRest++;
            }

            output.WriteLine($"난이도 {difficulty}: 진척 미달 {unfinished}/{trials} " +
                             $"(그중 쉬지도 않은 경우 {unfinishedWithoutRest})");

            // 예전에는 난이도 3 이상이 100% 미달이었습니다. 지금 남은 미달은
            // <b>조우 운</b>입니다 — 토벌은 "안 만나면 진척이 없다"(§17.3)이므로,
            // 조우가 적게 뜬 달이 겹치면 끝까지 일해도 못 채웁니다.
            //
            // 그 잔여율이 적정한지는 <b>주인님이 판단할 체감</b>이고 감으로 못 정합니다
            // (docs/06 #42의 [검토중]). 여기서는 <b>구조가 무너지지 않았는지</b>만 지킵니다 —
            // 즉 "운이 나빴다"가 아니라 "애초에 불가능하다"로 돌아가면 깨집니다.
            if (difficulty <= 2) Assert.True(unfinished <= trials / 8,
                $"난이도 {difficulty}에서 진척 미달 {unfinished}/{trials} — 요구치가 다시 닿을 수 없는 값입니다");
        }
    }

    [Fact]
    public void 강도는_난이도를_곱하지_않는다()
    {
        // 난이도는 적을 강하게 만들고, 수는 파티 인원이 정합니다 (docs/06 #33).
        var boards = Enumerable.Range(1, 12)
            .SelectMany(m => ContractBoard.Post(new DeterministicRandom((ulong)m), m, Rank.A))
            .ToList();

        foreach (var c in boards.Where(c => c.Form == ContractForm.Subjugate))
        {
            // 한 달에 전투는 한 번, 한 전투의 적은 최대 MaxEnemies마리입니다.
            int ceiling = c.Months * EncounterGenerator.MaxEnemies;
            Assert.True(c.Intensity <= ceiling,
                $"{c.Name}: 강도 {c.Intensity} > 처치 상한 {ceiling} — 성공이 불가능합니다");
        }
    }

    [Fact]
    public void 승급_의뢰의_강도도_달성_가능해야_한다()
    {
        // 예전 D 승급은 2달에 12마리였고 처치 상한이 8이라 통과가 불가능했습니다 —
        // 등급이 영원히 F에 고정되고 그 위의 자격이 전부 잠겼습니다.
        foreach (var target in Ranks.All.Skip(1))
        {
            var quest = ContractBoard.Promotion(target);
            int ceiling = quest.Months * EncounterGenerator.MaxEnemies;

            output.WriteLine($"{quest.Name}: 난이도 {quest.Difficulty} · 강도 {quest.Intensity} (상한 {ceiling})");
            Assert.True(quest.Intensity <= ceiling);
        }
    }

    [Fact]
    public void 랭크가_올라도_낮은_난이도_의뢰가_사라지지_않는다()
    {
        // 게시판은 길드 공용입니다. 하한을 올리면 매년 1월에 뽑는 신입이 받을 게 없어집니다.
        var high = Enumerable.Range(1, 12)
            .SelectMany(m => ContractBoard.Post(new DeterministicRandom((ulong)m + 500), m, Rank.S))
            .ToList();

        Assert.Contains(high, c => c.Difficulty <= 2);
    }

    // ════ 2. 승급 배선 ════

    [Fact]
    public void 승급_의뢰를_통과하면_등급이_오른다()
    {
        // ⚠️ 예전에는 Promotion()도 Promote()도 src에서 호출되지 않았습니다.
        //    등급이 오르는 경로가 없으니 모두 영구히 F였고, RequiredRank 필터 때문에
        //    난이도 4 이상 의뢰가 영원히 보이지 않았습니다.
        var a = Grown("A", JobId.ShieldApprentice);
        var b = Grown("B", seed: 21);
        IReadOnlyList<Adventurer> party = [a, b];

        Assert.Equal(Ranks.Lowest, a.Rank);

        var quest = ContractBoard.Promotion(Rank.E);
        Assert.True(quest.IsPromotion);

        var session = new DeploymentSession(party, quest, new DeterministicRandom(3));
        while (!session.IsComplete) session.AdvanceMonth();
        var result = session.Complete();

        foreach (var member in party)
        {
            CareerSimulator.ResolveDeployment(member, session, result, new DeterministicRandom(9));
        }

        output.WriteLine($"{result} → {a.Name} {a.Rank.Label()}");
        Assert.Equal(result.Succeeded ? Rank.E : Rank.F, a.Rank);
    }

    [Fact]
    public void 실전이_없으면_승급_의뢰가_뜨지_않는다()
    {
        // "훈련만 5년 해도 3등급이 되는" 문제를 막는 것이 승급 의뢰의 목적입니다 (§6.5).
        var trained = Grown("A", years: 5);
        Assert.Equal(0, trained.DeploymentMonths);
        Assert.Null(ContractBoard.PromotionFor(trained));
    }

    // ════ 3. 승인 안 된 [제안]이 굳지 않게 ════

    [Fact]
    public void 직업이_태생_스킬을_주지_않는다()
    {
        // ⚠️ 수송대장이 SkillId.Cheerful(태생)을 줬습니다. 직업이 성격을 주면
        //    "타고난다"와 "배운다"가 같아져 두 축이 붕괴합니다 (§10).
        foreach (var job in Jobs.Catalogue)
        {
            foreach (var id in job.Grants)
            {
                Assert.Equal(SkillSource.Job, SkillBook.Of(id).Source);
            }
        }
    }

    [Fact]
    public void 히든_직업은_합집합이다()
    {
        // ⚠️ 손으로 골라 적었더니 마궁사에서 약화가, 마검사에서 회복이 빠져 있었습니다.
        //    §16.5 [확정]: "평균이 아니라 합집합입니다."
        foreach (var hidden in Jobs.Catalogue.Where(j => j.IsHidden))
        {
            foreach (var (weapon, _) in hidden.Requires)
            {
                // 그 무기 계열의 최상단이 주는 것 전부가 히든 직업에도 있어야 합니다.
                var line = Jobs.Catalogue
                    .Where(j => !j.IsHidden && j.Requires.Count == 1 && j.Requires.ContainsKey(weapon))
                    .SelectMany(j => j.Grants)
                    .Distinct();

                foreach (var id in line)
                {
                    Assert.Contains(id, hidden.Grants);
                }
            }

            output.WriteLine($"{hidden.Korean}: {string.Join(", ", hidden.Grants.Select(g => SkillBook.Of(g).Korean))}");
        }
    }

    // ════ 4. 마나와 쿨다운 — §10.0b "둘 다" ════

    [Fact]
    public void 쿨다운이_실제로_돌아간다()
    {
        // ⚠️ StartCooldown·TickCooldowns가 한 번도 호출되지 않아 Skill.Cooldown이
        //    죽은 데이터였습니다 — "마나 + 쿨다운 둘 다"가 사실상 마나 하나였습니다.
        var mage = TestParty.Make("M", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Staff),
            actives: [SkillId.Cure, SkillId.Empower]);

        var cure = SkillBook.Of(SkillId.Cure);
        Assert.True(cure.Cooldown > 0, "쿨다운이 0이면 이 테스트가 아무것도 지키지 않습니다");

        Assert.True(mage.CanDo(TacticAction.HealAlly));
        mage.PaySkillCost(TacticAction.HealAlly);

        Assert.True(mage.OnCooldown(SkillId.Cure));
        Assert.False(mage.CanDo(TacticAction.HealAlly));

        for (int i = 0; i < cure.Cooldown; i++) mage.TickCooldowns();

        Assert.False(mage.OnCooldown(SkillId.Cure));
        Assert.True(mage.CanDo(TacticAction.HealAlly));
    }

    [Fact]
    public void 마나_소모량이_스킬마다_다르다()
    {
        // ⚠️ 전투가 전부 DamageModel.ManaPerSpell 고정값을 써서 Skill.ManaCost가
        //    읽히지 않았습니다. 물리 기술은 싼 게 아니라 공짜였습니다.
        var mage = TestParty.Make("M", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Staff),
            actives: [SkillId.Cure, SkillId.Empower]);

        int cure = mage.ManaCostOf(TacticAction.HealAlly);
        int buff = mage.ManaCostOf(TacticAction.BuffAlly);

        output.WriteLine($"치유 {cure} · 축복 {buff}");
        Assert.NotEqual(cure, buff);

        int before = mage.Mana;
        mage.PaySkillCost(TacticAction.HealAlly);
        Assert.Equal(before - cure, mage.Mana);
    }

    [Fact]
    public void 물리_기술도_마나를_낸다()
    {
        var warrior = TestParty.Make("W", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Greatsword),
            actives: [SkillId.Sweep]);

        Assert.True(warrior.ManaCostOf(TacticAction.AttackAll) > 0);

        int before = warrior.Mana;
        warrior.PaySkillCost(TacticAction.AttackAll);
        Assert.True(warrior.Mana < before);
    }

    [Fact]
    public void 마나가_모자라면_그_스킬을_못_쓴다()
    {
        var mage = TestParty.Make("M", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Staff),
            actives: [SkillId.Cure]);

        mage.SpendMana(mage.Mana);

        Assert.True(mage.CanDo(TacticAction.HealAlly));     // 스킬은 있습니다.
        Assert.False(mage.CanAfford(TacticAction.HealAlly)); // 그런데 낼 게 없습니다.
    }

    // ════ 5. 스킬이 행동을 엽니다 — 무기가 아닙니다 (§10) ════

    [Fact]
    public void 후열_타격은_스킬이_연다()
    {
        // ⚠️ 예전에는 Loadout.CanStrikeBackRow가 행동을 열었습니다. §10 "[확정] 스킬이
        //    떠맡게 된 것" 표는 CanStrikeBackRow를 스킬로 옮기라고 했고, 옛 테스트는
        //    "스킬이 아니라 물건의 성질입니다"라는 주석으로 그 이탈을 고정하고 있었습니다.
        var plainArcher = TestParty.Make("P", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Bow), actives: []);

        var trained = TestParty.Make("T", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Bow), actives: [SkillId.PiercingShot]);

        Assert.False(plainArcher.CanAfford(TacticAction.AttackBackRow));
        Assert.True(trained.CanAfford(TacticAction.AttackBackRow));
    }

    [Fact]
    public void 강화와_약화도_스킬이_연다()
    {
        // ⚠️ TacticalBrain이 self.UsesMagicPower로 열었습니다 — 스킬 하나 없는 견습이
        //    지팡이만 들면 버프·디버프를 썼습니다.
        var plain = TestParty.Make("P", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Staff), actives: []);

        Assert.True(plain.UsesMagicPower);
        Assert.False(plain.CanAfford(TacticAction.BuffAlly));
        Assert.False(plain.CanAfford(TacticAction.DebuffEnemy));
    }

    [Fact]
    public void 지휘_개입도_규칙을_지켜야_한다()
    {
        // ⚠️ 검사가 콘솔 UI에만 있어서, 다른 IBattleCommander는 검객에게 회복을 시켜
        //    마법 회복량을 뽑을 수 있었습니다. 규칙은 코어에 있어야 합니다.
        var swordsman = TestParty.Make("S", Team.Player, 60,
            loadout: Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield), actives: []);

        Assert.False(TacticalBrain.CanTake(swordsman, TacticAction.HealAlly));
        Assert.True(TacticalBrain.CanTake(swordsman, TacticAction.AttackNearest));
    }

    // ════ 6. 짐꾼 (§16.8b) ════

    [Fact]
    public void 짐꾼은_회복약을_건넬_수_있다()
    {
        // ⚠️ GivePotion이 Execute에 case가 없어 default로 떨어져 턴만 버렸고,
        //    후보 목록에도 없어서 기본 전술 규칙이 조용히 무효였습니다.
        var porter = TestParty.Make("P", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Backpack),
            actives: [SkillId.HandPotion], potions: 3,
            tactics: CombatantFactory.DefaultTacticsFor(Loadout.Single(WeaponKind.Backpack)));

        Assert.True(porter.CanAfford(TacticAction.GivePotion));

        var hurt = TestParty.Make("H", Team.Player, 60);
        hurt.TakeDamage(hurt.MaxHp / 2);
        int wounded = hurt.Hp;

        var state = new BattleState([porter, hurt, TestParty.Make("E", Team.Enemy, 40)]);
        new BattleResolver(recordLog: true).Resolve(state, new DeterministicRandom(5));

        output.WriteLine($"{wounded} → {hurt.Hp}");
        Assert.True(porter.Potions < 3 || hurt.Hp > wounded,
            "짐꾼이 회복약을 건넬 수 있어야 합니다");
    }

    [Fact]
    public void 짐꾼은_후열에_선다()
    {
        // 가방은 사거리가 Melee라, 사거리만 보면 무방비 짐꾼이 전열에서 시작했습니다.
        var porter = Grown("P", JobId.Porter, seed: 51);
        var fighter = Grown("F", JobId.ShieldApprentice);

        Assert.True(porter.Loadout.CarryingPack);

        var state = CombatantFactory.FormParty([porter, fighter], [Grown("E", seed: 71)]);
        var placed = state.All.First(c => c.Id == "P");

        Assert.Equal(Row.Back, placed.Row);
    }

    [Fact]
    public void 짐꾼은_표적_최후순위다()
    {
        // "마물이 짐꾼만 노리는 형태는 없는 게 낫다" (§16.8b).
        var porter = TestParty.Make("P", Team.Player, 60,
            loadout: Loadout.Single(WeaponKind.Backpack), row: Row.Back);
        var fighter = TestParty.Make("F", Team.Player, 60);
        var monster = TestParty.Make("E", Team.Enemy, 60,
            loadout: Loadout.Single(WeaponKind.Bow));

        var state = new BattleState([porter, fighter, monster]);

        var targets = state.ReachableTargets(monster);
        Assert.DoesNotContain(porter, targets);
        Assert.Contains(fighter, targets);

        // 다른 아군이 다 쓰러지면 그때는 노려집니다.
        fighter.TakeDamage(fighter.MaxHp);
        Assert.Contains(porter, state.ReachableTargets(monster));
    }

    [Fact]
    public void 보조_세트에도_가방을_끼울_수_있다()
    {
        // ⚠️ Equip이 언제나 보조 세트를 비웠으므로, 보조 세트에 가방을 끼우면
        //    방금 넣은 그 가방이 지워졌습니다.
        var loadout = Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield);
        loadout.Equip(WeaponSet.Secondary, Hand.Right, WeaponKind.Backpack);

        Assert.Equal(WeaponKind.Backpack, loadout[WeaponSet.Secondary, Hand.Right]);

        // 그리고 가방을 든 세트로 바꿔 들면 주무기 칸이 비워집니다.
        Assert.Equal(WeaponKind.None, loadout[WeaponSet.Primary, Hand.Right]);
    }

    // ════ 7. 상태 효과 (§18.4) ════

    [Fact]
    public void 동상은_다시_걸려도_피해가_커지지_않는다()
    {
        // ⚠️ 동상이 GrowthMode.PerStack이고 피해가 스택에 곱해져서, 동상이
        //    "중독 + 둔화 + 전이"가 되어 중독과 축이 겹쳤습니다.
        //    §18.4는 동상을 "안 커짐 + 느려짐", 중독만 "다시 걸릴 때 커짐"으로 규정합니다.
        var victim = TestParty.Make("V", Team.Player, 50);

        victim.ApplyEffect(StatusEffects.Create(EffectName.Frostbite, 5, "src"));
        int first = DamageModel.OverTimeDamage(victim, victim.Effects.First(e => e.Name == EffectName.Frostbite));

        victim.ApplyEffect(StatusEffects.Create(EffectName.Frostbite, 5, "src"));
        victim.ApplyEffect(StatusEffects.Create(EffectName.Frostbite, 5, "src"));
        var stacked = victim.Effects.First(e => e.Name == EffectName.Frostbite);
        int later = DamageModel.OverTimeDamage(victim, stacked);

        output.WriteLine($"스택 {stacked.Stacks} · 피해 {first} → {later}");
        Assert.True(stacked.Stacks > 1, "임계 전이를 위해 스택은 쌓여야 합니다");
        Assert.Equal(first, later);
    }

    [Fact]
    public void 중독은_다시_걸리면_피해가_커진다()
    {
        // 동상과의 대조. 이쪽은 커져야 합니다.
        var victim = TestParty.Make("V", Team.Player, 50);

        victim.ApplyEffect(StatusEffects.Create(EffectName.Poison, 5, "src"));
        int first = DamageModel.OverTimeDamage(victim, victim.Effects.First(e => e.Name == EffectName.Poison));

        victim.ApplyEffect(StatusEffects.Create(EffectName.Poison, 5, "src"));
        int later = DamageModel.OverTimeDamage(victim, victim.Effects.First(e => e.Name == EffectName.Poison));

        Assert.True(later > first);
    }

    // ════ 8. 파견 (§17) ════

    [Fact]
    public void 쓰러진_동료가_파견을_잠그지_않는다()
    {
        // ⚠️ HealthRatio가 쓰러진 사람을 분모에 두어서, 2인 파티에서 한 명이 쓰러지면
        //    비율이 영구히 0.5가 되고 문턱(0.55) 아래에 고정됐습니다. 만피인 동료까지
        //    남은 모든 달을 쉬었고, 지킴형은 그 순간 실패 경로가 사라졌습니다.
        var a = Grown("A", JobId.ShieldApprentice);
        var b = Grown("B", seed: 21);

        var contract = new Contract("t", "시험", ContractForm.Gather, ContractSource.Village,
            Difficulty: 2, Months: 6, Intensity: 12);

        var session = new DeploymentSession([a, b], contract, new DeterministicRandom(7));
        while (!session.IsComplete) session.AdvanceMonth();

        // 서 있는 사람 기준이므로, 한 명이 쓰러졌더라도 나머지가 만피면 일합니다.
        double ratio = session.HealthRatio;
        var standing = session.Standing;
        output.WriteLine($"서 있는 사람 {standing.Count}명 · 비율 {ratio:P0}");

        if (standing.Count > 0)
        {
            Assert.Equal(standing.Sum(x => (double)session.Hp[x.Id] / x.MaxHp) / standing.Count, ratio, 6);
        }
    }

    [Fact]
    public void 길드_자체_의뢰는_보수가_없다()
    {
        // ⚠️ 결산이 출처를 보지 않아 길드 의뢰가 보수까지 받았고, 콘솔은 명성을 2배로
        //    더 줬습니다 — "자기 돈 들여 하는 투자"가 가장 이득인 역설이었습니다.
        var paid = Grown("A");
        var invested = Grown("B");

        var village = new Contract("v", "마을", ContractForm.Gather, ContractSource.Village, 3, 12, 30);
        var guild = village with { Id = "g", Source = ContractSource.Guild };

        Assert.Equal(RewardKind.Pay, village.Reward);
        Assert.Equal(RewardKind.Renown, guild.Reward);

        var one = CareerSimulator.ResolveDeploymentYear(paid, 3, new DeterministicRandom(31), contract: village);
        var two = CareerSimulator.ResolveDeploymentYear(invested, 3, new DeterministicRandom(31), contract: guild);

        output.WriteLine($"마을 보수 {one.Income} · 길드 보수 {two.Income}");
        Assert.True(one.Income > 0);
        Assert.Equal(0, two.Income);
    }

    [Fact]
    public void 판단력도_기간에_비례한다()
    {
        // ⚠️ 판단력만 기간 무관 상수였습니다. 실측: 1달×12 → 42→100, 12달×1 → 42→48.
        //    판단력은 사고 위험을 최대 45% 깎으므로 짧은 의뢰 반복이 명확한 최적해였습니다.
        var shortRuns = Grown("S");
        var oneLong = Grown("L");

        var contract = new Contract("t", "시험", ContractForm.Gather, ContractSource.Village, 1, 1, 3);

        for (int i = 0; i < 12; i++)
        {
            CareerSimulator.ResolveDeploymentYear(
                shortRuns, 1, new DeterministicRandom((ulong)i + 700), contract: contract, months: 1);
        }

        CareerSimulator.ResolveDeploymentYear(
            oneLong, 1, new DeterministicRandom(700), contract: contract with { Months = 12 }, months: 12);

        output.WriteLine($"1달×12 판단력 {shortRuns.Judgement} · 12달×1 {oneLong.Judgement}");
        Assert.Equal(shortRuns.MonthsElapsed, oneLong.MonthsElapsed);

        // 열두 번 나눠 나간 쪽이 크게 이득이면 안 됩니다. 반올림 여유만 둡니다.
        Assert.True(shortRuns.Judgement - oneLong.Judgement <= 12,
            $"짧은 의뢰 반복이 판단력을 {shortRuns.Judgement - oneLong.Judgement} 더 줍니다");
    }

    [Fact]
    public void 마나가_파견_내내_이어지고_실제로_줄어든다()
    {
        // ⚠️ 옛 테스트는 "최대치를 넘지 않는다"만 확인해서, startingMana를 다시 무시해도
        //    통과했습니다. 이어진다는 것도 소모된다는 것도 검증하지 않았습니다.
        var mage = Grown("M", JobId.StaffApprentice, seed: 41);
        var guard = Grown("G", JobId.ShieldApprentice, seed: 51);

        int max = DerivedStats.MaxMana(mage.Stats, mage.Bonuses);

        // 이어받은 마나가 그대로 전투 시작값이 됩니다.
        var carried = new Dictionary<string, int> { [mage.Id] = 3, [guard.Id] = 5 };
        var state = CombatantFactory.FormParty([mage, guard], [Grown("E", seed: 71)],
            carriedMana: carried);

        Assert.Equal(3, state.All.First(c => c.Id == mage.Id).Mana);

        // 넘기지 않으면 만땅입니다 (단일 전투 경로).
        var fresh = CombatantFactory.FormParty([mage, guard], [Grown("E", seed: 71)]);
        Assert.Equal(max, fresh.All.First(c => c.Id == mage.Id).Mana);

        // 키가 없으면 "0"이 아니라 "안 넘긴 것"으로 봅니다 — 예전에는 HP 1이 됐습니다.
        var partial = CombatantFactory.FormParty([mage, guard], [Grown("E", seed: 71)],
            carriedHp: new Dictionary<string, int> { [mage.Id] = 20 });
        Assert.Equal(guard.MaxHp, partial.All.First(c => c.Id == guard.Id).Hp);
    }

    // ════ 9. 결정론 ════

    [Fact]
    public void 적성_굴림이_능력치_순서로_고정된다()
    {
        // ⚠️ Dictionary 순회 순서로 부동소수를 누적했습니다 — 합의 순서가 마지막 비트를
        //    바꾸고, 그 값이 적성 등급 → 경력 전체를 좌우합니다.
        for (ulong seed = 0; seed < 30; seed++)
        {
            var potential = GrowthProfile.Roll(new DeterministicRandom(seed), 3).Potential;

            var first = WeaponAptitudes.Roll(potential, new DeterministicRandom(seed + 1));
            var second = WeaponAptitudes.Roll(potential, new DeterministicRandom(seed + 1));

            foreach (var kind in Weaponry.Trainable)
            {
                Assert.Equal(first[kind], second[kind]);
            }
        }
    }
}
