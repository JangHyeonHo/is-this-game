using Guildwright.Cli;
using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;

// ─────────────────────────────────────────────────────────────
// Guildwright — 텍스트 프로토타입
//
// 목적은 하나입니다: "이게 재미있는가"를 실제로 판단하는 것.
// 그래픽만 없을 뿐, 게임 흐름은 실제 설계 그대로입니다.
// ─────────────────────────────────────────────────────────────

// 배치 시뮬레이션 모드. 밸런스 수치를 감으로 바꾸지 않기 위한 도구입니다.
//   dotnet run --project src/Guildwright.Console -- sim [시행수] [연차]
if (args.Length > 0 && args[0] == "sim")
{
    int trials = args.Length > 1 && int.TryParse(args[1], out int t) ? t : 400;
    int years = args.Length > 2 && int.TryParse(args[2], out int y) ? y : 5;
    BalanceReport.TrainingPolicies(trials, years);
    return;
}

ulong seed = args.Length > 0 && ulong.TryParse(args[0], out ulong s) ? s : 20260728UL;
var rng = new DeterministicRandom(seed);

Ui.Title("Guildwright");
Ui.Note($"시드 {seed} — 같은 시드는 항상 같은 세계를 만듭니다.");
Ui.Note("당신은 신생 길드의 길드장입니다. 사람을 뽑고, 키우고, 내보내십시오.");

var guild = new Guild(rng);
guild.Run();

// ─────────────────────────────────────────────────────────────

/// <summary>길드 상태와 연간 진행. 규칙은 전부 코어에 있고 여기는 흐름만 엮습니다.</summary>
internal sealed class Guild(IRandomSource rng)
{
    private const int StartingFunds = 2_000;
    private const int NamePoolSize = 3;

    private readonly List<Adventurer> _members = [];

    /// <summary>
    /// 평가서 캐시.
    /// <para>
    /// <b>같은 상황에서 다시 보면 같은 내용이어야 합니다.</b> 볼 때마다 새로 굴리면
    /// 화면을 다시 여는 것만으로 추정치가 바뀌어, 정보로서 의미가 없어집니다.
    /// 관찰 연차나 감정 역량이 달라졌을 때만 새로 굴립니다.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, (int Years, int Skill, ScoutingReport Report)> _reports = [];
    private readonly List<Adventurer> _retired = [];
    private readonly List<string> _chronicle = [];

    private int _funds = StartingFunds;
    private int _reputation;
    private int _year = 1;
    private int _nextId;

    public void Run()
    {
        while (true)
        {
            Ui.Title($"{_year}년차   자금 {_funds}   평판 {_reputation}   단원 {_members.Count}명");

            RecruitPhase();

            if (_members.Count == 0)
            {
                Ui.Note("단원이 없습니다. 길드는 문을 닫았습니다.");
                return;
            }

            PlanAndExecutePhase();
            YearEndPhase();

            if (_funds < 0)
            {
                Ui.Section("파산");
                Ui.Note("자금이 바닥났습니다. 길드는 해산되었습니다.");
                ShowChronicle();
                return;
            }

            Ui.Line();
            if (!Ui.Confirm("다음 해로 넘어가시겠습니까?"))
            {
                ShowChronicle();
                return;
            }

            _year++;
        }
    }

    // ── 모집 ────────────────────────────────────────────────

    private void RecruitPhase()
    {
        Ui.Section("모집");

        // 감정 역량이 높은 단원이 있으면 후보를 더 정확히 봅니다.
        double appraisal = GuildAppraisalSkill();
        Ui.Note($"길드 감정 역량 {appraisal:P0} — 높을수록 후보의 재능을 정확히 알아봅니다.");

        var candidates = new List<Adventurer>();
        for (int i = 0; i < NamePoolSize; i++)
        {
            candidates.Add(Adventurer.Recruit($"A{_nextId++}", Names.Next(rng), rng.Fork($"recruit:{_year}:{i}")));
        }

        var labels = new List<string>();
        Ui.Line();

        for (int i = 0; i < candidates.Count; i++)
        {
            var report = ReportFor(candidates[i]);
            Ui.Line($"   ── 후보 {i + 1} ──");
            Display.Scouting(candidates[i], report);
            Ui.Line();
            labels.Add($"{candidates[i].Name} 영입 (계약금 {RecruitCost})");
        }

        int affordable = Math.Max(0, _funds / RecruitCost);
        var picked = Ui.ChooseMany("영입할 사람", labels, affordable);

        foreach (int index in picked)
        {
            _members.Add(candidates[index]);
            _funds -= RecruitCost;
            Record($"{_year}년: {candidates[index].Name} 영입");
        }

        if (picked.Count == 0) Ui.Note("아무도 뽑지 않았습니다.");
    }

