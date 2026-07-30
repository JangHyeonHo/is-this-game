namespace Guildwright.Core.Weapons;

/// <summary>어느 세트인가.</summary>
public enum WeaponSet
{
    /// <summary>지금 들고 싸우는 것.</summary>
    Primary,
    /// <summary>등에 멘 예비. 턴을 써서 바꿔 듭니다.</summary>
    Secondary
}

/// <summary>어느 손인가.</summary>
public enum Hand
{
    Right,
    Left
}

/// <summary>
/// 장착 4칸 — <b>주무기(좌·우) + 보조무기(좌·우)</b>.
/// <para>
/// 손에 무엇을 들었는지가 곧 스타일입니다. 오른손 검 + 왼손 방패면 한손+방패,
/// 양쪽 다 검이면 쌍수, 활 하나면 양손입니다. <b>스타일이라는 개념이 따로 필요 없습니다.</b>
/// </para>
/// <para>
/// 주무기 ↔ 보조무기 전환은 <b>턴을 하나 씁니다.</b> 이게 없으면
/// "액티브는 특정 무기를 요구한다"가 무의미해집니다 — 아무 때나 바꿔 들 수 있으면
/// 제약이 아니기 때문입니다.
/// </para>
/// 근거: docs/07-decisions.md §16.2b
/// </summary>
public sealed class Loadout
{
    private readonly Dictionary<(WeaponSet Set, Hand Hand), WeaponKind> _slots = new()
    {
        [(WeaponSet.Primary, Hand.Right)] = WeaponKind.None,
        [(WeaponSet.Primary, Hand.Left)] = WeaponKind.None,
        [(WeaponSet.Secondary, Hand.Right)] = WeaponKind.None,
        [(WeaponSet.Secondary, Hand.Left)] = WeaponKind.None
    };

    /// <summary>지금 들고 있는 세트.</summary>
    public WeaponSet Active { get; private set; } = WeaponSet.Primary;

    public WeaponKind this[WeaponSet set, Hand hand] => _slots[(set, hand)];

    public WeaponKind ActiveRight => _slots[(Active, Hand.Right)];
    public WeaponKind ActiveLeft => _slots[(Active, Hand.Left)];

    /// <summary>지금 들고 있는 무기들. 빈손은 빠집니다.</summary>
    public IReadOnlyList<WeaponKind> Held =>
        new[] { ActiveRight, ActiveLeft }.Where(k => k != WeaponKind.None).ToArray();

    /// <summary>
    /// 주된 물건 — <b>숙련도와 전투 효율의 기준</b>입니다.
    /// <para>
    /// 위력이 가장 큰 것을 고르되, 때릴 수 있는 것이 하나도 없으면(방패만, 가방만)
    /// <b>들고 있는 것 중 첫 번째</b>를 씁니다. 그래야 방패술과 짐 다루기처럼
    /// 때리지 않는 물건의 숙련도가 쌓입니다.
    /// </para>
    /// <para>정말 빈손일 때만 <see cref="WeaponKind.None"/>입니다.</para>
    /// </summary>
    public WeaponKind MainWeapon
    {
        get
        {
            // 오른손을 먼저 봅니다 — 동률이면 순서가 결과를 정하므로 고정해야 합니다.
            var best = WeaponKind.None;
            double bestPower = 0.0;

            foreach (var kind in new[] { ActiveRight, ActiveLeft })
            {
                double power = Weaponry.Of(kind).Power;
                if (power > bestPower)
                {
                    bestPower = power;
                    best = kind;
                }
            }

            if (best != WeaponKind.None) return best;

            // 때릴 수 있는 게 없으면 든 것 중 첫 번째 (방패 · 가방).
            if (ActiveRight != WeaponKind.None) return ActiveRight;
            return ActiveLeft;
        }
    }

    /// <summary>가방을 들고 있는가. <b>그러면 무방비입니다.</b></summary>
    public bool CarryingPack =>
        ActiveRight == WeaponKind.Backpack || ActiveLeft == WeaponKind.Backpack;

    /// <summary>적재량. 가방을 몇 칸에 들었느냐로 정해집니다.</summary>
    public int Load => Held.Sum(k => Weaponry.Of(k).Load);

    /// <summary>때릴 수 있는 것을 하나도 안 들었는가. <b>가방을 든 짐꾼이 여기 해당합니다.</b></summary>
    public bool Unarmed => Power <= 0.0;

    // ---- 합쳐진 명세 ----

    /// <summary>
    /// 위력 배율. <b>주손 + 보조손 × 0.5</b>입니다.
    /// <para>
    /// 쌍수가 이득인 이유가 여기 있습니다. 방패나 가방은 위력이 0이므로 더해도 그대로입니다.
    /// </para>
    /// </summary>
    public double Power
    {
        get
        {
            // 값이 아니라 칸으로 가릅니다 — 쌍수처럼 같은 무기를 두 자루 들면
            // 값으로 빼면 둘 다 빠져버립니다.
            double right = Weaponry.Of(ActiveRight).Power;
            double left = Weaponry.Of(ActiveLeft).Power;

            double main = Math.Max(right, left);
            double off = Math.Min(right, left);

            return main + off * OffHandRatio;
        }
    }

