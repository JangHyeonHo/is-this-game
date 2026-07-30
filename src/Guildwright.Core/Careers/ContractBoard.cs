using Guildwright.Core.Parties;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Careers;

/// <summary>
/// 계절. <b>확률 가중치일 뿐입니다.</b>
/// <para>
/// ⚠️ "이 계절엔 훈련만" "이 계절엔 실전만" 같은 개념이 <b>아닙니다.</b> 훈련이냐 실전이냐는
/// 항상 열려 있는 선택이고, 계절은 의뢰 풀의 경향만 바꿉니다. 확정 규칙으로 두면
/// 플레이어가 계절표를 외워 최적화합니다.
/// </para>
/// 근거: docs/08-design-revision.md §17.9
/// </summary>
public enum Season
{
    /// <summary>1~3월. 월동에서 깬 마물이 많음. 농사 준비 보호, 마을 보수공사.</summary>
    Spring,

    /// <summary>4~6월. 마물 활동이 상대적으로 잠잠. 보수 계열이 많음.</summary>
    Summer,

    /// <summary>7~9월. 수확 지원, 월동 대책 준비, 채집.</summary>
    Autumn,

    /// <summary>10~12월. <b>[검토중]</b> — 굶주린 마물이 내려온다는 해석도, 내부 보수공사도 가능.</summary>
    Winter
}

/// <summary>달과 계절.</summary>
public static class Calendar
{
    public const int MonthsPerYear = 12;

    /// <summary>1월 시작, 3개월씩 4계절.</summary>
    public static Season SeasonOf(int month) => ((month - 1) / 3) switch
    {
        0 => Season.Spring,
        1 => Season.Summer,
        2 => Season.Autumn,
        _ => Season.Winter
    };

    public static string ToKorean(this Season season) => season switch
    {
        Season.Spring => "봄",
        Season.Summer => "여름",
        Season.Autumn => "가을",
        _ => "겨울"
    };

    /// <summary>매년 1월에 길드원 모집이 열립니다 (§17.10).</summary>
    public static bool IsRecruitmentMonth(int month) => month == 1;
}

/// <summary>
/// 의뢰 게시판 — <b>매달 랜덤으로 발생</b>합니다.
/// <para>
/// 고정된 개수가 없고, <b>길드 랭크가 양과 질을 동시에 조절</b>합니다. 랭크가 오르면
/// 뜨는 확률이 늘고 더 높은 난이도가 나옵니다. <b>감당 못 할 의뢰는 아예 뜨지 않으므로</b>
/// 랭크 상승의 체감이 "새로운 게 보이기 시작한다"로 옵니다.
/// </para>
/// <para>
/// <b>그 달에 수락하지 않으면 사라집니다</b> — 단 <see cref="Contract.Persists"/>인 것
/// (승급 의뢰 · 전개상 필수 의뢰)은 남습니다.
/// </para>
/// <para>
/// ⚠️ 발생 개수·확률은 <b>임시값</b>입니다. 루즈함과 밸런스를 보고 조절합니다.
/// </para>
/// 근거: docs/08-design-revision.md §17.8, §17.9, §17.10
/// </summary>
public static class ContractBoard
{
    /// <summary>랭크별 한 달 발생 기대 건수. ⚠️ 임시값.</summary>
    public static double ExpectedCountAt(Rank guildRank) => 2.0 + (int)guildRank * 1.2;

    /// <summary>
    /// 랭크별 난이도 상한. <b>이 위는 아예 뜨지 않습니다.</b> ⚠️ 임시값.
    /// </summary>
    public static int MaxDifficultyAt(Rank guildRank) => 2 + (int)guildRank * 2;

    /// <summary>
    /// 랭크별 최소 난이도. 랭크가 오르면 시시한 의뢰가 줄어듭니다 — 하한선도 같이 오릅니다.
    /// </summary>
    public static int MinDifficultyAt(Rank guildRank) => Math.Max(1, (int)guildRank - 1);