    private const int RecruitCost = 150;

    /// <summary>캐시된 평가서를 가져옵니다. 상황이 바뀌었을 때만 다시 굴립니다.</summary>
    private ScoutingReport ReportFor(Adventurer a)
    {
        double skill = GuildAppraisalSkill();
        int skillBucket = (int)Math.Round(skill * 20);   // 5%p 단위로만 갱신

        if (_reports.TryGetValue(a.Id, out var cached) &&
            cached.Years == a.CompletedYears && cached.Skill == skillBucket)
        {
            return cached.Report;
        }

        var fresh = Appraiser.Appraise(a, skill, rng.Fork($"appraise:{a.Id}:{a.CompletedYears}:{skillBucket}"));
        _reports[a.Id] = (a.CompletedYears, skillBucket, fresh);
        return fresh;
    }

    private double GuildAppraisalSkill()
    {
        double fromMembers = _members.Count == 0
            ? 0.0
            : _members.Max(m => m.Support[SupportSkill.Appraisal]) / 100.0;

        double fromMentors = _retired.Count == 0
            ? 0.0
            : _retired.Max(r => Mentorship.From(r).AppraisalBonus);

        return Math.Clamp(Math.Max(fromMembers, fromMentors), 0.0, 1.0);
    }

    // ── 연간 계획과 실행 ────────────────────────────────────

    private void PlanAndExecutePhase()
    {
        var board = ContractGenerator.GenerateBoard(
            rng.Fork($"board:{_year}"), 4, Math.Max(2, _reputation / 8 + 2));

        foreach (var member in _members.ToList())
        {
            if (member.Status != AdventurerStatus.Active) continue;

            Ui.Section($"{member.Name}의 {_year}년");
            Display.StatSheet(member);

            if (!member.CanDeploy)
            {
                Ui.Note("등록 첫 해입니다. 실전에 내보낼 수 없습니다.");
                TrainingYear(member);
                continue;
            }

            int choice = Ui.Choose("올해 무엇을 시킬까요", ["육성 (안전하지만 수입 없음)", "실전 파견 (수입과 경험, 죽을 수 있음)", "은퇴시킨다"]);

            switch (choice)
            {
                case 0: TrainingYear(member); break;
                case 1: DeploymentYear(member, board); break;
                case 2: Retire(member); break;
            }
        }
    }

    // ── 육성: 계획 → 하이브리드 실행 ────────────────────────

    private void TrainingYear(Adventurer member)
    {
        var mentorship = BestMentor();
        if (mentorship is not null) Ui.Note($"멘토: {mentorship.MentorName} (훈련 배율 {mentorship.TrainingMultiplier:F2})");

        var report = ReportFor(member);

        var plan = BuildYearPlan(member, report, mentorship);
        ExecuteYearPlan(member, plan, mentorship);
    }

    /// <summary>연초에 12개월 계획을 짭니다. 예상 성장을 보면서 고칠 수 있습니다.</summary>
    private List<TrainingActivity> BuildYearPlan(Adventurer member, ScoutingReport report, Mentorship? mentorship)
    {
        Ui.Line();
        Ui.Line("   ── 1년 계획 ──");
        Ui.Note("12개월을 미리 짭니다. 실행 중 상황이 바뀌면 그때 고칠 수 있습니다.");

        var plan = new List<TrainingActivity>();
        var menu = Display.FocusMenu().Append("이후 전부 휴식").ToList();

        for (int month = 1; month <= TrainingRules.MonthsPerYear; month++)
        {
            if (plan.Count > 0)
            {
                var preview = TrainingForecaster.Forecast(member, report, PadPlan(plan), mentorship);
                Ui.Line();
                Display.Forecast(preview, report.Confidence, decidedMonths: plan.Count);
            }

            int pick = Ui.Choose($"   {month}월", menu);

            if (pick == menu.Count - 1)
            {
                while (plan.Count < TrainingRules.MonthsPerYear) plan.Add(TrainingActivity.Rest);
                break;
            }

            plan.Add(Display.FocusFromIndex(pick));
        }

        Ui.Line();
        Ui.Line("   확정된 계획: " + string.Join(" ", plan.Select(Display.FocusName)));

        var final = TrainingForecaster.Forecast(member, report, plan, mentorship);
        Display.Forecast(final, report.Confidence);

        return plan;
    }

