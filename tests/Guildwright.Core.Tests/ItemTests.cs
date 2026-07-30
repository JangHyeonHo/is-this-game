using Guildwright.Core.Items;
using Guildwright.Core.Weapons;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 아이템 — 재질·인벤토리·상점 (docs/07 §20.3 · §20.7).
/// </summary>
public class ItemTests
{
    [Fact]
    public void 재질은_위력만_바꾼다()
    {
        var wood = new Loadout();
        wood.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Sword, WeaponMaterial.Wood);
        var iron = new Loadout();
        iron.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Sword, WeaponMaterial.Iron);
        var steel = new Loadout();
        steel.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Sword, WeaponMaterial.Steel);

        Assert.True(wood.Power < iron.Power);
        Assert.True(iron.Power < steel.Power);
        Assert.Equal(wood.Speed, iron.Speed);
        Assert.Equal(iron.Reach, steel.Reach);
    }

    [Fact]
    public void 재질_기본값은_철이라_기존_장비의_위력이_그대로다()
    {
        // 재질 도입 전의 무기는 전부 배율 1.0이었다. 기본값이 철(1.0)이 아니면
        // 기존 밸런스 측정치(docs/08)가 통째로 무효가 된다.
        var plain = Loadout.Pair(WeaponKind.Sword, WeaponKind.Shield);
        Assert.Equal(Weaponry.Of(WeaponKind.Sword).Power, plain.Power);
    }

    [Fact]
    public void 인벤토리는_꺼낸_만큼만_줄고_없는_것은_못_꺼낸다()
    {
        var armory = new Armory();
        var steelSword = new WeaponItem(WeaponKind.Sword, WeaponMaterial.Steel);

        Assert.False(armory.TryTake(steelSword));

        armory.Add(steelSword, 2);
        Assert.True(armory.TryTake(steelSword));
        Assert.Equal(1, armory.CountOf(steelSword));
        Assert.True(armory.TryTake(steelSword));
        Assert.False(armory.TryTake(steelSword));
    }

    [Fact]
    public void 상점_카탈로그는_확정된_초급_아이템뿐이다()
    {
        // §20.7 — 나무·철·강철 × 검·방패 6종, 소형 포션 2종. 여기서 벗어난 물건이
        // 카탈로그에 들어오면 스코프가 조용히 넓어진 것이다.
        Assert.Equal(6, Shop.Smithy.Count);
        Assert.All(Shop.Smithy, offer =>
            Assert.True(offer.Item.Kind is WeaponKind.Sword or WeaponKind.Shield));
        Assert.Equal(2, Shop.Apothecary.Count);
        Assert.All(Shop.Smithy, offer => Assert.True(offer.Price > 0));
        Assert.All(Shop.Apothecary, offer => Assert.True(offer.Price > 0));
    }

    [Fact]
    public void 같은_재질_같은_종류는_같은_아이템이다()
    {
        // 인벤토리가 수량으로 쌓이려면 값 동등성이 성립해야 한다.
        var a = new WeaponItem(WeaponKind.Shield, WeaponMaterial.Wood);
        var b = new WeaponItem(WeaponKind.Shield, WeaponMaterial.Wood);
        Assert.Equal(a, b);
        Assert.Equal("나무방패", a.Korean);
    }
}