    /// <summary>
    /// 계절별 형태 가중치. <b>강제가 아니라 경향</b>입니다.
    /// <para>겨울은 <b>[검토중]</b>이므로 다른 계절의 평균에 가깝게 둡니다 — 성격이 정해지면 바꿉니다.</para>
    /// </summary>
    public static IReadOnlyDictionary<ContractForm, double> WeightsIn(Season season) => season switch
    {
        // 봄: 월동에서 깬 마물 + 농사 준비 보호 + 보수공사
        Season.Spring => new Dictionary<ContractForm, double>
        {
            [ContractForm.Subjugate] = 0.35,
            [ContractForm.Defend] = 0.35,
            [ContractForm.Gather] = 0.20,
            [ContractForm.Discover] = 0.10
        },

        // 여름: 마물이 잠잠하고 보수 계열이 많음
        Season.Summer => new Dictionary<ContractForm, double>
        {
            [ContractForm.Subjugate] = 0.20,
            [ContractForm.Defend] = 0.40,
            [ContractForm.Gather] = 0.25,
            [ContractForm.Discover] = 0.15
        },

        // 가을: 수확 지원과 채집
        Season.Autumn => new Dictionary<ContractForm, double>
        {
            [ContractForm.Subjugate] = 0.25,
            [ContractForm.Defend] = 0.30,
            [ContractForm.Gather] = 0.35,
            [ContractForm.Discover] = 0.10
        },

        // 겨울: [검토중]. 지금은 어느 쪽으로도 기울이지 않습니다.
        _ => new Dictionary<ContractForm, double>
        {
            [ContractForm.Subjugate] = 0.27,
            [ContractForm.Defend] = 0.33,
            [ContractForm.Gather] = 0.25,
            [ContractForm.Discover] = 0.15
        }
    };

    /// <summary>기간 후보. 1달이 기본이고 2~3달, 드물게 1년짜리가 있습니다 (§17.4).</summary>
    private static readonly (int Months, double Weight)[] Durations =
    [
        (1, 0.55), (2, 0.22), (3, 0.15), (Calendar.MonthsPerYear, 0.08)
    ];

    /// <summary>
    /// 그 달의 게시판을 만듭니다.
    /// </summary>
    /// <param name="rng">난수원.</param>
    /// <param name="month">1~12. 계절 가중치에 씁니다.</param>
    /// <param name="guildRank">길드 랭크. 양과 질을 동시에 조절합니다.</param>
    /// <param name="carriedOver">
    /// 지난 달에서 넘어온 지속 의뢰. 안 받아도 사라지지 않는 것들입니다.
    /// </param>
    public static IReadOnlyList<Contract> Post(
        IRandomSource rng,
        int month,
        Rank guildRank,
        IReadOnlyList<Contract>? carriedOver = null)
    {
        var season = Calendar.SeasonOf(month);
        var weights = WeightsIn(season);

        // 기대 건수를 중심으로 흔듭니다 — 고정 개수가 아니어야 매달이 다릅니다.
        double expected = ExpectedCountAt(guildRank);
        int count = Math.Max(1, (int)Math.Round(expected * (0.6 + rng.NextDouble() * 0.8)));

        var board = new List<Contract>();

        // 지속 의뢰가 먼저 옵니다 — 준비가 안 된 달에 떠서 놓치는 일이 없어야 합니다.
        if (carriedOver is not null) board.AddRange(carriedOver.Where(c => c.Persists));

        var used = new HashSet<string>(board.Select(c => c.Name), StringComparer.Ordinal);

        for (int i = 0; i < count; i++)
        {
            var forked = rng.Fork($"contract:{month}:{i}");
            var contract = Roll(forked, month, guildRank, weights, $"C{month:00}-{i:00}");

            // 같은 이름이 한 게시판에 두 번 뜨면 고르는 행위 자체가 헷갈립니다.
            if (!used.Add(contract.Name)) continue;

            board.Add(contract);
        }

        return board;
    }