    private static List<TrainingActivity> PadPlan(List<TrainingActivity> plan)
    {
        var padded = new List<TrainingActivity>(plan);
        while (padded.Count < TrainingRules.MonthsPerYear) padded.Add(TrainingActivity.Rest);
        return padded;
    }

    /// <summary>
    /// 계획대로 자동 진행하되, <b>상황이 바뀌는 달에만 멈춰서</b> 물어봅니다.
    /// <para>계획의 재미와 대응의 재미를 둘 다 얻기 위한 절충입니다.</para>
    /// </summary>
    private void ExecuteYearPlan(Adventurer member, List<TrainingActivity> plan, Mentorship? mentorship)
    {
        Ui.Line();
        Ui.Line("   ── 실행 ──");

        var before = member.Stats;
        var session = new TrainingYearSession(member, rng.Fork($"train:{_year}:{member.Id}"), mentorship);

        while (!session.IsComplete)
        {
            int month = session.CurrentMonth;
            var planned = plan[month - 1];

            if (ShouldInterrupt(session, planned))
            {
                Ui.Line();
                Ui.Note($"{month}월 · 컨디션 {session.Condition.ToKorean()} · 피로 {session.Fatigue}" +
                        (session.FailureChance > 0 ? $" · 실패 확률 {session.FailureChance:P0}" : ""));
                Ui.Note($"계획은 [{Display.FocusLabel(planned)}] 입니다.");

                if (Ui.Confirm("   계획을 바꾸시겠습니까?"))
                {
                    int pick = Ui.Choose($"   {month}월 변경", Display.FocusMenu());
                    planned = Display.FocusFromIndex(pick);
                    plan[month - 1] = planned;
                }
            }

            var outcome = session.AdvanceMonth(planned);
            Ui.Line($"     {outcome.Note}");

            // 월별 성장치는 표시하지 않습니다. 내부적으로는 실수로 누적하고 연말에 한 번만
            // 반올림하므로, 월별 반올림값을 더해도 연간 합계와 맞지 않아 오히려 혼란스럽습니다.
        }

        session.Complete();

        Ui.Line();
        Ui.Note($"1년 결과: {FormatGain(member.Stats - before)}");
        Display.StatSheet(member);
    }

    /// <summary>계획을 다시 물어볼 만한 순간인가.</summary>
    private static bool ShouldInterrupt(TrainingYearSession session, TrainingActivity planned)
    {
        // 절호조인데 쉬려고 한다 — 놓치면 아까운 달
        if (session.Condition >= Condition.Excellent && planned == TrainingActivity.Rest) return true;

        // 컨디션이 바닥인데 훈련하려 한다
        if (session.Condition <= Condition.Terrible && planned != TrainingActivity.Rest) return true;

        // 실패 확률이 무시 못 할 수준인데 계속 훈련하려 한다
        if (session.FailureChance >= 0.15 && planned != TrainingActivity.Rest) return true;

        return false;
    }

    private static string FormatGain(PrimaryStats gain)
    {
        var parts = PrimaryStats.AllStats
            .Where(s => gain[s] != 0)
            .Select(s => $"{s.ToKorean()} {(gain[s] > 0 ? "+" : "")}{gain[s]}");

        string text = string.Join(" ", parts);
        return text.Length == 0 ? "변화 없음" : text;
    }

    // ── 실전 파견 ───────────────────────────────────────────

