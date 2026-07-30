using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 상태 효과를 <b>이름이 아니라 기전으로</b> 만든 결과를 지킵니다.
/// <para>
/// 예전에는 열거형에 이름을 나열했습니다. 그러면 공격·방어·명중·회피·속도에
/// 강화와 약화를 두는 것만으로 10종이 되는데, 전부 "무엇을 얼마나 몇 라운드"라는
/// 같은 모양입니다. <b>폭발하는 건 이름이지 기전이 아닙니다.</b>
/// </para>
/// 근거: docs/08-design-revision.md §18
/// </summary>
public class StatusEffectMechanismTests(ITestOutputHelper output)
{
    private static Combatant Subject() => TestParty.Make("T", Team.Player, 60);

    // ---- 기전이 늘어나지 않는지 ----

    [Fact]
    public void 기전은_여덟_종이다()
    {
        // 이름은 얼마든지 늘어도 되지만 기전이 늘면 코드 경로가 늡니다.
        // 늘리기 전에 "기존 기전으로 표현할 수 없는가"를 먼저 확인하세요.
        Assert.Equal(8, Enum.GetValues<EffectMechanism>().Length);
    }

    [Fact]
    public void 모든_이름이_기전과_설정을_가진다()
    {
        // 표에서 빠진 이름이 있으면 조회 시점에 터집니다. 시작할 때 잡습니다.
        foreach (var name in Enum.GetValues<EffectName>())
        {
            var profile = StatusEffects.ProfileOf(name);
            Assert.Equal(name, profile.Name);
            Assert.False(string.IsNullOrWhiteSpace(profile.Korean), $"{name}에 표시 이름이 없습니다.");
        }
    }

    // ---- 수치 증감 ----

    [Fact]
    public void 강화와_약화가_같은_기전으로_반대_방향으로_움직인다()
    {
        var up = Subject();
        var down = Subject();

        up.ApplyEffect(StatusEffects.Create(EffectName.PowerUp, 3));
        down.ApplyEffect(StatusEffects.Create(EffectName.PowerDown, 3));

        Assert.True(up.EffectivePhysicalPower > up.BasePhysicalPower);
        Assert.True(down.EffectivePhysicalPower < down.BasePhysicalPower);
    }

    [Fact]
    public void 증감은_덧셈으로_모아_한_번_곱한다()
    {
        // 곱셈을 누적하면 적용 순서에 따라 부동소수점 끝자리가 달라져
        // 배치 시뮬레이션 재현성이 깨집니다.
        var a = Subject();
        var b = Subject();

        a.ApplyEffect(StatusEffects.Create(EffectName.GuardUp, 3));
        a.ApplyEffect(StatusEffects.Create(EffectName.GuardDown, 3));

        b.ApplyEffect(StatusEffects.Create(EffectName.GuardDown, 3));
        b.ApplyEffect(StatusEffects.Create(EffectName.GuardUp, 3));

        Assert.Equal(a.EffectivePhysicalGuard, b.EffectivePhysicalGuard);

        // 같은 세기의 강화와 약화는 상쇄됩니다 (덧셈이므로).
        Assert.Equal(a.BasePhysicalGuard, a.EffectivePhysicalGuard);
    }

    [Fact]
    public void 명중과_회피도_같은_기전에_얹힌다()
    {
        // 이름을 늘렸을 뿐 코드 경로는 그대로여야 합니다.
        var c = Subject();

        Assert.Equal(1.0, c.AccuracyFactor);

        c.ApplyEffect(StatusEffects.Create(EffectName.AccuracyDown, 2));
        Assert.True(c.AccuracyFactor < 1.0);

        double before = c.EvasionChance;
        c.ApplyEffect(StatusEffects.Create(EffectName.EvasionUp, 2));
        Assert.True(c.EvasionChance > before);
    }

    // ---- 지속 피해 넷의 차이 ----

