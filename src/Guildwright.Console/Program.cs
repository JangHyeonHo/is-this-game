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

    private readonly List<Adventurer> _retired = [];
    private readonly List<string> _chronicle = [];

    private int _funds = StartingFunds;

    /// <summary>이번 해 모집을 이미 했는가 (튜토리얼 귀환 직후 모집과 1월 모집의 중복 방지).</summary>
    private int _recruitDoneForYear;

    /// <summary>다음 모집에서 뽑을 수 있는 최대 인원. 튜토리얼 직후 해는 1명입니다 (§1).</summary>
    private int? _recruitLimit;
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

    /// <summary>지난달에 시킨 훈련 — Enter 한 번으로 반복하기 위해 기억합니다.</summary>
    private readonly Dictionary<string, TrainingActivity> _lastTraining = [];

    /// <summary>지난 달에서 넘어온 지속 의뢰 (승급 의뢰 · 전개상 필수).</summary>
    private IReadOnlyList<Contract> _carriedOver = [];

    /// <summary>이번 달 게시판 (수락되면 여기서 빠집니다 — 같은 의뢰를 두 파티가 받을 수는 없습니다).</summary>
    private List<Contract> _monthBoard = [];

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
        Ui.Line();
        Ui.Note("마물이 언제든 내려오는 세계. 나라는 멀고, 마을은 스스로를 지킬 수 없습니다.");
        Ui.Note("당신은 이름 없는 신생 길드의 주인입니다 — 사람을 뽑고, 키우고, 내보내십시오.");
        Ui.Note("(그만두고 싶으면 연말 결산에서 고르거나 Ctrl+C)");

        // 튜토리얼은 건너뛸 수 있습니다 (§1) — "한 번 더"가 판단 기준인 게임에서
        // 같은 튜토리얼을 강제하면 그게 무너집니다.
        if (Ui.Choose("어떻게 시작합니까",
                ["처음부터 (첫 해를 함께 — 권장)", "튜토리얼 건너뛰기 (자유 시작)"], 0) == 0)
        {
            TutorialPrologue();
        }

        while (true)
        {
            // 매년 1월에 길드원 모집이 열립니다 (§17.10).
            if (Calendar.IsRecruitmentMonth(_month) && _recruitDoneForYear != _year)
            {
                RecruitPhase();
            }

            if (_members.Count == 0)
            {
                Ui.Note("단원이 없습니다. 길드는 문을 닫았습니다.");
                ShowChronicle();
                return;
            }

            MonthPhase();

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

                // 한 해가 게임의 자연스러운 마디입니다. 종료를 물을 자리는 여기뿐입니다 —
                // 달마다 물으면 흐름이 끊기고, 안 물으면 그만둘 방법이 Ctrl+C뿐입니다.
                if (Ui.Choose($"{_year}년이 저물었습니다", ["새해로", "여기서 마친다 (연대기를 보고 종료)"]) == 1)
                {
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

    /// <summary>
    /// 지금까지 산 달 — 결산 전의 훈련 달을 포함합니다.
    /// 결산 값만 보면 "실전까지 3달"이 12달 내내 동결된 채 표시됩니다.
    /// </summary>
    private int LivedMonths(Adventurer a) => a.MonthsElapsed; // 달수는 매달 본체에 반영됩니다

    /// <summary>실전에 나갈 수 있는가 — 진행 중인 훈련 달을 포함해 판정합니다.</summary>
    private bool CanDeployNow(Adventurer a) =>
        a.Status == AdventurerStatus.Active
        && (a.CanDeploy || LivedMonths(a) >= Adventurer.MonthsPerYear);

    /// <summary>그 사람이 지금 예약되어 있는가.</summary>
    private bool IsBooked(Adventurer a) =>
        _bookedUntil.TryGetValue(a.Id, out int until) && until >= AbsoluteMonth;

    private int BookedMonthsLeft(Adventurer a) =>
        _bookedUntil.TryGetValue(a.Id, out int until) ? Math.Max(0, until - AbsoluteMonth + 1) : 0;

    /// <summary>
    /// 한 달을 진행합니다. 매달 정책을 정합니다 (§17.10).
    /// <para>
    /// 화면 순서가 곧 판단 순서입니다 — 길드가 지금 어떤가(현황판) → 이번 달 무엇이
    /// 있나(게시판) → 그래서 누구에게 무엇을 시키나(행동). 행동의 결과는 그 자리에서
    /// 보여줍니다. 달이 끝나면 묻지 않고 넘어갑니다.
    /// </para>
    /// </summary>
    private void MonthPhase()
    {
        var season = Calendar.SeasonOf(_month);

        // 게시판은 매달 새로 뜨고, 안 받으면 사라집니다. 지속 의뢰만 남습니다 (§17.8).
        var board = ContractBoard.Post(
            rng.Fork($"board:{_year}:{_month}"), _month, GuildRank, _carriedOver);
        _carriedOver = board;

        _monthBoard = board.ToList();
        var free = _members.Where(m => m.Status == AdventurerStatus.Active && !IsBooked(m)).ToList();

        // 전원이 파견 중인 달은 한 줄로 흘러갑니다 — 시킬 것이 없는데 화면만 크면 소음입니다.
        if (free.Count == 0)
        {
            Ui.Line($"   {_year}년 {_month}월 — 자유 단원 없음");
            AdvanceDeployments();
            return;
        }

        Ui.Section($"{_year}년 {_month}월 · {season.ToKorean()}   자금 {_funds} · 평판 {_reputation}({GuildRank.Label()}) · 단원 {_members.Count}/{RosterCapacity}");

        // 현황판 — 전원 한 줄씩. 파견 중인 사람도 어디서 뭘 하는지 보입니다.
        foreach (var m in _members)
        {
            string state = IsBooked(m)
                ? $"의뢰 중 · {BookedMonthsLeft(m)}달 남음"
                : _training.TryGetValue(m.Id, out var t)
                    ? $"피로 {t.Fatigue} · {t.Condition.ToKorean()}"
                    : "대기";
            Ui.Line($"   {m.Name,-14} {m.Title} · {m.Rank.Label()} · {m.Age}세   {state}");
        }

        // 정규 파티와, 등록이 가까워지는 조합의 게이지. 6개월이 차오르는 게 보여야
        // "같이 내보내서 파티를 만든다"가 목표가 됩니다 (§6.0).
        foreach (var party in _parties.ActiveParties)
        {
            Ui.Line($"   ★ {party.Name} [{party.Rank.Label()}] 평가 {party.Evaluation}/{PartyRules.EvaluationNeeded(party.Rank)}" +
                    (party.ReadyToPromote ? " — 승급 의뢰를 받을 수 있음" : ""));
        }
        foreach (var (comp, months) in _parties.VirtualParties
                     .Where(v => v.Months > 0 && _parties.RegularPartyOf(v.Composition.MemberIds[0]) is null)
                     .OrderByDescending(v => v.Months).Take(2))
        {
            var names = comp.MemberIds
                .Select(id => _members.FirstOrDefault(m => m.Id == id)?.Name)
                .Where(n => n is not null).ToList();
            if (names.Count < comp.MemberIds.Count) continue;
            int shown = Math.Min(months, PartyRules.MonthsToRegister);
            string tag = months >= PartyRules.MonthsToRegister ? " — 정규 등록 가능" : "";
            Ui.Line($"   ☆ {string.Join("·", names)} — 함께 {shown}/{PartyRules.MonthsToRegister}개월 {Ui.Bar((double)shown / PartyRules.MonthsToRegister, 6)}{tag}");
        }

        // 게시판 — 이걸 보고 누구를 보낼지 정합니다.
        Ui.Line();
        Ui.Line($"   [게시판] {season.ToKorean()}에는 {string.Join("·", ContractBoard.WeightsIn(season).OrderByDescending(w => w.Value).Take(2).Select(w => w.Key.ToKorean()))}이 많습니다");
        foreach (var c in board) Ui.Line($"     {Display.ContractLine(c)}");

        foreach (var member in free)
        {
            if (member.Status != AdventurerStatus.Active || IsBooked(member)) continue;

            // 전직은 자유이고 비용도 없습니다 (§16.4) — 달을 쓰지 않으므로,
            // 전직한 뒤 이번 달의 행동을 다시 고릅니다.
            bool blockedNoteShown = false;
            while (true)
            {
                bool canDeploy = CanDeployNow(member);
                var (openNow, blockedPromotion) = canDeploy
                    ? OpenContractsFor(member, _monthBoard)
                    : ((IReadOnlyList<Contract>)[], null);

                // 사다리가 막혀 있으면 다음 한 수를 알립니다 — 침묵하면 5년을 E급으로 보냅니다.
                if (blockedPromotion is not null && !blockedNoteShown)
                {
                    blockedNoteShown = true;
                    Ui.Note($"{blockedPromotion.Name}(난이도 {blockedPromotion.Difficulty})가 걸려 있지만 " +
                            $"수주 자격(현재 {member.MaxContractDifficulty})이 모자랍니다 — " +
                            $"수주 난이도 {blockedPromotion.Difficulty} 이상 직업으로 전직이 먼저입니다.");
                }

                // 동행이 없고 솔로 자격(D급)도 없으면 게시판은 헛걸음입니다 — 옵션을 빼고 이유를 말합니다.
                bool hasCompany = _members.Any(m => m.Id != member.Id && CanDeployNow(m) && !IsBooked(m));
                bool canGoOut = hasCompany || member.Rank >= PartyRules.SoloingUnlock;
                if (canDeploy && openNow.Count > 0 && !canGoOut && !blockedNoteShown)
                {
                    blockedNoteShown = true;
                    Ui.Note($"함께 나갈 사람이 없습니다 — 혼자 나가는 것은 {PartyRules.SoloingUnlock.Label()}부터입니다.");
                }

                var choices = new List<string>();
                if (canDeploy && openNow.Count > 0 && canGoOut) choices.Add($"의뢰를 받는다 ({openNow.Count}건)");
                choices.Add("훈련한다 (휴식 포함)");

                var upgrades = UpgradesFor(member);
                int better = upgrades.Count(j => j.MaxContractDifficulty > member.MaxContractDifficulty);
                if (better > 0) choices.Add($"전직한다 (상위 {better}개 해금 · 달을 쓰지 않음)");
                choices.Add("상세를 본다 (달을 쓰지 않음)");
                choices.Add("은퇴시킨다");

                if (!canDeploy)
                {
                    Ui.Note($"{member.Name}: 실전까지 {Math.Max(0, Adventurer.MonthsPerYear - LivedMonths(member))}달 (첫 12달은 훈련)");
                }

                int choice = Ui.Choose($"   {member.Name}, {_month}월에 무엇을", choices);
                string picked = choices[choice];

                if (picked.StartsWith("전직"))
                {
                    ChangeJob(member, upgrades);
                    continue;
                }
                if (picked.StartsWith("상세"))
                {
                    Display.StatSheet(member);
                    ShowSessionGains(member);
                    // 계열 전향(수평 이동)은 상세에서만 — 매달 메뉴에 열 개씩 늘어놓을 일이 아닙니다.
                    if (upgrades.Count > 0 && Ui.Confirm("   전직 목록을 보시겠습니까"))
                    {
                        ChangeJob(member, upgrades);
                    }
                    continue;
                }

                if (picked.StartsWith("의뢰"))
                {
                    if (!TryAcceptContract(member, _monthBoard)) continue; // 취소·편성 불가 — 달을 쓰지 않음
                }
                else if (picked.StartsWith("훈련"))
                {
                    if (!TrainMonth(member)) continue; // 돌아가기 — 달을 쓰지 않음
                }
                else
                {
                    // 은퇴는 되돌릴 수 없습니다 — 실수 클릭 하나로 잃게 두지 않습니다.
                    if (!Ui.Confirm($"   {member.Name}을(를) 정말 은퇴시킵니까 (되돌릴 수 없음)")) continue;
                    Retire(member);
                }
                break;
            }
        }

        AdvanceDeployments();
    }

    /// <summary>이번 훈련 세션에서 쌓였지만 아직 결산 전인 성장을 보여줍니다.</summary>
    private void ShowSessionGains(Adventurer member)
    {
        if (!_training.TryGetValue(member.Id, out var session) || session.MonthsCompleted == 0) return;

        var parts = PrimaryStats.AllStats
            .Select(k => (k, g: session.AccumulatedGain(k)))
            .Where(x => x.g >= 0.05)
            .Select(x => $"{x.k.ToKorean()} +{x.g:F1}");
        string text = string.Join(" ", parts);
        if (text.Length > 0) Ui.Note($"이번 훈련 기간 성장: {text}");
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
            .Select(j =>
            {
                var weapon = Adventurer.StartingWeaponFor(j.Id);
                return $"{j.Korean} — {weapon.ToKorean()} · 액티브 슬롯 {j.ActiveSlots} · 수주 난이도 {j.MaxContractDifficulty}" +
                       (j.Grants.Count > 0
                           ? $" · {string.Join(", ", j.Grants.Select(gr => SkillBook.Of(gr).Korean))}"
                           : "");
            })
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

        // 같은 달에 여러 번 갈아타면 마지막 것만 남깁니다 — 연대기가 오염됩니다.
        string prefix = $"{_year}년 {_month}월: {member.Name} 전직";
        if (_chronicle.Count > 0 && _chronicle[^1].StartsWith(prefix)) _chronicle.RemoveAt(_chronicle.Count - 1);
        Record($"{prefix} — {was} → {member.Title}");
        string actives = member.Actives.Count > 0
            ? $" · 액티브 {string.Join(", ", member.Actives.Select(id => SkillBook.Of(id).Korean))}"
            : "";
        Ui.Note($"{was} → {member.Title}. 수주 난이도 {member.MaxContractDifficulty}{actives}");
    }

    /// <summary>
    /// 한 달 훈련합니다 (휴식도 활동의 하나입니다). 성장은 결산 때 확정되지만,
    /// <b>화면은 그 달에 오른 것을 그 자리에서 보여줍니다</b> — 매달 정하는 게임에서
    /// 피드백이 연 단위면 판단할 재료가 없습니다.
    /// </summary>
    /// <returns>이 달을 썼는가. 돌아가면 달을 쓰지 않습니다.</returns>
    private bool TrainMonth(Adventurer member, bool allowBack = true)
    {
        var session = SessionFor(member);

        // 판단 재료를 묻기 전에 줍니다 — 현재 능력치·숙련 없이 7지선다는 감입니다.
        var st = member.Stats;
        Ui.Note($"힘{st.Strength} 민{st.Agility} 기{st.Finesse} 활{st.Vitality} 지{st.Intellect} 정{st.Spirit} · " +
                $"{member.Loadout.MainWeapon.ToKorean()} 숙련 {member.Proficiency[member.Loadout.MainWeapon]}");
        ShowSessionGains(member);
        Ui.Note($"컨디션 {session.Condition.ToKorean()} (성장 배율 {session.Condition.Multiplier():F1}) · 피로 {session.Fatigue}" +
                (session.FailureChance > 0 ? $" · 실패 확률 {session.FailureChance:P0}" : ""));

        // 활동마다 "이 아이가" 얼마나 오를지 기대값을 붙입니다 (§2.4 예보).
        // 가중치 점만으로는 캐릭터별 차이가 안 보입니다.
        var menu = Display.FocusMenu()
            .Select((label, i) =>
            {
                var preview = session.PreviewMonth(Display.FocusFromIndex(i))
                    .Where(x => x.Gain >= 0.05)
                    .OrderByDescending(x => x.Gain)
                    .Take(3)
                    .Select(x => $"{x.Stat.ToKorean()}+{x.Gain:F1}")
                    .ToList();
                return preview.Count > 0 ? $"{label} → 예상 {string.Join(" ", preview)}" : label;
            })
            .ToList();
        if (allowBack) menu.Add("돌아간다");
        int? last = _lastTraining.TryGetValue(member.Id, out var prev) ? Display.FocusIndexOf(prev) : null;
        int pick = Ui.Choose("   무엇을 훈련할까요", menu, last);
        if (allowBack && pick >= menu.Count - 1) return false;
        _lastTraining[member.Id] = Display.FocusFromIndex(pick);

        var before = PrimaryStats.AllStats.ToDictionary(k => k, session.AccumulatedGain);
        var outcome = session.AdvanceMonth(Display.FocusFromIndex(pick));

        var gained = PrimaryStats.AllStats
            .Select(k => (k, d: session.AccumulatedGain(k) - before[k]))
            .Where(x => x.d >= 0.05)
            .Select(x => $"{x.k.ToKorean()} +{x.d:F1}")
            .ToList();

        string change = gained.Count > 0 ? string.Join(" ", gained) : "성장 없음";
        string prof = outcome.ProficiencyGain >= 0.05 ? $" · 숙련 +{outcome.ProficiencyGain:F1}" : "";

        Ui.Line($"     {_month}월: {Display.FocusName(outcome.Activity)} · {outcome.Grade.ToKorean()} — " +
                $"{change}{prof} · 피로 {session.Fatigue}");
        if (outcome.Failed) Ui.Note("무리했습니다 — 이 달의 성장 대부분을 잃었습니다.");

        // 12달을 채우면 그 자리에서 결산하고 새 세션을 엽니다.
        if (session.IsComplete) SettleTraining(member);
        return true;
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

        Ui.Note($"[훈련 결산] {record.Note} — {(member.Stats - before)}");
    }

    // ── 모집 ────────────────────────────────────────────────

    private void RecruitPhase()
    {
        Ui.Section($"{_year}년 {_month}월 · 모집   자금 {_funds} · 단원 {_members.Count}/{RosterCapacity}");

        var candidates = new List<Adventurer>();
        for (int i = 0; i < NamePoolSize; i++)
        {
            candidates.Add(Adventurer.Recruit($"A{_nextId++}", UniqueName(candidates), rng.Fork($"recruit:{_year}:{i}")));
        }

        var labels = new List<string>();
        Ui.Line();

        // 평가서는 없습니다 (docs/07 §19 — 사용자 결정으로 제거). 보이는 것은 지금의
        // 능력치와 희망 직업뿐이고, 성장 곡선은 키워 보기 전에는 알 수 없습니다.
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            Ui.Line($"   ── 후보 {i + 1} ──");
            Ui.Line($"   {c.Name} ({c.Age}세) · 희망 직업: {c.Title}");
            Ui.Line($"   힘{c.Stats.Strength} 민{c.Stats.Agility} 기{c.Stats.Finesse} " +
                    $"활{c.Stats.Vitality} 지{c.Stats.Intellect} 정{c.Stats.Spirit}");
            Ui.Line();
            labels.Add($"{c.Name} 영입 (계약금 {RecruitCost})");
        }

        // 길드 랭크가 최대 인원을 정합니다 (§17.10).
        _recruitDoneForYear = _year;
        int room = Math.Max(0, RosterCapacity - _members.Count);
        int affordable = Math.Min(room, Math.Max(0, _funds / RecruitCost));
        if (_recruitLimit is { } cap) { affordable = Math.Min(affordable, cap); _recruitLimit = null; }

        if (room == 0) Ui.Note($"정원이 찼습니다 ({RosterCapacity}명). 랭크가 올라야 늘어납니다.");
        else if (affordable == 0) Ui.Note($"자금이 모자랍니다 (계약금 {RecruitCost}).");

        if (affordable == 0) return;

        var picked = Ui.ChooseMany("영입할 사람", labels, affordable);

        // 단원 0명에서 아무도 안 뽑으면 길드가 문을 닫습니다 — Enter 한 번으로 갈 결말이 아닙니다.
        if (picked.Count == 0 && _members.Count == 0
            && !Ui.Confirm("   아무도 뽑지 않으면 길드는 문을 닫습니다. 정말입니까"))
        {
            picked = Ui.ChooseMany("영입할 사람", labels, affordable);
        }

        foreach (int index in picked)
        {
            _members.Add(candidates[index]);
            _funds -= RecruitCost;
            Record($"{_year}년: {candidates[index].Name} 영입");
        }

        if (picked.Count == 0) Ui.Note("아무도 뽑지 않았습니다.");
    }

    private const int RecruitCost = 150;

    // ── 튜토리얼 (docs/07 §1 확정) ──────────────────────────

    /// <summary>
    /// 튜토리얼 — 고정 캐릭터 1명을 1년 키우고, 평가서가 틀렸음을 보여주고,
    /// 주인공이 짐꾼으로 동행하는 첫 의뢰를 나갑니다.
    /// <para>
    /// 이 게임이 가르치는 것은 메뉴 사용법이 아니라 <b>평가서는 틀린다</b>는 사실입니다 (§1).
    /// 그래서 캐릭터는 고정이고, 성장 곡선은 반드시 평가서와 어긋나게 심어져 있습니다.
    /// </para>
    /// </summary>
    private void TutorialPrologue()
    {
        Ui.Section("1년 봄 — 첫 단원");
        Ui.Note("길드의 첫 지원자가 문을 두드립니다. 맨손의 열다섯 살입니다.");

        // 고정 캐릭터 — 평범형입니다 (docs/07 §19). 첫 해의 목적은 육성 손맛을 익히는 것이라
        // 극단적인 곡선보다 표준적인 성장이 낫습니다.
        var first = new Adventurer(
            "T0", "리안(떠돌이)",
            new PrimaryStats(11, 11, 12, 12, 10, 10),
            judgement: 16,
            new GrowthProfile
            {
                PeakAge = 21, BloomWidth = 5.0,
                Temperament = Temperament.Balanced,
                Potential = new PrimaryStats(66, 62, 64, 68, 55, 58),
                DeclineAge = 36
            },
            loadout: WoodenSwordOnly());
        _members.Add(first);

        // 나무검은 무기 종류가 아니라 <b>재질</b>이다 (§20.7) — 검 숙련이 진짜 무장 뒤에도 이어진다.
        static Loadout WoodenSwordOnly()
        {
            var loadout = new Loadout();
            loadout.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Sword, WeaponMaterial.Wood);
            return loadout;
        }

        Ui.Line($"   {first.Name} ({first.Age}세) · 희망 직업: {first.Title}");
        Ui.Line($"   힘{first.Stats.Strength} 민{first.Stats.Agility} 기{first.Stats.Finesse} " +
                $"활{first.Stats.Vitality} 지{first.Stats.Intellect} 정{first.Stats.Spirit}");
        Ui.Note("성장 곡선은 보이지 않습니다 — 어떤 아이인지는 키워 봐야 압니다.");
        Ui.Note("무기고가 비어 있어 나무검을 쥐여 줍니다.");
        Ui.Pause("1년 동안 이 아이를 키웁니다 — 계속하려면 Enter");

        // 1년 육성 — 본편과 같은 화면으로.
        for (_month = 1; _month <= Calendar.MonthsPerYear; _month++)
        {
            Ui.Section($"1년 {_month}월 · {Calendar.SeasonOf(_month).ToKorean()} — 육성");
            while (!TrainMonth(first, allowBack: false)) { }
        }
        SettleTraining(first);

        // 첫 의뢰 — 주인공이 짐꾼으로 동행합니다 (2인 충족 · §1).
        // 주인공은 싸우지 않습니다. 싸우면 1년 훈련의 결과가 화면에 나타나지 않습니다.
        Ui.Section("2년 봄 — 첫 의뢰");
        // 10마리는 §1 확정. 솔로 전투는 적이 1마리씩 나오므로 (한 달 최대 1처치)
        // 기간이 10달 미만이면 산술적으로 성공이 불가능합니다 — 12달로 여유 2를 둡니다.
        var tutorial = new Contract(
            "tutorial", "가도 정리", ContractForm.Subjugate, ContractSource.Village,
            Difficulty: 1, Months: 12, Intensity: 10, Objective: "고블린 무리");
        Ui.Line($"     {Display.ContractLine(tutorial)}");

        // 1년을 버틴 보상 — 나무검을 거두고 진짜 무기를 쥐여 줍니다.
        first.Reequip();
        Ui.Note($"나무검을 거두고 진짜 무장을 갖춥니다 — {first.Loadout}.");
        Ui.Note("당신이 직접 가방을 메고 동행합니다 — 싸우지 않고, 위기에 회복약을 건넵니다.");

        var hero = new Adventurer(
            "HERO", "당신", PrimaryStats.Uniform(25), judgement: 80,
            new GrowthProfile
            {
                PeakAge = 22, BloomWidth = 4.0, Temperament = Temperament.Balanced,
                Potential = PrimaryStats.Uniform(30), DeclineAge = 40
            },
            age: 26, job: JobId.Porter);

        var party = new List<Adventurer> { first, hero };
        bool manual = Ui.Confirm("   전투에 직접 개입하시겠습니까 (권장 — 개입이 이 게임의 손맛입니다)");

        var session = new DeploymentSession(
            party, tutorial, rng.Fork("tutorial:deploy"),
            Supplies.UpTo(party, Supplies.CapacityOf(party)), Names.Monster);
        var commander = manual ? new ConsoleCommander() : null;

        for (_month = 1; !session.IsComplete && _month <= tutorial.Months; _month++)
        {
            Ui.Section($"2년 {_month}월 — 가도 정리");
            Display.FieldStatus(session, party);
            var monthLog = session.AdvanceMonth(
                rng.Fork($"tutorial:battle:{_month}"), commander,
                manual ? line => Ui.Line("       " + line) : null);
            Ui.Line($"     {monthLog.Note}");

            if (!session.IsComplete && session.HealthRatio < 0.4
                && Ui.Confirm("   상태가 좋지 않습니다. 포기하고 돌아옵니까"))
            {
                session.Abandon();
            }
        }

        var result = session.Complete();
        Ui.Line();
        Ui.Note(result.Succeeded
            ? $"가도 정리 완료 — 첫 의뢰였습니다."
            : $"실패 ({result.Failure.ToKorean()}) — 살아 돌아온 것으로 충분합니다. 다음이 있습니다.");
        // 결산은 단원만 — 주인공은 성장도 보수도 받지 않습니다 (§1: 이후 파견을 나가지 않음).
        ApplyDeploymentResults([first], session, result);
        Record($"1년: 리안(떠돌이) 합류 · 2년: 첫 의뢰 {(result.Succeeded ? "성공" : "실패")}");

        // 주인공은 이후 파견을 나가지 않습니다 (§1) — 길드에 사람이 생겼기 때문입니다.
        _members.Remove(hero);
        Ui.Note("당신은 가방을 내려놓습니다. 이제부터 나가는 것은 단원들입니다.");

        // 귀환 직후 신규 1명 모집 (§1 — 모집은 1명씩).
        _year = 2;
        _month = Math.Min(result.MonthsSpent + 1, Calendar.MonthsPerYear);
        _recruitLimit = 1;
        RecruitPhase();
    }

    /// <summary>겹치지 않는 이름을 뽑습니다 — 동명이인은 연대기와 명단을 유령의 집으로 만듭니다.</summary>
    private string UniqueName(IReadOnlyList<Adventurer> pending)
    {
        for (int tries = 0; tries < 40; tries++)
        {
            string name = Names.Next(rng);
            bool taken = _members.Any(m => m.Name == name) || _retired.Any(r => r.Name == name)
                         || pending.Any(c => c.Name == name);
            if (!taken) return name;
        }
        return Names.Next(rng);
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

    /// <summary>이 사람이 지금 받을 수 있는 의뢰. 승급 의뢰가 자격에 막혀 있으면 그 사실도 돌려줍니다.</summary>
    private (IReadOnlyList<Contract> Open, Contract? BlockedPromotion) OpenContractsFor(
        Adventurer member, IReadOnlyList<Contract> board)
    {
        var promotion = ContractBoard.PromotionFor(member);
        IReadOnlyList<Contract> posted = promotion is null ? board : [.. board, promotion];

        // 개인 자격은 개인 등급으로 판정합니다. 파티 등급으로 판정하면 F급 파티에 속했다는
        // 이유로 개인 승급이 막히고, 안내문이 엉뚱한 처방(전직)을 내립니다.
        var open = ContractBoard.AvailableTo(
            posted, member.Rank, asRegularParty: false, member.MaxContractDifficulty).ToList();

        // 파티 전용 의뢰는 파티 등급으로.
        var regular = _parties.RegularPartyOf(member.Id);
        if (regular is not null && ContractBoard.PromotionFor(regular) is { } partyQuest)
        {
            open.AddRange(ContractBoard.AvailableTo(
                [partyQuest], regular.Rank, asRegularParty: true, member.MaxContractDifficulty));
        }

        var blocked = promotion is not null && !open.Contains(promotion) ? promotion : null;
        return (open, blocked);
    }

    /// <summary>진행 중인 파견 하나.</summary>
    private sealed record ActiveDeployment(
        DeploymentSession Session, List<Adventurer> Party, IBattleCommander? Commander, bool Manual);

    private readonly List<ActiveDeployment> _active = [];

    /// <summary>
    /// 그 달에 의뢰를 받습니다. 받으면 그 기간만큼 칸이 잠기고, <b>파견은 달력과 함께
    /// 한 달씩 진행됩니다</b> — 수락 즉시 결말을 보여주면 파견의 긴장이 구조적으로 죽습니다.
    /// </summary>
    /// <returns>이 달을 썼는가. 취소하거나 편성이 성립하지 않으면 달을 쓰지 않습니다.</returns>
    private bool TryAcceptContract(Adventurer member, IReadOnlyList<Contract> board)
    {
        var (open, _) = OpenContractsFor(member, board);
        if (open.Count == 0) return false;

        Ui.Line();
        Ui.Line("   ── 의뢰 게시판 ──");
        int chosen = Ui.Choose("   무엇을 받겠습니까",
            [.. open.Select(Display.ContractLine), "돌아간다"]);
        if (chosen >= open.Count) return false;
        var contract = open[chosen];

        while (true)
        {
            var party = new List<Adventurer> { member };

            // 예약된 사람은 못 데려갑니다 — 그 사람의 그 기간은 이미 다른 의뢰에 잠겨 있습니다.
            var others = _members
                .Where(m => m.Id != member.Id && CanDeployNow(m) && !IsBooked(m))
                .ToList();

            if (others.Count > 0)
            {
                // 총 5인까지 — §17.4가 든 예가 "다섯을 보내면"입니다. 최대 인원의 확정은 아직 없습니다.
                var picks = Ui.ChooseMany("   함께 보낼 동료 (없으면 Enter)",
                    others.Select(o => $"{o.Name} · {o.Title} · {o.Rank.Label()} ({o.Loadout})").ToList(), 4);
                party.AddRange(picks.Select(i => others[i]));
            }

            // 편성이 성립하지 않으면 그 자리에서 다시 짭니다 — 달을 태우지 않습니다.
            var problem = PartyFormation.Check(party);
            if (problem != FormationProblem.None)
            {
                Ui.Note($"이 조합으로는 나갈 수 없습니다 — {problem.ToKorean()}.");
                if (others.Count == 0) return false; // 바꿀 여지가 없으면 되짚을 것도 없습니다
                if (Ui.Choose("   어떻게 할까요", ["편성을 다시 짠다", "의뢰를 취소한다"]) == 1) return false;
                continue;
            }

            int capacity = Supplies.CapacityOf(party);
            Ui.Note($"{string.Join(" · ", party.Select(f => f.Name))} — {contract.Months}달 · 짐 한도 {capacity}개");

            int go = Ui.Choose("   이 편성으로 출발합니까",
                ["출발한다", "편성을 다시 짠다", "의뢰를 취소한다"]);
            if (go == 1) continue;
            if (go == 2) return false;

            // 파견 전에 훈련을 결산합니다 — 그러지 않으면 훈련한 달이 이력에서 사라집니다.
            foreach (var fighter in party) SettleTraining(fighter);

            bool manual = Ui.Confirm("   전투에 직접 개입하시겠습니까 (주인공 동행)");

            var session = new DeploymentSession(
                party, contract, rng.Fork($"deploy:{_year}:{_month}:{contract.Id}:{member.Id}"),
                Supplies.UpTo(party, capacity), Names.Monster);

            // 같은 의뢰를 같은 달에 두 파티가 받을 수는 없습니다 (지속 의뢰는 남습니다).
            if (!contract.Persists) _monthBoard.Remove(contract);

            // 기간만큼 예약합니다. 중도 이탈하면 그 시점에 풀립니다 (§17.7 — 구속이 아니라 예약).
            int until = AbsoluteMonth + contract.Months - 1;
            foreach (var fighter in party) _bookedUntil[fighter.Id] = until;

            _active.Add(new ActiveDeployment(session, party, manual ? new ConsoleCommander() : null, manual));
            Ui.Note($"출발 — 결과는 달마다 들어옵니다.");
            return true;
        }
    }

    /// <summary>
    /// 진행 중인 모든 파견을 한 달씩 전진시킵니다. 달의 끝에 호출됩니다.
    /// </summary>
    private void AdvanceDeployments()
    {
        foreach (var dep in _active.ToList())
        {
            var session = dep.Session;
            var contract = session.Contract;

            Display.FieldStatus(session, dep.Party);
            var month = session.AdvanceMonth(
                rng.Fork($"battle:{_year}:{_month}:{contract.Id}:{dep.Party[0].Id}:{session.CurrentMonth}"),
                dep.Commander,
                dep.Manual ? line => Ui.Line("       " + line) : null);
            Ui.Line($"     {month.Note}");

            // 함께 산 달만 장부에 쌓입니다 — 정규 파티 등록 조건(6개월)의 유일한 입력원.
            _parties.RecordMonth(dep.Party);

            // 손절할 기회 — 끝까지 밀어서 무너지느냐, 빈손이라도 사람을 지키느냐.
            if (!session.IsComplete && session.HealthRatio < 0.4
                && Ui.Confirm($"   {contract.Name} — 상태가 좋지 않습니다. 포기하고 돌아옵니까"))
            {
                session.Abandon();
            }

            if (!session.IsComplete) continue;

            _active.Remove(dep);
            var result = session.Complete();

            Ui.Line();
            Ui.Note(result.Succeeded
                ? $"{contract.Name} 완료 — {Math.Min(result.Progress, contract.Intensity)}{contract.Form.IntensityLabel()}."
                : $"{contract.Name} 실패 ({result.Failure.ToKorean()}) — {result.MonthsSpent}/{contract.Months}달.");

            ApplyDeploymentResults(dep.Party, session, result);

            // 남은 예약을 풉니다 — 조기 복귀면 다음 달부터 자유입니다.
            foreach (var fighter in dep.Party) _bookedUntil[fighter.Id] = AbsoluteMonth;

            PartyPhase(dep.Party, result);
        }
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

            // 파티 승급 의뢰를 통과하면 여기서 오릅니다 (§6.5 — 승급은 사건).
            // 이 배선이 없으면 파티는 영원히 F급이고, 승급 의뢰가 무한 파밍이 됩니다.
            if (result.Contract.PartyOnly && result.Contract.IsPromotion && existing.ReadyToPromote)
            {
                _parties.Promote(existing);
                Ui.Note($"★ {existing.Name}, {existing.Rank.Label()} 파티가 되다!");
                Record($"{_year}년 {_month}월: {existing.Name} {existing.Rank.Label()} 승급");
            }

            Ui.Note($"{existing}");
        }

        // 정규 파티 멤버가 아닌 동행이 있었으면 증원을 물어봅니다 (§6.1).
        // 자격은 코어가 검사하고, 증원해도 누적은 새 조합으로 다시 6개월입니다.
        var regularOfAny = party.Select(p => _parties.RegularPartyOf(p.Id)).FirstOrDefault(p => p is not null);
        if (regularOfAny is not null)
        {
            foreach (var guest in party.Where(p => !regularOfAny.Members.Any(m => m.Id == p.Id)))
            {
                if (_parties.CheckAdmission(regularOfAny, guest) != AdmissionProblem.None) continue;

                if (Ui.Confirm($"   {guest.Name}을(를) {regularOfAny.Name}에 증원하시겠습니까 (새 조합의 누적은 6개월부터)"))
                {
                    _parties.Admit(regularOfAny, guest);
                    Record($"{_year}년: {regularOfAny.Name} 증원 — {guest.Name}");
                    Ui.Note($"증원되었습니다. {regularOfAny}");
                }
            }
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
            var was = fighter.Rank;
            var record = CareerSimulator.ResolveDeployment(
                fighter, session, result, rng.Fork($"settle:{_year}:{fighter.Id}"));

            totalIncome += record.Income;
            Ui.Line($"     {record.Note}");

            // 승급은 이 게임의 가장 큰 마일스톤입니다 — 무음으로 지나가게 두지 않습니다.
            if (fighter.Rank > was)
            {
                Ui.Note($"★ {fighter.Name}, {fighter.Rank.Label()} 모험가가 되다!");
                Record($"{_year}년 {_month}월: {fighter.Name} {fighter.Rank.Label()} 승급");
            }

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

        // 보수는 나눕니다 (docs/07 §7) — 파티 평균 등급이 높을수록 모험가 몫이 큽니다.
        // 그래서 쉬운 의뢰에 고수를 보내는 것이 손해가 됩니다.
        var ranks = party.Select(p => p.Rank).ToList();
        int guildTake = CareerRules.GuildTake(totalIncome, ranks);

        _funds += guildTake;
        _reputation = Math.Max(0, _reputation + reputationGain);
        if (contract.Reward == RewardKind.Renown)
        {
            Ui.Note($"명성 의뢰 — 보수 없음, 평판 {(reputationGain >= 0 ? "+" : "")}{reputationGain}");
        }
        else
        {
            Ui.Note($"보수 {totalIncome} — 모험가 몫 {totalIncome - guildTake}(평균 등급 비례 {CareerRules.AdventurerShare(ranks):P0}) · " +
                    $"길드 몫 {guildTake}, 평판 {(reputationGain >= 0 ? "+" : "")}{reputationGain}");
        }

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

        var theirs = _parties.RegularPartyOf(member.Id);
        if (_parties.Leave(member.Id) && theirs is not null)
        {
            Ui.Note(theirs.Disbanded
                ? $"{theirs.Name}이(가) 해체되었습니다 — 남은 인원이 1명."
                : $"{theirs.Name}에서 빠졌습니다. 빈자리의 누적은 다시 6개월입니다.");
            if (theirs.Disbanded) Record($"{_year}년: {theirs.Name} 해체 — {member.Name} 은퇴");
        }

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

        // 유지비는 등급 무관 정액입니다 (docs/07 §7) — 단원이 강해져도 오르지 않습니다.
        // 강한 사람의 비용은 보수 분배에서 나갑니다. 평판은 의뢰에서만 오릅니다.
        int upkeep = _members.Count * CareerRules.AnnualUpkeep;
        _funds -= upkeep;

        Ui.Note($"유지비 지출 {upkeep} ({_members.Count}명 × {CareerRules.AnnualUpkeep})");
        Ui.Note($"남은 자금 {_funds} · 평판 {_reputation}");

        if (_funds < upkeep)
        {
            Ui.Note("⚠ 다음 해 유지비를 감당하기 어렵습니다. 실전에 내보내야 합니다.");
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