    private void DeploymentYear(Adventurer member, IReadOnlyList<Contract> board)
    {
        // 지금은 목표 수치형 의뢰 하나만 있습니다.
        // 「난이도 N짜리 전투 한 판」 구조를 걷어내는 중이라 옛 의뢰는 잠시 내렸습니다.
        var contract = Contract.Combat("가도 정리", difficulty: 1);
        const int Quota = 10;

        Ui.Line();
        Ui.Line("   ── 의뢰 ──");
        Ui.Line($"   [{contract.Name}] 난이도 {contract.Difficulty}");
        Ui.Note($"마을 근처 가도에 고블린이 늘었습니다. 1년 안에 {Quota}마리를 정리하십시오.");

        var party = new List<Adventurer> { member };

        var others = _members
            .Where(m => m.Id != member.Id && m.Status == AdventurerStatus.Active && m.CanDeploy)
            .ToList();

        if (others.Count > 0)
        {
            var picks = Ui.ChooseMany("   함께 보낼 동료",
                others.Select(o => $"{o.Name} · {o.Title} ({o.EquippedStyle.ToKorean()})").ToList(), 3);
            party.AddRange(picks.Select(i => others[i]));
        }

        int rolePick = Ui.Choose("   맡길 비전투 역할",
            SupportSkills.All.Select(sk => $"{sk.ToKorean()} (현재 {member.Support[sk]})").Append("없음").ToList());

        SupportSkill? role = rolePick < SupportSkills.All.Count ? SupportSkills.All[rolePick] : null;

        var support = ContractResolver.Evaluate(contract, party.Select(p => p.Support).ToList());

        Ui.Line();
        Ui.Note($"파티 역량 반영 — 위험 {support.RiskMultiplier:P0}, 보수 {support.IncomeMultiplier:P0}, " +
                $"추가 회복약 {support.ExtraPotions}");

        var result = RunFieldYear(party, contract, Quota, support);

        ApplyDeploymentResults(member, party, contract, support, role, result);
    }

    /// <summary>
    /// 파견 1년을 월 단위로 진행합니다.
    /// <para>
    /// 훈련 연도와 같은 리듬입니다 — 매달 무엇을 할지 고르고, 조우하면 싸울지 피할지 고릅니다.
    /// <b>HP와 회복약이 전투 사이에 저절로 회복되지 않아서</b> 매 판단에 무게가 생깁니다.
    /// </para>
    /// </summary>
    private FieldYearOutcome RunFieldYear(
        List<Adventurer> party, Contract contract, int quota, ContractSupport support)
    {
        var session = new FieldYearSession(
            party, contract, quota, rng.Fork($"field:{_year}"),
            potionsEach: 2 + support.ExtraPotions);

        bool manual = Ui.Confirm("   전투에 직접 개입하시겠습니까?");
        var commander = manual ? new ConsoleCommander() : null;

        Ui.Line();
        Ui.Line("   ── 파견 ──");

        while (!session.IsComplete)
        {
            Display.FieldStatus(session, party);

            int pick = Ui.Choose($"   {session.CurrentMonth}월", Display.FieldMenu());
            var action = (FieldAction)pick;

            var encounter = session.StartMonth(action);

            if (encounter is null)
            {
                Ui.Line($"     {session.Months[^1].Note}");
                continue;
            }

            Ui.Line();
            Ui.Note($"고블린 {encounter.Enemies.Count}마리와 마주쳤습니다. " +
                    $"빠져나갈 가능성 {encounter.AvoidChance:P0}");

            bool fight = Ui.Choose("   어떻게 할까요",
                [$"교전한다", $"피한다 (성공 {encounter.AvoidChance:P0} · 실패하면 기습당함)"]) == 0;

            if (!fight && session.Avoid())
            {
                Ui.Line($"     {session.Months[^1].Note}");
                continue;
            }

            if (!fight) Ui.Note("빠져나가지 못했습니다. 기습당한 채로 싸웁니다.");

            var battle = session.Fight(
                rng.Fork($"battle:{_year}:{session.CurrentMonth}"),
                commander,
                manual ? line => Ui.Line("       " + line) : null);

            if (!manual)
            {
                foreach (var line in battle.Log) Ui.Line("       " + line);
            }

            Ui.Line($"     {session.Months[^1].Note}");
        }

        var final = session.Complete();

        Ui.Line();
        Ui.Note(final.Achieved
            ? $"목표 달성 — 고블린 {final.Killed}마리를 정리했습니다."
            : final.Retreated
                ? $"더 싸울 수 없어 돌아왔습니다. {final.Killed}/{final.Quota}마리."
                : $"1년이 끝났습니다. {final.Killed}/{final.Quota}마리에 그쳤습니다.");

        return new FieldYearOutcome(final, session.Experience);
    }

    private sealed record FieldYearOutcome(
        FieldYearResult Result,
        IReadOnlyDictionary<string, CombatExperience> Experience);

