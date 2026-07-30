using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Weapons;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 피해 계산 과정을 보여주는 기능이 <b>전투 결과를 바꾸지 않는지</b> 확인합니다.
/// <para>
/// 관전 옵션이 결과에 영향을 주면 배치 시뮬레이션으로 잰 승률이 실제 플레이와 달라집니다.
/// 설명은 이미 굴린 값을 적어놓는 것일 뿐이어야 합니다.
/// </para>
/// </summary>
public class AttackExplainTests
{
    [Fact]
    public void ResolveAttack_설명을_켜도_피해와_판정이_같다()
    {
        for (ulong seed = 0; seed < 50; seed++)
        {
            var plain = Attack(seed, explain: false);
            var explained = Attack(seed, explain: true);

            Assert.Equal(plain.Damage, explained.Damage);
            Assert.Equal(plain.Evaded, explained.Evaded);
            Assert.Equal(plain.Critical, explained.Critical);
        }
    }

    [Fact]
    public void ResolveAttack_설명을_끄면_Detail이_비어있다()
    {
        Assert.Null(Attack(1, explain: false).Detail);
    }

    [Fact]
    public void ResolveAttack_설명에_위력과_방어와_변동이_들어있다()
    {
        // 회피가 나면 계산 자체가 없으므로, 맞은 판정을 하나 찾습니다.
        AttackResult hit = default;
        for (ulong seed = 0; seed < 50 && hit.Detail is null; seed++)
        {
            var r = Attack(seed, explain: true);
            if (!r.Evaded) hit = r;
        }

        Assert.NotNull(hit.Detail);
        Assert.Contains("위력", hit.Detail);
        Assert.Contains("방어", hit.Detail);
        Assert.Contains("변동", hit.Detail);
        Assert.Contains("회피", hit.Detail);
        Assert.Contains("치명타", hit.Detail);
    }

    [Fact]
    public void Resolve_설명을_켜도_전투_결과가_같다()
    {
        var plain = new BattleResolver(recordLog: true)
            .Resolve(TestParty.MirrorMatch(70, 70), new DeterministicRandom(777));

        var explained = new BattleResolver(recordLog: true, explainAttacks: true)
            .Resolve(TestParty.MirrorMatch(70, 70), new DeterministicRandom(777));

        Assert.Equal(plain.Outcome, explained.Outcome);
        Assert.Equal(plain.Rounds, explained.Rounds);

        // 설명 줄이 끼어들 뿐, 원래 있던 줄은 순서 그대로 남아 있어야 합니다.
        Assert.True(explained.Log.Count > plain.Log.Count, "설명을 켰는데 기록이 늘지 않았습니다.");
        Assert.Equal(plain.Log, explained.Log.Where(l => !l.StartsWith("      ")).ToList());
    }

    [Fact]
    public void EvasionChanceOf_방어_태세면_회피율이_오른다()
    {
        var attacker = TestParty.Make("A", Team.Player, 50);
        var defender = TestParty.Make("D", Team.Enemy, 50);

        double before = DamageModel.EvasionChanceOf(attacker, defender);
        defender.BeginDefending();
        double after = DamageModel.EvasionChanceOf(attacker, defender);

        Assert.True(after > before, $"방어 태세 회피율 {after:P1} 이 평소 {before:P1} 보다 높아야 합니다.");
    }

    private static AttackResult Attack(ulong seed, bool explain)
    {
        var attacker = TestParty.Make("A", Team.Player, 50, loadout: Loadout.Single(WeaponKind.Greatsword));
        var defender = TestParty.Make("D", Team.Enemy, 50, loadout: Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield));

        return DamageModel.ResolveAttack(attacker, defender, new DeterministicRandom(seed), explain: explain);
    }
}
