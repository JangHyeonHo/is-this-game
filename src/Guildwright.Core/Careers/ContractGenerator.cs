using Guildwright.Core.Rng;

namespace Guildwright.Core.Careers;

/// <summary>
/// 의뢰를 절차적으로 생성합니다.
/// <para>
/// 마을 사람들의 요구가 곧 의뢰입니다. 재료를 구해달라는 것도, 마물을 없애달라는 것도.
/// 성격마다 요구하는 역량이 다르므로, <b>같은 난이도라도 어떤 파티를 보낼지가 달라집니다.</b>
/// </para>
/// </summary>
public static class ContractGenerator
{
    private static readonly string[] CombatNames =
    [
        "폐광 고블린 소탕", "숲길 늑대 퇴치", "무너진 감시탑 정찰", "다리 밑 트롤 처리",
        "묘지 언데드 정화", "산적단 토벌", "채석장 골렘 파괴"
    ];

    private static readonly string[] GatheringNames =
    [
        "은광맥 채굴", "약초 채집", "철광석 운반", "버섯 동굴 수확",
        "수정 광맥 조사", "목재 벌채 호위"
    ];

    private static readonly string[] ExplorationNames =
    [
        "고대 유적 조사", "실종자 수색", "봉인된 지하실 답사", "지도 없는 동굴 탐사"
    ];

    public static Contract Generate(IRandomSource rng, int difficulty)
    {
        double roll = rng.NextDouble();

        var kind = roll switch
        {
            < 0.55 => ContractKind.Combat,
            < 0.85 => ContractKind.Gathering,
            _ => ContractKind.Exploration
        };

        string[] pool = kind switch
        {
            ContractKind.Combat => CombatNames,
            ContractKind.Gathering => GatheringNames,
            _ => ExplorationNames
        };

        string name = pool[rng.NextInt(0, pool.Length)];

        // ⚠️ 의뢰의 성격을 정하는 축은 재설계 대상입니다 — 형태 4종(토벌·지킴·수집·발견)으로
        // 바뀝니다. 지금 ContractKind 3종은 소재 분류라 그 표와 대응하지 않습니다.
        // 근거: docs/08-design-revision.md §17.3
        return new Contract(name, kind, difficulty);
    }

    /// <summary>
    /// 길드 평판에 어울리는 난이도 범위로 의뢰 게시판을 채웁니다.
    /// <para>
    /// 같은 이름의 의뢰가 한 게시판에 두 번 뜨지 않게 합니다. 이름이 겹치면 게시판을 보고
    /// 고르는 행위 자체가 헷갈립니다 — 실제로 "채석장 골렘 파괴"가 나란히 두 개 떴습니다.
    /// 이름 풀보다 게시판이 클 수도 있으므로 재시도 횟수에 상한을 둡니다.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Contract> GenerateBoard(IRandomSource rng, int count, int maxDifficulty)
    {
        const int MaxRerolls = 8;

        var board = new List<Contract>(count);
        var used = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            int difficulty = Math.Clamp(1 + rng.NextInt(0, Math.Max(1, maxDifficulty)), 1, 10);

            var contract = Generate(rng.Fork($"contract:{i}"), difficulty);
            for (int attempt = 1; attempt <= MaxRerolls && !used.Add(contract.Name); attempt++)
            {
                contract = Generate(rng.Fork($"contract:{i}:{attempt}"), difficulty);
            }

            board.Add(contract);
        }

        return board;
    }
}
