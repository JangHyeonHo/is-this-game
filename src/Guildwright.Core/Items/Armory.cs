using Guildwright.Core.Weapons;

namespace Guildwright.Core.Items;

/// <summary>
/// 길드 인벤토리 — 창고에 쌓인 장비와 소모품 (docs/07 §20.3 · §20.7).
/// <para>
/// 단원이 장착 중인 것은 여기 없다 — 장착은 창고에서 꺼내는 것이고,
/// 벗기면 창고로 돌아온다. 목록은 항상 같은 순서로 나온다 (결정론).
/// </para>
/// </summary>
public sealed class Armory
{
    private readonly Dictionary<WeaponItem, int> _weapons = new();
    private readonly Dictionary<ConsumableKind, int> _consumables = new();

    /// <summary>보유 장비 — (아이템, 수량). 종류·재질 순으로 정렬된다.</summary>
    public IReadOnlyList<(WeaponItem Item, int Count)> Weapons =>
        _weapons.Where(p => p.Value > 0)
            .OrderBy(p => p.Key.Kind).ThenBy(p => p.Key.Material)
            .Select(p => (p.Key, p.Value))
            .ToArray();

    /// <summary>보유 소모품 — (종류, 수량). 종류 순으로 정렬된다.</summary>
    public IReadOnlyList<(ConsumableKind Item, int Count)> Consumables =>
        _consumables.Where(p => p.Value > 0)
            .OrderBy(p => p.Key)
            .Select(p => (p.Key, p.Value))
            .ToArray();

    public int CountOf(WeaponItem item) => _weapons.GetValueOrDefault(item, 0);
    public int CountOf(ConsumableKind kind) => _consumables.GetValueOrDefault(kind, 0);

    public void Add(WeaponItem item, int count = 1) =>
        _weapons[item] = CountOf(item) + count;

    public void Add(ConsumableKind kind, int count = 1) =>
        _consumables[kind] = CountOf(kind) + count;

    /// <summary>하나 꺼낸다. 없으면 false — 없는 것을 장착하는 버그를 여기서 막는다.</summary>
    public bool TryTake(WeaponItem item)
    {
        if (CountOf(item) <= 0) return false;
        _weapons[item] = CountOf(item) - 1;
        return true;
    }

    public bool TryTake(ConsumableKind kind)
    {
        if (CountOf(kind) <= 0) return false;
        _consumables[kind] = CountOf(kind) - 1;
        return true;
    }
}