    /// <summary>파견 결과를 각자에게 적용합니다.</summary>
    private void ApplyDeploymentResults(
        Adventurer leader,
        List<Adventurer> party,
        Contract contract,
        ContractSupport support,
        SupportSkill? role,
        FieldYearOutcome fought)
    {
        // 목표를 채웠으면 승리로, 못 채웠으면 미완/실패로 봅니다.
        var outcome = fought.Result.Achieved
            ? BattleOutcome.PlayerVictory
            : fought.Result.Retreated
                ? BattleOutcome.EnemyVictory
                : BattleOutcome.Draw;

        int totalIncome = 0;

        foreach (var fighter in party)
        {
            var record = CareerSimulator.ResolveDeploymentYear(
                fighter, contract.Difficulty, rng.Fork($"deploy:{_year}:{fighter.Id}"),
                fought.Experience.GetValueOrDefault(fighter.Id),
                fighter.Id == leader.Id ? role : null,
                contract, support,
                new BattleReport(outcome, Downed: fought.Result.Retreated));

            totalIncome += record.Income;
            Ui.Line($"     {record.Note}");

            if (record.Outcome == DeploymentOutcome.Died)
            {
                Record($"{_year}년: {fighter.Name} 전사 — {contract.Name}");
            }
            else if (record.Outcome == DeploymentOutcome.Crippled)
            {
                Record($"{_year}년: {fighter.Name} 불구가 되어 은퇴 — {contract.Name}");
                _retired.Add(fighter);
            }
        }

        int reputationGain = outcome switch
        {
            BattleOutcome.PlayerVictory => contract.Difficulty,
            BattleOutcome.Draw => 0,
            _ => -1
        };

        _funds += totalIncome;
        _reputation = Math.Max(0, _reputation + reputationGain);
        Ui.Note($"보수 {totalIncome}, 평판 {(reputationGain >= 0 ? "+" : "")}{reputationGain}");

        _members.RemoveAll(m => m.Status is AdventurerStatus.Dead or AdventurerStatus.Crippled);
    }

    // ── 연말 ────────────────────────────────────────────────

    private void Retire(Adventurer member)
    {
        member.Retire();
        _retired.Add(member);
        _members.Remove(member);
        Record($"{_year}년: {member.Name} 은퇴 ({member.Title}, {member.Age}세)");
        Ui.Note($"{member.Name}이(가) 은퇴했습니다. 이제 후배를 가르칠 수 있습니다.");
    }

    private Mentorship? BestMentor()
    {
        var candidates = _retired.Where(r => r.CanMentor).ToList();
        if (candidates.Count == 0) return null;

        return candidates
            .Select(Mentorship.From)
            .OrderByDescending(m => m.TrainingMultiplier)
            .First();
    }

    private void YearEndPhase()
    {
        Ui.Section($"{_year}년 결산");

        int wages = _members.Sum(m => m.AnnualWage);
        _funds -= wages;
        _reputation += _members.Sum(m => m.ReputationValue) / 4;

        Ui.Note($"급여 지출 {wages}");
        foreach (var m in _members)
        {
            Ui.Line($"     {m.Name} · {m.Title} ({m.Age}세) 연봉 {m.AnnualWage}");
        }

        Ui.Note($"남은 자금 {_funds} · 평판 {_reputation}");

        if (_funds < wages)
        {
            Ui.Note("⚠ 다음 해 급여를 감당하기 어렵습니다. 실전에 내보내야 합니다.");
        }
    }

    private void Record(string entry)
    {
        _chronicle.Add(entry);
    }

    private void ShowChronicle()
    {
        Ui.Section("길드 연대기");
        if (_chronicle.Count == 0) Ui.Note("기록이 없습니다.");
        foreach (var entry in _chronicle) Ui.Note(entry);
    }
}

/// <summary>이름 생성. 아트가 없으니 이름이라도 분위기를 냅니다.</summary>
internal static class Names
{
    private static readonly string[] First =
    [
        "아스카르", "미렌", "도르한", "셀비아", "카이엔", "루베르", "타냐", "그림", "이졸데",
        "베르난", "샤이엔", "오르한", "리케", "무단", "엘리아", "가웨인", "노라", "테오"
    ];

    private static readonly string[] Epithet =
    [
        "몰락 귀족", "떠돌이", "전직 병사", "고아", "밀렵꾼", "수도원 출신", "광부의 아들",
        "이방인", "빚쟁이", "탈영병"
    ];

    private static readonly string[] Monsters =
    [
        "고블린 전사", "숲늑대", "산적", "해골병사", "동굴 트롤", "석상 골렘", "들개 무리"
    ];

    public static string Next(IRandomSource rng) =>
        $"{First[rng.NextInt(0, First.Length)]}({Epithet[rng.NextInt(0, Epithet.Length)]})";

    public static string Monster(IRandomSource rng) => Monsters[rng.NextInt(0, Monsters.Length)];
}