    [Fact]
    public void 중독은_다시_걸리면_쌓인다()
    {
        var c = Subject();

        c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 5));
        c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 5));
        c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 5));

        Assert.Single(c.Effects);
        Assert.Equal(3, c.Effects[0].Stacks);
    }

    [Fact]
    public void 화상은_다시_걸려도_덮어쓴다()
    {
        var c = Subject();

        c.ApplyEffect(StatusEffects.Create(EffectName.Burn, 5));
        c.ApplyEffect(StatusEffects.Create(EffectName.Burn, 5));

        Assert.Equal(1, c.Effects[0].Stacks);
    }

    [Fact]
    public void 출혈은_행동할_때마다_커진다()
    {
        // 방치하면 비싸지므로 "지금 지혈할까"가 매 턴 판단이 됩니다.
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Bleed, 9));

        int before = c.Effects[0].Stacks;
        c.GrowOnAction();
        c.GrowOnAction();

        output.WriteLine($"출혈 {before} → {c.Effects[0].Stacks}");
        Assert.True(c.Effects[0].Stacks > before);
    }

    [Fact]
    public void 출혈이_아닌_지속피해는_행동으로_커지지_않는다()
    {
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Burn, 9));

        c.GrowOnAction();
        c.GrowOnAction();

        Assert.Equal(1, c.Effects[0].Stacks);
    }

    [Fact]
    public void 스택에는_상한이_있다()
    {
        // 없으면 긴 전투에서 그냥 죽습니다.
        var c = Subject();

        for (int i = 0; i < 50; i++) c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 5));

        Assert.Equal(StatusEffects.MaxStacks, c.Effects[0].Stacks);
    }

    [Fact]
    public void 동상은_속도_저하를_동반한다()
    {
        var c = Subject();
        double before = c.EffectiveSpeed;

        c.ApplyEffect(StatusEffects.Create(EffectName.Frostbite, 5));

        Assert.True(c.HasEffect(EffectName.SpeedDown), "동상에 둔화가 따라오지 않았습니다.");
        Assert.True(c.EffectiveSpeed < before);
    }

    [Fact]
    public void 동상이_쌓이면_빙결로_전이하고_동상은_사라진다()
    {
        // 파국이자 리셋입니다. 완전히 절망적이지 않아야 합니다.
        var c = Subject();
        int threshold = StatusEffects.ProfileOf(EffectName.Frostbite).TransitionThreshold;

        for (int i = 0; i < threshold; i++) c.ApplyEffect(StatusEffects.Create(EffectName.Frostbite, 5));

        var moved = c.ResolveTransition();

        Assert.Equal(EffectName.Freeze, moved);
        Assert.True(c.HasEffect(EffectName.Freeze));
        Assert.False(c.HasEffect(EffectName.Frostbite));
        Assert.False(c.HasEffect(EffectName.SpeedDown), "동상이 풀렸는데 동반 둔화가 남았습니다.");
    }

    [Fact]
    public void 임계_전이는_동상만_설정되어_있다()
    {
        // 특정 효과의 특권이 아니라 매개변수입니다. 다만 지금 값을 채운 것은 동상뿐입니다 —
        // "중독이 쌓이면 마비 아닌가" 같은 요구가 와도 일관성이 깨지지 않게.
        var withTransition = StatusEffects.Catalogue
            .Where(p => p.TransitionsTo is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Equal([EffectName.Frostbite], withTransition);
    }

    // ---- 확률적 행동 불가 ----

    [Fact]
    public void 마비_빙결_석화는_확률만_다른_한_기전이다()
    {
        foreach (var name in (EffectName[])[EffectName.Paralysis, EffectName.Freeze, EffectName.Petrify])
        {
            Assert.Equal(EffectMechanism.Incapacitate, StatusEffects.ProfileOf(name).Mechanism);
        }

        double paralysis = StatusEffects.ProfileOf(EffectName.Paralysis).BlockChance;
        double freeze = StatusEffects.ProfileOf(EffectName.Freeze).BlockChance;

        Assert.True(paralysis is > 0.0 and < 1.0, "마비는 가끔 막혀야 합니다.");
        Assert.Equal(1.0, freeze);
    }

    [Fact]
    public void 여러_행동불가가_겹치면_가장_높은_확률을_쓴다()
    {
        // 곱하면 적용 순서에 의존하게 됩니다.
        var c = Subject();

        c.ApplyEffect(StatusEffects.Create(EffectName.Paralysis, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Petrify, 3));

        Assert.Equal(1.0, c.IncapacitateChance);
    }

    // ---- 지시 불통 ----

    [Fact]
    public void 공포와_혼란은_지시를_막는다()
    {
        // 지휘에 횟수 제한이 없는 대신 이것이 유일한 제약입니다.
        var normal = Subject();
        Assert.True(normal.AcceptsOrders);

        foreach (var name in (EffectName[])[EffectName.Fear, EffectName.Confusion])
        {
            var c = Subject();
            c.ApplyEffect(StatusEffects.Create(name, 2));
            Assert.False(c.AcceptsOrders, $"{name}에 걸렸는데 지시가 통합니다.");
        }
    }

    [Fact]
    public void 마비는_지시를_막지_않는다()
    {
        // 마비는 "듣고도 못 하는 것"이라 지시 불통과 성격이 다릅니다.
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Paralysis, 3));

        Assert.True(c.AcceptsOrders);
        Assert.True(c.IncapacitateChance > 0.0);
    }

    // ---- 행동 종류 제한 ----

    [Fact]
    public void 속박은_이동만_막고_침묵은_마나스킬만_막는다()
    {
        var bound = Subject();
        bound.ApplyEffect(StatusEffects.Create(EffectName.Bind, 2));
        Assert.True(bound.IsRestricted(ActionRestriction.Movement));
        Assert.False(bound.IsRestricted(ActionRestriction.ManaSkills));

        var silenced = Subject();
        silenced.ApplyEffect(StatusEffects.Create(EffectName.Silence, 2));
        Assert.True(silenced.IsRestricted(ActionRestriction.ManaSkills));
        Assert.False(silenced.IsRestricted(ActionRestriction.Movement));
    }

    // ---- 방벽과 회복 ----

    [Fact]
    public void 보호막이_HP보다_먼저_깎인다()
    {
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Barrier, 3));

        int shield = c.Barrier;
        Assert.True(shield > 0);

        c.TakeDamage(shield - 1);
        Assert.Equal(c.MaxHp, c.Hp);
        Assert.Equal(1, c.Barrier);
    }

    [Fact]
    public void 보호막이_다_깎이면_사라진다()
    {
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Barrier, 3));

        c.TakeDamage(c.Barrier + 5);

        Assert.Equal(0, c.Barrier);
        Assert.False(c.HasEffect(EffectName.Barrier));
        Assert.Equal(c.MaxHp - 5, c.Hp);
    }

    [Fact]
    public void 저주는_회복량을_줄인다()
    {
        var cursed = Subject();
        var healthy = Subject();

        cursed.TakeDamage(40);
        healthy.TakeDamage(40);
        cursed.ApplyEffect(StatusEffects.Create(EffectName.Curse, 5));

        cursed.Heal(20);
        healthy.Heal(20);

        output.WriteLine($"저주 {cursed.Hp} vs 정상 {healthy.Hp}");
        Assert.True(cursed.Hp < healthy.Hp);
    }

    // ---- 전투가 끝났을 때 ----

    [Fact]
    public void 상처는_남고_상태는_풀린다()
    {
        var c = Subject();

        // 상처
        c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Bleed, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Petrify, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Curse, 3));
        // 상태
        c.ApplyEffect(StatusEffects.Create(EffectName.PowerUp, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Fear, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Taunt, 3));

        c.EndBattle();

        Assert.True(c.HasEffect(EffectName.Poison));
        Assert.True(c.HasEffect(EffectName.Bleed));
        Assert.True(c.HasEffect(EffectName.Petrify));
        Assert.True(c.HasEffect(EffectName.Curse));

        Assert.False(c.HasEffect(EffectName.PowerUp));
        Assert.False(c.HasEffect(EffectName.Fear));
        Assert.False(c.HasEffect(EffectName.Taunt));
    }

    [Fact]
    public void 남는_효과는_지속시간으로_사라지지_않는다()
    {
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 1));

        for (int i = 0; i < 20; i++) c.TickEffects();

        Assert.True(c.HasEffect(EffectName.Poison), "치료 없이 중독이 저절로 나았습니다.");
    }

    // ---- 치료 ----

    [Fact]
    public void 치료제는_해당_상처만_푼다()
    {
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Bleed, 3));

        Assert.Equal(1, c.Cure(CureItem.Antidote));

        Assert.False(c.HasEffect(EffectName.Poison));
        Assert.True(c.HasEffect(EffectName.Bleed));
    }

    [Fact]
    public void 성수는_석화와_저주_둘을_푼다()
    {
        var c = Subject();
        c.ApplyEffect(StatusEffects.Create(EffectName.Petrify, 3));
        c.ApplyEffect(StatusEffects.Create(EffectName.Curse, 3));

        Assert.Equal(2, c.Cure(CureItem.HolyWater));
        Assert.Empty(c.Effects);
    }

    [Fact]
    public void 이로운_효과에는_치료_수단이_붙지_않는다()
    {
        // 해독·정화가 아군 강화까지 지우면 안 됩니다.
        foreach (var profile in StatusEffects.Catalogue.Where(p => p.Beneficial))
        {
            Assert.Equal(CureItem.None, profile.Cure);
        }
    }

    [Fact]
    public void 지속_피해는_모두_치료_수단이_있다()
    {
        // 풀 방법이 없는 상처가 있으면 그 캐릭터는 파견 내내 회복 불가입니다.
        foreach (var profile in StatusEffects.Catalogue
            .Where(p => p.Mechanism == EffectMechanism.DamageOverTime))
        {
            Assert.NotEqual(CureItem.None, profile.Cure);
        }
    }

    // ---- 결정론 ----

    [Fact]
    public void 같은_시드로_두_번_돌리면_결과가_같다()
    {
        static (int Hp, int Stacks) Run(ulong seed)
        {
            var c = TestParty.Make("D", Team.Player, 60);
            var rng = new DeterministicRandom(seed);

            for (int round = 0; round < 6; round++)
            {
                if (rng.Chance(0.5)) c.ApplyEffect(StatusEffects.Create(EffectName.Poison, 4));
                if (rng.Chance(0.5)) c.ApplyEffect(StatusEffects.Create(EffectName.Bleed, 4));
                c.GrowOnAction();

                foreach (var effect in c.Effects.ToList())
                {
                    if (effect.Mechanism == EffectMechanism.DamageOverTime)
                        c.TakeDamage(DamageModel.OverTimeDamage(c, effect));
                }

                c.TickEffects();
            }

            return (c.Hp, c.Effects.Sum(e => e.Stacks));
        }

        Assert.Equal(Run(777), Run(777));
    }
}