    /// <summary>
    /// 승급 의뢰를 만듭니다. <b>지속 의뢰</b>이고 <b>길드에 도움이 되는 수준</b>입니다 (§6.5).
    /// <para>
    /// 승급이 자동이 아니라 사건이 되는 장치입니다 — 숙련도가 문턱을 넘는 순간 조용히
    /// 올라가는 게 아니라, 받아서 통과해야 합니다.
    /// </para>
    /// <para><b>[검토중]</b> 자격 조건·실패 시 재도전·파티 승급과 개인 승급이 같은 형태인지.</para>
    /// </summary>
    /// <param name="target">올라가려는 등급.</param>
    /// <param name="partyOnly">파티 승급인가.</param>
    public static Contract Promotion(Rank target, bool partyOnly = false)
    {
        // 승급 의뢰의 난이도는 올라갈 등급에 맞춥니다 — 시시하면 승급이 사건이 안 됩니다.
        int difficulty = Math.Max(1, MaxDifficultyAt(target.Below(1)));

        return new Contract(
            Id: $"promo:{(partyOnly ? "party" : "solo")}:{target}",
            Name: $"{target.ToKorean()}등급 승급 의뢰",
            Form: ContractForm.Subjugate,
            Source: ContractSource.Realm,
            Difficulty: difficulty,
            Months: 2,
            Intensity: difficulty * 3,
            PartyOnly: partyOnly,
            Persists: true,
            RequiredRank: target.Below(1));
    }

    /// <summary>그 사람 · 그 파티가 지금 받을 수 있는 의뢰만 골라냅니다.</summary>
    /// <param name="board">게시판.</param>
    /// <param name="rank">수주자의 등급 (개인이면 개인 등급, 파티면 파티 등급).</param>
    /// <param name="asRegularParty">정규 파티로 받는가. 아니면 파티 전용이 빠집니다.</param>
    /// <param name="maxDifficulty">수주 자격 난이도 상한. 직업에서 나옵니다.</param>
    public static IReadOnlyList<Contract> AvailableTo(
        IReadOnlyList<Contract> board, Rank rank, bool asRegularParty, int maxDifficulty) =>
        [.. board.Where(c =>
            c.IsOpenTo(rank)
            && c.Difficulty <= maxDifficulty
            && (asRegularParty || !c.PartyOnly))];

    // ── 내부 ────────────────────────────────────────────────

    private static Contract Roll(
        IRandomSource rng,
        int month,
        Rank guildRank,
        IReadOnlyDictionary<ContractForm, double> weights,
        string id)
    {
        var form = PickForm(rng, weights);
        int months = PickMonths(rng);

        int min = MinDifficultyAt(guildRank);
        int max = MaxDifficultyAt(guildRank);
        int difficulty = min + rng.NextInt(0, Math.Max(1, max - min + 1));

        var source = PickSource(rng, form);
        var (name, objective) = ContractFlavor.Pick(form, rng);

        // 강도는 기간과 난이도에서 나옵니다 — 그 기간에 그 정도 싸움이 있다는 표시입니다.
        int intensity = Math.Max(1, (int)Math.Round(difficulty * months * IntensityScale(form)));

        // 파티 전용은 여럿이 필요한 일 — 긴 토벌과 전선 수비가 자연스럽게 여기 옵니다.
        bool partyOnly = months >= 3
            && form is ContractForm.Subjugate or ContractForm.Defend
            && guildRank >= Rank.D;

        return new Contract(
            Id: id,
            Name: name,
            Form: form,
            Source: source,
            Difficulty: difficulty,
            Months: months,
            Intensity: intensity,
            PartyOnly: partyOnly,
            RequiredRank: RequiredRankFor(difficulty),
            Objective: objective);
    }

    /// <summary>형태별 강도 배율. 토벌은 마리 수라 크고, 발견은 곳 수라 작습니다. ⚠️ 임시값.</summary>
    private static double IntensityScale(ContractForm form) => form switch
    {
        ContractForm.Subjugate => 2.0,
        ContractForm.Defend => 0.5,
        ContractForm.Gather => 3.0,
        _ => 0.4
    };

    /// <summary>난이도가 요구하는 등급. 낮은 등급이 높은 난이도를 받지 못하게 합니다. ⚠️ 임시값.</summary>
    private static Rank RequiredRankFor(int difficulty) =>
        Ranks.Lowest.Above(Math.Max(0, (difficulty - 2) / 2));