    /// <summary>보조손 위력 기여율.</summary>
    public const double OffHandRatio = 0.5;

    /// <summary>속도 배율. 든 것들의 평균입니다 — 무거운 걸 하나 끼면 전체가 느려집니다.</summary>
    public double Speed
    {
        get
        {
            var held = Held;
            if (held.Count == 0) return Weaponry.Of(WeaponKind.None).Speed;

            // 순서 의존을 막기 위해 합을 먼저 내고 마지막에 한 번 나눕니다.
            double sum = held.Sum(k => Weaponry.Of(k).Speed);
            return sum / held.Count;
        }
    }

    /// <summary>사거리. 든 것 중 가장 먼 것입니다.</summary>
    public Reach Reach =>
        Held.Count == 0 ? Reach.Melee : Held.Max(k => Weaponry.Of(k).Reach);

    /// <summary>주된 무기가 마법 위력을 쓰는가.</summary>
    public bool UsesMagicPower => Weaponry.Of(MainWeapon).UsesMagicPower;

    public bool CanStrikeBackRow => Reach == Reach.Ranged;
    public bool CanActFromBackRow => Reach is Reach.Extended or Reach.Ranged;

    /// <summary>그 무기를 지금 들고 있는가. <b>액티브 스킬의 무기 요구를 판정합니다.</b></summary>
    public bool Holding(WeaponKind kind) => ActiveRight == kind || ActiveLeft == kind;

    // ---- 변경 ----

    /// <summary>
    /// 무기를 끼웁니다.
    /// <para>
    /// 양손 무기는 그 세트의 다른 칸을 비웁니다. 가방을 주무기에 끼우면
    /// <b>보조무기 칸도 비웁니다</b> — 짐꾼은 전환할 무기를 가질 수 없습니다.
    /// </para>
    /// </summary>
    public void Equip(WeaponSet set, Hand hand, WeaponKind kind)
    {
        var spec = Weaponry.Of(kind);

        _slots[(set, hand)] = kind;

        if (spec.Hands == Hands.Two)
        {
            _slots[(set, Other(hand))] = WeaponKind.None;
        }

        // 가방을 들면 전환할 무기를 가질 수 없습니다. 비우는 것은 <b>반대 세트</b>입니다 —
        // 예전에는 언제나 보조 세트를 비웠으므로, 보조 세트에 가방을 끼우면 방금 넣은
        // 그 가방이 지워졌습니다. 예외도 안 나고 조용히 사라지는 종류의 버그였습니다.
        if (kind == WeaponKind.Backpack)
        {
            var other = set == WeaponSet.Primary ? WeaponSet.Secondary : WeaponSet.Primary;
            _slots[(other, Hand.Right)] = WeaponKind.None;
            _slots[(other, Hand.Left)] = WeaponKind.None;
        }
    }

    /// <summary>주무기와 보조무기를 바꿔 듭니다. <b>턴을 하나 씁니다.</b></summary>
    public void Switch() =>
        Active = Active == WeaponSet.Primary ? WeaponSet.Secondary : WeaponSet.Primary;

    /// <summary>전환할 무기가 있는가. 가방을 들었으면 없습니다.</summary>
    public bool CanSwitch
    {
        get
        {
            if (CarryingPack) return false;

            var other = Active == WeaponSet.Primary ? WeaponSet.Secondary : WeaponSet.Primary;
            return _slots[(other, Hand.Right)] != WeaponKind.None
                || _slots[(other, Hand.Left)] != WeaponKind.None;
        }
    }

    private static Hand Other(Hand hand) => hand == Hand.Right ? Hand.Left : Hand.Right;

    /// <summary>한 손에 무기 하나만 든 간단한 구성. 테스트와 기본값용.</summary>
    public static Loadout Single(WeaponKind kind)
    {
        var loadout = new Loadout();
        loadout.Equip(WeaponSet.Primary, Hand.Right, kind);
        return loadout;
    }

    /// <summary>양손에 각각. 쌍수나 한손+방패를 만들 때 씁니다.</summary>
    public static Loadout Pair(WeaponKind right, WeaponKind left)
    {
        var loadout = new Loadout();
        loadout.Equip(WeaponSet.Primary, Hand.Right, right);
        loadout.Equip(WeaponSet.Primary, Hand.Left, left);
        return loadout;
    }

    public override string ToString()
    {
        var held = Held;
        if (held.Count == 0) return "빈손";
        return string.Join("+", held.Select(k => k.ToKorean()));
    }
}
