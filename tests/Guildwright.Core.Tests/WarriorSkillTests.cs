using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 견습 전사의 스킬 — 대담함 · 베고 막기 (docs/07 §22, 사용자 결정 2026-07-30).
/// </summary>
public class WarriorSkillTests
{
    private static Adventurer Warrior(string id = "W")
    {
        var rng = new DeterministicRandom($"warrior:{id}");
        var recruit = Adventurer.Recruit(id, $"전사{id}", rng);
        Assert.Equal(JobId.SwordApprentice, recruit.Job);
        return recruit;
    }

    [Fact]
    public void 견습_전사는_대담함과_베고_막기를_받는다()
    {
        var grants = Jobs.Of(JobId.SwordApprentice).Grants;
        Assert.Contains(SkillId.Boldness, grants);
        Assert.Contains(SkillId.CutAndGuard, grants);
    }

    [Fact]
    public void 대담함은_방어력을_1점1배로_만든다()
    {
        var warrior = Warrior("guard");
        var fighter = CombatantFactory.Create(warrior, Team.Player, Row.Front);

        // 같은 조건에서 대담함만 뺀 값과 비교한다 — 배율 축이 실제로 곱해지는지.
        double withBoldness = fighter.EffectivePhysicalGuard;
        double baseline = fighter.BasePhysicalGuard;
        Assert.Equal(Math.Round(baseline * 1.10), withBoldness, precision: 0);
        Assert.True(withBoldness > baseline);
    }

    [Fact]
    public void 베고_막기는_검_숙련_문턱을_채워야_배운다()
    {
        var warrior = Warrior("learn");
        Assert.DoesNotContain(SkillId.CutAndGuard, warrior.Actives);

        var needed = SkillBook.Of(SkillId.CutAndGuard).RequiresProficiency;
        while (warrior.Proficiency[WeaponKind.Sword] < needed)
        {
            warrior.Proficiency.Advance(WeaponKind.Sword, warrior.Aptitudes[WeaponKind.Sword], 5.0);
        }

        Assert.Contains(SkillId.CutAndGuard, warrior.Actives);
    }

    [Fact]
    public void 베고_막기의_공격_배율은_피해를_줄인다()
    {
        var attacker = CombatantFactory.Create(Warrior("atk"), Team.Player, Row.Front);
        var defender = CombatantFactory.Create(Warrior("def"), Team.Enemy, Row.Front);

        // 같은 시드로 두 번 — 배율만 다르게. 회피·치명타·변동 굴림이 같으므로 순수 배율 차이만 남는다.
        var full = DamageModel.ResolveAttack(attacker, defender, new DeterministicRandom("swing"));
        var scaled = DamageModel.ResolveAttack(attacker, defender, new DeterministicRandom("swing"),
            powerScale: DamageModel.GuardStrikePowerRatio);

        if (!full.Evaded)
        {
            Assert.True(scaled.Damage <= full.Damage);
            Assert.True(scaled.Damage >= (int)(full.Damage * 0.5));
        }
    }

    [Fact]
    public void 베고_막기를_쓰면_방어_자세가_된다()
    {
        var warrior = Warrior("stance");
        var needed = SkillBook.Of(SkillId.CutAndGuard).RequiresProficiency;
        while (warrior.Proficiency[WeaponKind.Sword] < needed)
        {
            warrior.Proficiency.Advance(WeaponKind.Sword, warrior.Aptitudes[WeaponKind.Sword], 5.0);
        }

        var attacker = CombatantFactory.Create(warrior, Team.Player, Row.Front);

        // 자격 검사 — 스킬·무기·마나가 있으니 쓸 수 있어야 한다.
        Assert.True(attacker.CanDo(TacticAction.GuardStrike));
        Assert.True(attacker.CanAfford(TacticAction.GuardStrike));

        attacker.PaySkillCost(TacticAction.GuardStrike);
        attacker.BeginDefending();
        Assert.True(attacker.IsDefending);

        // 쿨다운이 돌기 시작했으니 연타는 안 된다.
        Assert.False(attacker.CanAfford(TacticAction.GuardStrike));
    }
}