    private static ContractForm PickForm(
        IRandomSource rng, IReadOnlyDictionary<ContractForm, double> weights)
    {
        // Dictionary 순회 순서가 결과를 바꾸지 않도록 열거형 순서로 고정합니다.
        var forms = new[]
        {
            ContractForm.Subjugate, ContractForm.Defend, ContractForm.Gather, ContractForm.Discover
        };

        double total = forms.Sum(f => weights.GetValueOrDefault(f));
        double roll = rng.NextDouble() * total;

        double running = 0.0;
        foreach (var form in forms)
        {
            running += weights.GetValueOrDefault(form);
            if (roll < running) return form;
        }

        return forms[^1];
    }

    private static int PickMonths(IRandomSource rng)
    {
        double total = Durations.Sum(d => d.Weight);
        double roll = rng.NextDouble() * total;

        double running = 0.0;
        foreach (var (months, weight) in Durations)
        {
            running += weight;
            if (roll < running) return months;
        }

        return Durations[0].Months;
    }

    /// <summary>
    /// 출처는 형태에서 대체로 따라옵니다 — 발견은 길드가 자기 돈으로 하는 일이고,
    /// 전선 수비는 나라가 시킵니다.
    /// </summary>
    private static ContractSource PickSource(IRandomSource rng, ContractForm form) => form switch
    {
        ContractForm.Discover => rng.Chance(0.7) ? ContractSource.Guild : ContractSource.Village,
        ContractForm.Subjugate => rng.Chance(0.45) ? ContractSource.Realm : ContractSource.Village,
        ContractForm.Defend => rng.Chance(0.35) ? ContractSource.Realm : ContractSource.Village,
        _ => ContractSource.Village
    };
}

/// <summary>
/// 의뢰 이름 풀.
/// <para>
/// <b>여기에 줄을 더하는 것만으로 새 종류가 생깁니다</b> — 진행 규칙은 형태 4종이
/// 전부이므로 코드를 건드릴 일이 없습니다 (§17.3).
/// </para>
/// </summary>
public static class ContractFlavor
{
    private static readonly (string Name, string? Objective)[] Subjugation =
    [
        ("폐광 고블린 소탕", null), ("숲길 늑대 퇴치", null), ("다리 밑 트롤 처리", null),
        ("묘지 언데드 정화", null), ("산적단 토벌", null), ("채석장 골렘 파괴", null),
        ("마물령 침범", null), ("전선 마물 정리", null)
    ];

    private static readonly (string Name, string? Objective)[] Defence =
    [
        ("상단 호위", "상단"), ("감시탑 수비", "감시탑"), ("파종 보호", "밭"),
        ("성벽 보수공사 경비", "공사장"), ("수확 호위", "수확물"), ("이주민 호송", "이주민"),
        ("광산 입구 방어", "광산")
    ];

    private static readonly (string Name, string? Objective)[] Gathering =
    [
        ("은광맥 채굴", null), ("약초 채집", null), ("철광석 운반", null),
        ("버섯 동굴 수확", null), ("마물 소재 수집", null), ("목재 벌채", null)
    ];

    private static readonly (string Name, string? Objective)[] Discovery =
    [
        ("고대 유적 조사", "유적"), ("실종자 수색", "실종자"), ("봉인된 지하실 답사", "지하실"),
        ("지도 없는 동굴 탐사", "동굴"), ("던전 탐색", "던전"), ("무너진 감시탑 정찰", "감시탑")
    ];

    public static (string Name, string? Objective) Pick(ContractForm form, IRandomSource rng)
    {
        var pool = PoolFor(form);
        return pool[rng.NextInt(0, pool.Length)];
    }

    public static (string Name, string? Objective)[] PoolFor(ContractForm form) => form switch
    {
        ContractForm.Subjugate => Subjugation,
        ContractForm.Defend => Defence,
        ContractForm.Gather => Gathering,
        _ => Discovery
    };
}
