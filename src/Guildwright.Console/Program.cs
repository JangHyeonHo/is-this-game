using Guildwright.Cli;
using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Parties;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
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
    /// 파티 장부. <b>가상 파티 누적과 정규 파티</b>가 여기 있습니다 (docs/07 §6).
    /// <para>
    /// 코어에 있어도 여기에 연결되지 않으면 <b>플레이어는 파티를 만질 수 없습니다.</b>
    /// 실제로 그 상태였습니다 — 층·누적·자격·등급이 전부 있는데 인게임에 없었습니다.
    /// </para>
    /// </summary>
    private readonly PartyLedger _parties = new();

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
    private int _month = 1;
    private int _nextId;

    /// <summary>
    /// 달력 잠금 — 그 사람이 <b>몇 월까지 예약되어 있는가</b> (모험가 Id → 마지막 잠긴 달).
    /// <para>
    /// 이것이 §17.4의 유일한 브레이크입니다: <b>"다섯을 보내면 그 다섯의 그 기간이 잠기고,
    /// 그 사이에 뜬 의뢰는 못 받습니다. 기회비용이 유일한 브레이크입니다."</b>
    /// 연 단위 루프에서는 이 압력이 아예 성립하지 않았습니다.
    /// </para>
    /// <para>구속이 아니라 예약입니다 — 중도 이탈하면 칸이 풀립니다 (§17.7).</para>
    /// </summary>
    private readonly Dictionary<string, int> _bookedUntil = [];

    /// <summary>진행 중인 훈련. 달마다 한 달씩 전진하고, 연말이나 파견 전에 결산합니다.</summary>
    private readonly Dictionary<string, TrainingYearSession> _training = [];

    /// <summary>지난 달에서 넘어온 지속 의뢰 (승급 의뢰 · 전개상 필수).</summary>
    private IReadOnlyList<Contract> _carriedOver = [];

    /// <summary>지금까지 흐른 절대 달 수. 잠금 계산에 씁니다.</summary>
    private int AbsoluteMonth => (_year - 1) * Calendar.MonthsPerYear + _month;

    /// <summary>길드 랭크. 아직 평판이 대신합니다.</summary>
    private Rank GuildRank => Ranks.Lowest.Above(Math.Clamp(_reputation / 12, 0, 7));

    /// <summary>
    /// <b>달 단위로 굴러갑니다.</b> 매달 정책을 정하고, 의뢰를 받으면 그 기간만큼 칸이 잠깁니다.
    /// <para>
    /// 예전에는 <b>연 1회 루프</b>였습니다 — 한 사람이 한 해에 의뢰 하나만 받고, 1달 의뢰를
    /// 받아도 그 해가 끝났습니다. 그러면 §17.4가 과잉 전력을 막는 <b>유일한 브레이크</b>로
    /// 지목한 기회비용이 성립하지 않고, 계절도 파티 6개월 누적도 도달할 수 없습니다.
    /// </para>
    /// 근거: docs/07-decisions.md §15, §17.4, §17.10
    /// </summary>
    public void Run()
    {
        while (true)
        {
            // 매년 1월에 길드원 모집이 열립니다 (§17.10).
            if (Calendar.IsRecruitmentMonth(_month))
            {
                Ui.Title($"{_year}년 {_month}월   자금 {_funds}   평판 {_reputation}({GuildRank.Label()})   " +
                         $"단원 {_members.Count}/{RosterCapacity}명");
                RecruitPhase();
            }

            if (_members.Count == 0)
            {
                Ui.Note("단원이 없습니다. 길드는 문을 닫았습니다.");
                ShowChronicle();
                return;
            }

            if (!MonthPhase()) { ShowChronicle(); return; }

            if (_month == Calendar.MonthsPerYear)
            {
                YearEndPhase();

                if (_funds < 0)
                {
                    Ui.Section("파산");
                    Ui.Note("자금이 바닥났습니다. 길드는 해산되었습니다.");
                    ShowChronicle();
                    return;
                }

                _year++;
                _month = 1;
            }
            else
            {
                _month++;
            }
        }
    }

    /// <summary>
    /// 랭크별 최대 단원 수 (§17.10). ⚠️ 임시값 — "실제 가능수는 아직은 미정입니다.
    /// 게임의 루즈함과 밸런스에 따라 조절을 고려하고 있습니다."
    /// </summary>
    private int RosterCapacity => 8 + (int)GuildRank * 7;

    /// <summary>그 사람이 지금 예약되어 있는가.</summary>
    private bool IsBooked(Adventurer a) =>
        _bookedUntil.TryGetValue(a.Id, out int until) && until >= AbsoluteMonth;

    private int BookedMonthsLeft(Adventurer a) =>
        _bookedUntil.TryGetValue(a.Id, out int until) ? Math.Max(0, until - AbsoluteMonth + 1) : 0;

    /// <summary>
    /// 한 달을 진행합니다. <b>매달 정책을 정합니다</b> — 연초에 1년치를 짜는 방식은 폐기됐습니다 (§17.10).
    /// </summary>
    /// <returns>계속 진행할지.</returns>
    private bool MonthPhase()
    {
        var season = Calendar.SeasonOf(_month);

        Ui.Section($"{_year}년 {_month}월 · {season.ToKorean()}");

        // 게시판은 매달 새로 뜨고, 안 받으면 사라집니다. 지속 의뢰만 남습니다 (§17.8).
        var board = ContractBoard.Post(
            rng.Fork($"board:{_year}:{_month}"), _month, GuildRank, _carriedOver);
        _carriedOver = board;

        Ui.Note($"게시판 {board.Count}건 · {season.ToKorean()}에는 " +
                $"{string.Join(" ", ContractBoard.WeightsIn(season).OrderByDescending(w => w.Value).Take(2).Select(w => w.Key.ToKorean()))}이 많습니다");

        foreach (var member in _members.ToList())
        {
            if (member.Status != AdventurerStatus.Active) continue;

            // 예약된 사람은 그 달에 손댈 수 없습니다. 이것이 기회비용입니다.
            if (IsBooked(member))
            {
                Ui.Line($"   {member.Name} — 의뢰 중 ({BookedMonthsLeft(member)}달 남음)");
                continue;
            }

            Ui.Line();
            Display.StatSheet(member);

            var choices = new List<string> { "훈련 (1달)", "휴식 (1달)" };
            bool canDeploy = member.CanDeploy;

            if (canDeploy) choices.Insert(0, "의뢰를 받는다");
            else Ui.Note("아직 실전에 나갈 수 없습니다 — 12달을 채워야 합니다.");

            // 전직은 자유이고 비용도 없습니다 (§16.4). 대가는 규칙이 아니라
            // 새 무기 숙련이 0부터라는 것입니다.
            var upgrades = UpgradesFor(member);
            // 사다리를 오를 수 있을 때만 눈에 띄게 알립니다. 계열을 바꾸는 전향은 언제나 가능합니다.
            int better = upgrades.Count(j => j.MaxContractDifficulty > member.MaxContractDifficulty);
            if (upgrades.Count > 0)
            {
                choices.Add(better > 0
                    ? $"전직 (상위 {better}개 해금)"
                    : $"전직 (계열 전향 {upgrades.Count}개)");
            }

            choices.Add("은퇴시킨다");

            int choice = Ui.Choose($"   {_month}월에 무엇을 시킬까요", choices);
            string picked = choices[choice];

            if (picked.StartsWith("의뢰")) DeploymentMonth(member, board);
            else if (picked.StartsWith("훈련")) TrainMonth(member);
            else if (picked.StartsWith("휴식")) RestMonth(member);
            else if (picked.StartsWith("전직")) ChangeJob(member, upgrades);
            else Retire(member);
        }

        Ui.Line();
        return Ui.Confirm("다음 달로 넘어가시겠습니까?");
    }

    /// <summary>
    /// 지금 갈 수 있는 다른 직업. <b>지금 직업은 빼고</b> 보여줍니다.
    /// <para>
    /// 이것이 없으면 사다리를 오를 수 없어 <b>영구히 견습</b>입니다. 그러면 수주 난이도
    /// 상한이 2에 고정되고, 그 위에 얹힌 승급 의뢰·상위 의뢰가 통째로 잠깁니다 —
    /// 실제로 E급에서 더 오르지 않는 것으로 관측됐습니다.
    /// </para>
    /// </summary>
    private static List<Job> UpgradesFor(Adventurer member) =>
        // 수주 난이도가 높은 것부터. 사다리의 다음 단이 맨 위에 오도록 하는 것이 목적입니다 —
        // 요구 숙련 0인 견습이 열한 개나 있어서 정렬이 없으면 다음 단이 목록에 묻힙니다.
        [.. member.AvailableJobs
                  .Where(j => j.Id != member.Job)
                  .OrderByDescending(j => j.MaxContractDifficulty)
                  .ThenByDescending(j => j.ActiveSlots)
                  .ThenBy(j => j.Id)];

    /// <summary>전직합니다. 고집을 타고났으면 다른 계열로는 가지 않습니다 (§16.8).</summary>
    private void ChangeJob(Adventurer member, List<Job> upgrades)
    {
        var labels = upgrades
            .Select(j => $"{j.Korean} — 슬롯 {j.ActiveSlots} · 수주 난이도 {j.MaxContractDifficulty} · " +
                         $"유지비 {j.Upkeep}" +
                         (j.Grants.Count > 0
                             ? $" · {string.Join(", ", j.Grants.Select(g => SkillBook.Of(g).Korean))}"
                             : ""))
            .Append("그대로 둔다")
            .ToList();

        int pick = Ui.Choose($"   {member.Name}을(를) 어느 직업으로", labels);
        if (pick >= upgrades.Count) return;

        var target = upgrades[pick];
        string was = member.Title;

        if (!member.ChangeJob(target.Id))
        {
            Ui.Note($"{member.Name}은(는) 듣지 않습니다 — 고집을 타고났습니다.");
            return;
        }

        Record($"{_year}년 {_month}월: {member.Name} 전직 — {was} → {member.Title}");
        Ui.Note($"{was} → {member.Title}. 수주 난이도 {member.MaxContractDifficulty} · " +
                $"액티브 {string.Join(", ", member.Actives.Select(id => SkillBook.Of(id).Korean))}");
    }

    /// <summary>한 달 훈련합니다. 세션은 연말까지 이어지고 그때 결산합니다.</summary>
    private void TrainMonth(Adventurer member)
    {
        var session = SessionFor(member);

        Ui.Note($"컨디션 {session.Condition.ToKorean()} · 피로 {session.Fatigue}" +
                (session.FailureChance > 0 ? $" · 실패 확률 {session.FailureChance:P0}" : ""));

        int pick = Ui.Choose("   무엇을 훈련할까요", Display.FocusMenu());
        var outcome = session.AdvanceMonth(Display.FocusFromIndex(pick));

        Ui.Line($"     {_month}월: {Display.FocusName(outcome.Activity)} · {outcome.Grade.ToKorean()} " +
                $"(피로 {session.Fatigue})");

        // 12달을 채우면 그 자리에서 결산하고 새 세션을 엽니다.
        if (session.IsComplete) SettleTraining(member);
    }

    private void RestMonth(Adventurer member)
    {
        var session = SessionFor(member);
        var outcome = session.AdvanceMonth(TrainingActivity.Rest);

        Ui.Line($"     {_month}월: 휴식 · {outcome.Grade.ToKorean()} (피로 {session.Fatigue})");
        if (session.IsComplete) SettleTraining(member);
    }

    private TrainingYearSession SessionFor(Adventurer member)
    {
        if (_training.TryGetValue(member.Id, out var existing)) return existing;

        var mentorship = BestMentor();
        if (mentorship is not null) Ui.Note($"멘토: {mentorship.MentorName} (훈련 배율 {mentorship.TrainingMultiplier:F2})");

        var session = new TrainingYearSession(
            member, rng.Fork($"train:{_year}:{_month}:{member.Id}"), mentorship);

        _training[member.Id] = session;
        return session;
    }

    /// <summary>훈련한 달만 결산합니다. 12달을 안 채워도 됩니다 — 달력이 달 단위이므로.</summary>
    private void SettleTraining(Adventurer member)
    {
        if (!_training.TryGetValue(member.Id, out var session)) return;
        _training.Remove(member.Id);

        if (session.MonthsCompleted == 0) return;

        var before = member.Stats;
        var record = session.Settle();

        Ui.Note($"{record.Note} — {(member.Stats - before)}");
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

        // 길드 랭크가 최대 인원을 정합니다 (§17.10).
        int room = Math.Max(0, RosterCapacity - _members.Count);
        int affordable = Math.Min(room, Math.Max(0, _funds / RecruitCost));

        if (room == 0) Ui.Note($"정원이 찼습니다 ({RosterCapacity}명). 랭크가 올라야 늘어납니다.");

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
        double fromMembers = 0.0;

        double fromMentors = _retired.Count == 0
            ? 0.0
            : _retired.Max(r => Mentorship.From(r).AppraisalBonus);

        return Math.Clamp(Math.Max(fromMembers, fromMentors), 0.0, 1.0);
    }

    // ── 연간 계획과 실행 ────────────────────────────────────


    // ── 육성: 계획 → 하이브리드 실행 ────────────────────────



    private static List<TrainingActivity> PadPlan(List<TrainingActivity> plan)
    {
        var padded = new List<TrainingActivity>(plan);
        while (padded.Count < TrainingRules.MonthsPerYear) padded.Add(TrainingActivity.Rest);
        return padded;
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

    /// <summary>
    /// 그 달에 의뢰를 받습니다. <b>받으면 그 기간만큼 칸이 잠깁니다</b> — 동행자도 같이.
    /// </summary>
    private void DeploymentMonth(Adventurer member, IReadOnlyList<Contract> board)
    {
        // 게시판에서 고릅니다. 그 달에 안 받으면 사라집니다 (지속 의뢰만 남습니다).
        // 승급 의뢰는 지속 의뢰이므로 자격이 되면 붙여 둡니다 — 등급이 오르는 유일한 길입니다.
        var promotion = ContractBoard.PromotionFor(member);
        IReadOnlyList<Contract> posted = promotion is null ? board : [.. board, promotion];

        // 정규 파티에 속해 있으면 파티 전용 의뢰가 열립니다 (§6.3).
        var regular = _parties.RegularPartyOf(member.Id);
        if (regular is not null)
        {
            var partyQuest = ContractBoard.PromotionFor(regular);
            if (partyQuest is not null) posted = [.. posted, partyQuest];
        }

        var open = ContractBoard.AvailableTo(
            posted, regular?.Rank ?? member.Rank,
            asRegularParty: regular is not null, member.MaxContractDifficulty);

        if (open.Count == 0)
        {
            Ui.Note("받을 수 있는 의뢰가 없습니다. 아직 자격이 모자랍니다.");
            return;
        }

        Ui.Line();
        Ui.Line("   ── 의뢰 게시판 ──");
        int chosen = Ui.Choose("   무엇을 받겠습니까", [.. open.Select(Display.ContractLine)]);
        var contract = open[chosen];

        Ui.Note($"{contract.Months}달 동안 {contract.Name}. " +
                $"강도 {contract.Intensity}{contract.Form.IntensityLabel()} — 기간은 고정입니다.");

        var party = new List<Adventurer> { member };

        // 예약된 사람은 못 데려갑니다 — 그 사람의 그 기간은 이미 다른 의뢰에 잠겨 있습니다.
        var others = _members
            .Where(m => m.Id != member.Id && m.Status == AdventurerStatus.Active
                        && m.CanDeploy && !IsBooked(m))
            .ToList();

        if (others.Count > 0)
        {
            var picks = Ui.ChooseMany("   함께 보낼 동료",
                others.Select(o => $"{o.Name} · {o.Title} · {o.Rank.Label()} ({o.Loadout})").ToList(), 3);
            party.AddRange(picks.Select(i => others[i]));
        }
        else
        {
            Ui.Note("함께 보낼 사람이 없습니다 — 다른 이들은 이미 의뢰 중입니다.");
        }

        int capacity = Supplies.CapacityOf(party);
        Ui.Note($"짐 한도 {capacity}개 — 가방을 든 사람이 있으면 늘어납니다");

        // 조합이 성립하지 않으면 애초에 나갈 수 없습니다 (§6.0 — 자격이 조합의 전제).
        var problem = PartyFormation.Check(party);
        if (problem != FormationProblem.None)
        {
            Ui.Note($"이 조합으로는 나갈 수 없습니다 — {problem.ToKorean()}");
            return;
        }

        // 파견 전에 훈련을 결산합니다 — 그러지 않으면 훈련한 달이 이력에서 사라집니다.
        foreach (var fighter in party) SettleTraining(fighter);

        var (session, result) = RunDeployment(party, contract);

        ApplyDeploymentResults(party, session, result);

        // 실제로 보낸 달만큼 칸을 잠급니다. 중도 이탈했으면 그만큼만 잠기고 나머지는 풀립니다 —
        // 달력 잠금은 구속이 아니라 예약입니다 (§17.7).
        int until = AbsoluteMonth + result.MonthsSpent - 1;
        foreach (var fighter in party) _bookedUntil[fighter.Id] = until;

        Ui.Note($"{string.Join(" · ", party.Select(p => p.Name))} — {result.MonthsSpent}달 예약" +
                (result.MonthsSpent < contract.Months ? $" (의뢰 {contract.Months}달 중 조기 복귀)" : ""));

        // 함께 나간 달을 장부에 쌓습니다. 의뢰 기간만큼 누적됩니다 —
        // 이것이 정규 파티 등록 조건(함께 나간 6개월)의 유일한 입력원입니다.
        for (int m = 0; m < result.MonthsSpent; m++) _parties.RecordMonth(party);

        PartyPhase(party, result);
    }

    /// <summary>
    /// 파견이 끝난 뒤의 파티 처리 — 평가 배분, 등록 제안, 증원.
    /// </summary>
    private void PartyPhase(List<Adventurer> party, DeploymentResult result)
    {
        var existing = _parties.RegularPartyOf(party);

        // 정규 파티로 나갔으면 그 파티가 평가를 쌓습니다 (§6.2 — 독립적으로 쌓임).
        if (existing is not null && result.Succeeded)
        {
            _parties.RecordEvaluation(existing, result.Contract.Difficulty * EvaluationPerDifficulty);
            Ui.Note($"{existing}");
        }

        Display.Parties(_parties, _members);

        // 등록은 강제가 아닙니다 (§6.0). 조건을 채웠을 때만 물어봅니다.
        var options = _parties.RegistrableCompositions(_members);
        if (options.Count == 0) return;

        var labels = options
            .Select(c => string.Join(" · ", c.MemberIds.Select(id => _members.First(m => m.Id == id).Name))
                         + $" (함께 {_parties.MonthsTogether(c)}달)")
            .Append("등록하지 않는다")
            .ToList();

        int pick = Ui.Choose("   정규 파티로 등록하시겠습니까", labels);
        if (pick >= options.Count) return;

        var chosenComposition = options[pick];
        var members = chosenComposition.MemberIds.Select(id => _members.First(m => m.Id == id)).ToList();

        var registered = _parties.Register($"P{_parties.Parties.Count}", $"{members[0].Name}의 파티", members);
        if (registered is not null)
        {
            Record($"{_year}년: 정규 파티 등록 — {string.Join(" · ", members.Select(m => m.Name))}");
            Ui.Note($"등록되었습니다. {registered}");
        }
    }

    /// <summary>난이도 1당 파티 평가. ⚠️ 임시값 — docs/08 #41.</summary>
    private const int EvaluationPerDifficulty = 4;

    /// <summary>
    /// 파견 한 건을 달 단위로 진행합니다.
    /// <para>
    /// <b>플레이어가 고르는 것은 편성과 보급뿐</b>입니다. 일할지 쉴지는 모험가가 판단하고,
    /// 플레이어가 끼어드는 곳은 전투 안입니다 (docs/07 §17.5).
    /// </para>
    /// </summary>
    private (DeploymentSession Session, DeploymentResult Result) RunDeployment(
        List<Adventurer> party, Contract contract)
    {
        var session = new DeploymentSession(
            party, contract, rng.Fork($"deploy:{_year}:{contract.Id}"),
            Supplies.UpTo(party, Supplies.CapacityOf(party)));

        bool manual = Ui.Confirm("   전투에 직접 개입하시겠습니까?");
        var commander = manual ? new ConsoleCommander() : null;

        Ui.Line();
        Ui.Line("   ── 파견 ──");

        while (!session.IsComplete)
        {
            Display.FieldStatus(session, party);

            var month = session.AdvanceMonth(
                rng.Fork($"battle:{_year}:{contract.Id}:{session.CurrentMonth}"),
                commander,
                manual ? line => Ui.Line("       " + line) : null);

            Ui.Line($"     {month.Note}");

            // 손절할 기회를 줍니다 — 끝까지 밀어서 무너지느냐, 빈손이라도 사람을 지키느냐.
            if (!session.IsComplete && session.HealthRatio < 0.4
                && Ui.Confirm("   상태가 좋지 않습니다. 의뢰를 포기하고 돌아오겠습니까?"))
            {
                session.Abandon();
            }
        }

        var result = session.Complete();

        Ui.Line();
        Ui.Note(result.Succeeded
            ? $"성공 — {result.Progress}{contract.Form.IntensityLabel()}."
            : $"실패 ({result.Failure}) — {result.MonthsSpent}/{contract.Months}달.");

        return (session, result);
    }

    /// <summary>파견 결과를 각자에게 적용합니다.</summary>
    private void ApplyDeploymentResults(
        List<Adventurer> party,
        DeploymentSession session,
        DeploymentResult result)
    {
        var contract = result.Contract;
        int totalIncome = 0;

        foreach (var fighter in party)
        {
            var record = CareerSimulator.ResolveDeployment(
                fighter, session, result, rng.Fork($"settle:{_year}:{fighter.Id}"));

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

        // 길드 자체 의뢰는 보수가 아니라 명성으로 돌아옵니다 (§17.2).
        int reputationGain = result.Succeeded
            ? contract.Difficulty * (contract.Reward == RewardKind.Renown ? 2 : 1)
            : -1;

        _funds += totalIncome;
        _reputation = Math.Max(0, _reputation + reputationGain);
        Ui.Note($"보수 {totalIncome}, 평판 {(reputationGain >= 0 ? "+" : "")}{reputationGain}");

        // 죽거나 불구가 되면 파티에서 빠지고, 1명 남으면 자동 해체됩니다 (§6.1).
        foreach (var lost in _members.Where(m => m.Status is AdventurerStatus.Dead or AdventurerStatus.Crippled))
        {
            var theirs = _parties.RegularPartyOf(lost.Id);
            if (_parties.Leave(lost.Id) && theirs is { Disbanded: true })
            {
                Record($"{_year}년: {theirs.Name} 해체 — 남은 인원이 1명");
                Ui.Note($"{theirs.Name}이(가) 해체되었습니다. 빈자리는 다시 6개월이 걸립니다.");
            }
        }

        foreach (var lost in _members.Where(m => m.Status is AdventurerStatus.Dead or AdventurerStatus.Crippled))
        {
            _bookedUntil.Remove(lost.Id);
            _training.Remove(lost.Id);
        }

        _members.RemoveAll(m => m.Status is AdventurerStatus.Dead or AdventurerStatus.Crippled);
    }

    // ── 연말 ────────────────────────────────────────────────

    private void Retire(Adventurer member)
    {
        SettleTraining(member);
        _bookedUntil.Remove(member.Id);
        _parties.Leave(member.Id);

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
        // 해가 끝나면 진행 중인 훈련을 결산합니다 — 몇 달만 훈련했어도 그만큼 반영됩니다.
        foreach (var member in _members.ToList()) SettleTraining(member);

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
