namespace Guildwright.Core.Careers;

/// <summary>의뢰의 성격.</summary>
public enum ContractKind
{
    /// <summary>토벌·호위 등. 전투력이 성패를 좌우합니다.</summary>
    Combat,
    /// <summary>채집·채광. 마을의 재료 요구에 응하는 의뢰로, 전투 비중이 낮습니다.</summary>
    Gathering,
    /// <summary>탐색·정찰. 위험하지만 전투보다 정보와 판단이 중요합니다.</summary>
    Exploration
}

/// <summary>
/// 길드가 받는 의뢰.
/// <para>
/// <b>난이도 하나로 끝나지 않고 "어떤 역량이 유리한가"를 함께 갖습니다.</b>
/// 이게 있어야 파티 편성이 전투력 순으로 줄 세우기가 아니게 됩니다.
/// </para>
/// <para>
/// 던전 탐험 층을 따로 만들지 않고, 함정·척후·운반의 효과를 여기서 흡수합니다.
/// 근거: docs/04-game-design.md §5.8
/// </para>
/// </summary>
/// <param name="Name">표시용 이름.</param>
/// <param name="Kind">성격.</param>
/// <param name="Difficulty">난이도. 보수와 위험을 좌우합니다.</param>
public sealed record Contract(
    string Name,
    ContractKind Kind,
    int Difficulty)
{
    public static Contract Combat(string name, int difficulty) =>
        new(name, ContractKind.Combat, difficulty);

    /// <summary>
    /// 전투 비중.
    /// <para>
    /// 채집 의뢰는 전투가 거의 없으므로, <b>전투력이 낮은 캐릭터도 제 몫을 할 자리</b>가 됩니다.
    /// </para>
    /// </summary>
    public double CombatWeight => Kind switch
    {
        ContractKind.Combat => 1.0,
        ContractKind.Exploration => 0.6,
        ContractKind.Gathering => 0.25,
        _ => 1.0
    };

    public override string ToString() => $"[{Name}] 난이도 {Difficulty} · {Kind}";
}
