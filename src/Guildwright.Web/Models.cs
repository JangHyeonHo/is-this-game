using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Items;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;

namespace Guildwright.Web;

/// <summary>지금 떠 있는 화면. 흐름은 docs/07-decisions.md §21.</summary>
public enum Screen
{
    Title,
    Opening,
    Recruit,
    Main,
    Characters,
    Shop,
    Prep,
    MonthResult
}

/// <summary>
/// 단원 하나 — 코어 <see cref="Adventurer"/>를 감싸고, 화면 상태(배정·파견 잠금)만 얹는다.
/// 능력치·성장·예보는 전부 코어가 계산한다.
/// </summary>
public sealed class MemberVm
{
    public required Adventurer Adventurer { get; init; }

    /// <summary>진행 중인 훈련 세션. 12달을 채우면 결산하고 비운다.</summary>
    public TrainingYearSession? Session { get; set; }

    /// <summary>0이면 자유. 1 이상이면 파견 중(잠김)이고 남은 달수다. 파견은 아직 코어 연결 전.</summary>
    public int DeployedMonthsLeft { get; set; }
    public string? DeploymentName { get; set; }

    /// <summary>이번 달 배정된 훈련. null이면 미배정.</summary>
    public TrainingActivity? Assigned { get; set; }

    /// <summary>직전에 실행된 달의 결과 — 월 결과 화면이 보여준다.</summary>
    public MonthOutcome? LastOutcome { get; set; }

    public bool Free => DeployedMonthsLeft <= 0;
    public string Name => Adventurer.Name;
    public int Age => Adventurer.Age;
    public string JobLabel => Jobs.Of(Adventurer.Job).Korean;
    public int Fatigue => Session?.Fatigue ?? 0;
    public string ConditionLabel => Session?.Condition.ToKorean() ?? Condition.Normal.ToKorean();

    public IEnumerable<string> ProficiencyTags =>
        Adventurer.Loadout.Held.Distinct()
            .Select(kind => $"{kind.ToKorean()} {Adventurer.Proficiency[kind]}");

    public IEnumerable<string> InnateLabels =>
        Adventurer.Innate.Select(id => SkillBook.Of(id).Korean);
}

/// <summary>
/// 화면 전환과 월 루프의 겉면. 성장 계산은 코어(<see cref="TrainingYearSession"/>)가 하고,
/// 시작 조건(자금 2,000 · 평판 0 · 1년 1월 · 단원 0명 · 매년 1월 모집 · 계약금 150 ·
/// 정원 8)은 콘솔(Guildwright.Console)의 확정 흐름을 그대로 따른다.
/// 파견·정산·의뢰는 아직 연결 전이다.
/// </summary>
public sealed class GameSession
{
    // 콘솔과 같은 시작 조건 (Program.cs StartingFunds 등).
    private const int StartingFunds = 2_000;
    private const int RecruitCost = 150;
    private const int CandidateCount = 3;

    // ⚠️ 자리표시 시드 — 본 게임에서는 새 게임마다 시드를 뽑고 저장 파일에 든다.
    private DeterministicRandom _rng = new("guildwright-web-prototype");
    private int _nextId = 1;
    private int _recruitDoneForYear;

    public Screen Screen { get; private set; } = Screen.Title;
    public event Action? Changed;

    public string GuildName { get; set; } = "";

    public int Year { get; private set; } = 1;
    public int Month { get; private set; } = 1;
    public int Gold { get; private set; } = StartingFunds;
    public int Fame { get; private set; }

    /// <summary>정원 — 콘솔의 RosterCapacity(8 + 랭크×7). 지금은 랭크 F 고정.</summary>
    public int Capacity => 8;

    public string RankLabel => "F";

    /// <summary>연말에 나갈 유지비 — 1인당 정액 (docs/07 §7, CareerRules.AnnualUpkeep).</summary>
    public int UpkeepDue => Members.Count * CareerRules.AnnualUpkeep;

    public List<MemberVm> Members { get; } = [];
    public List<string> Chronicle { get; } = [];
    public MemberVm? Selected { get; set; }

    /// <summary>길드 인벤토리 — 상점에서 산 것, 벗긴 장비가 쌓인다.</summary>
    public Armory Armory { get; private set; } = new();

    /// <summary>훈련 카드 위에 포커스된 활동 — 오른쪽 전체 스탯에 예상 수치를 만든다.</summary>
    public TrainingActivity? Focused { get; set; }

    /// <summary>이번 모집의 후보들. 모집 화면에 들어올 때 만든다.</summary>
    public List<Adventurer> Candidates { get; } = [];

    public string Season => Month switch { <= 3 => "봄", <= 6 => "여름", <= 9 => "가을", _ => "겨울" };
    public IEnumerable<MemberVm> FreeMembers => Members.Where(m => m.Free);
    public int AssignedCount => FreeMembers.Count(m => m.Assigned is not null);
    public int FreeCount => FreeMembers.Count();

    /// <summary>업무 시작 조건 — 단원이 있고, 자유로운 전원의 이번 달이 정해졌다.</summary>
    public bool CanStart => Members.Count > 0 && FreeMembers.All(m => m.Assigned is not null);

    public void Go(Screen screen)
    {
        Screen = screen;
        Raise();
    }

    public void Raise() => Changed?.Invoke();

    /// <summary>새 게임 — 0에서 시작한다. 단원도, 평판도, 이름도 없다.</summary>
    public void NewGame()
    {
        _rng = new DeterministicRandom("guildwright-web-prototype");
        _nextId = 1;
        _recruitDoneForYear = 0;
        GuildName = "";
        Year = 1; Month = 1;
        Gold = StartingFunds; Fame = 0;
        Members.Clear();
        Chronicle.Clear();
        Candidates.Clear();
        Armory = new Armory();
        Selected = null;
        Focused = null;
        Go(Screen.Opening);
    }

    // ── 상점 · 장비 (docs/07 §20.3 · §20.7) ──────────────────

    /// <summary>산다 — 자금이 모자라면 아무 일도 없다.</summary>
    public bool TryBuy(WeaponItem item)
    {
        int price = Shop.PriceOf(item);
        if (Gold < price) return false;

        Gold -= price;
        Armory.Add(item);
        Chronicle.Add($"{Year}년 {Month}월: {item.Korean} 구입 (-{price})");
        return true;
    }

    public bool TryBuy(ConsumableKind kind)
    {
        int price = Shop.PriceOf(kind);
        if (Gold < price) return false;

        Gold -= price;
        Armory.Add(kind);
        Chronicle.Add($"{Year}년 {Month}월: {kind.ToKorean()} 구입 (-{price})");
        return true;
    }

    /// <summary>창고의 장비를 그 칸에 끼운다. 끼고 있던 것은 창고로 돌아온다.</summary>
    public bool EquipFromArmory(MemberVm member, WeaponSet set, Hand hand, WeaponItem item)
    {
        if (!Armory.TryTake(item)) return false;

        var loadout = member.Adventurer.Loadout;
        ReturnToArmory(loadout, set, hand);
        // 양손 무기는 반대 손 칸을 비우므로, 그 칸의 물건도 먼저 창고로 돌린다.
        if (Weaponry.Of(item.Kind).Hands == Hands.Two)
            ReturnToArmory(loadout, set, hand == Hand.Right ? Hand.Left : Hand.Right);

        loadout.Equip(set, hand, item.Kind, item.Material);
        return true;
    }

    /// <summary>그 칸을 벗겨 창고로 보낸다.</summary>
    public void Unequip(MemberVm member, WeaponSet set, Hand hand)
    {
        var loadout = member.Adventurer.Loadout;
        ReturnToArmory(loadout, set, hand);
        loadout.Equip(set, hand, WeaponKind.None);
    }

    private void ReturnToArmory(Loadout loadout, WeaponSet set, Hand hand)
    {
        var kind = loadout[set, hand];
        if (kind == WeaponKind.None) return;
        Armory.Add(new WeaponItem(kind, loadout.MaterialOf(set, hand)));
    }

    // ── 모집 — 매년 1월 (콘솔 RecruitPhase와 같은 규칙) ──────────

    public bool RecruitOpen => Month == 1 && _recruitDoneForYear != Year;
    public int RecruitRoom => Math.Max(0, Capacity - Members.Count);
    public int RecruitAffordable => Math.Min(RecruitRoom, Math.Max(0, Gold / RecruitCost));
    public int RecruitPrice => RecruitCost;

    // 콘솔의 이름 풀과 같다 (Program.cs Names). 이름 표는 나중에 한 곳으로 모은다.
    private static readonly string[] FirstNames =
    [
        "아스카르", "미렌", "도르한", "셀비아", "카이엔", "루베르", "타냐", "그림", "이졸데",
        "베르난", "샤이엔", "오르한", "리케", "무단", "엘리아", "가웨인", "노라", "테오"
    ];

    private static readonly string[] Epithets =
    [
        "몰락 귀족", "떠돌이", "전직 병사", "고아", "밀렵꾼", "수도원 출신", "광부의 아들",
        "이방인", "빚쟁이", "탈영병"
    ];

    private string UniqueName(IRandomSource rng)
    {
        for (int tries = 0; tries < 40; tries++)
        {
            string name = $"{FirstNames[rng.NextInt(0, FirstNames.Length)]}({Epithets[rng.NextInt(0, Epithets.Length)]})";
            bool taken = Members.Any(m => m.Name == name) || Candidates.Any(c => c.Name == name);
            if (!taken) return name;
        }
        return $"{FirstNames[rng.NextInt(0, FirstNames.Length)]}({Epithets[rng.NextInt(0, Epithets.Length)]})";
    }

    /// <summary>모집을 연다 — 후보를 만들고 화면을 띄운다.</summary>
    public void OpenRecruit()
    {
        Candidates.Clear();
        var nameRng = _rng.Fork($"names:{Year}");
        for (int i = 0; i < CandidateCount; i++)
        {
            Candidates.Add(Adventurer.Recruit($"W{_nextId++}", UniqueName(nameRng), _rng.Fork($"recruit:{Year}:{i}")));
        }
        Go(Screen.Recruit);
    }

    /// <summary>영입 확정. 선택이 없어도 모집은 끝난 것으로 친다 (연 1회).</summary>
    public void Hire(IReadOnlyList<Adventurer> picked)
    {
        // 연타 방지 — 확정을 두 번 눌러도 같은 사람이 두 번 들어오지 않는다.
        if (Screen != Screen.Recruit) return;

        _recruitDoneForYear = Year;
        foreach (var candidate in picked)
        {
            Members.Add(new MemberVm { Adventurer = candidate });
            Gold -= RecruitCost;
            Chronicle.Add($"{Year}년: {candidate.Name} 영입");
        }
        Candidates.Clear();
        Go(Screen.Main);
    }

    /// <summary>단원 0명에 아무도 안 뽑음 — 길드는 문을 닫는다 (콘솔과 같은 결말).</summary>
    public void CloseGuild()
    {
        _recruitDoneForYear = Year;
        Candidates.Clear();
        Go(Screen.Title);
    }

    // ── 월 루프 ──────────────────────────────────────────────

    /// <summary>이 단원의 훈련 세션. 없으면 연다 — 콘솔과 같은 포크 라벨 방식이라 결정적이다.</summary>
    public TrainingYearSession SessionFor(MemberVm member)
    {
        if (member.Session is { } existing) return existing;

        var session = new TrainingYearSession(
            member.Adventurer, _rng.Fork($"train:{Year}:{Month}:{member.Adventurer.Id}"));
        member.Session = session;
        return session;
    }

    /// <summary>예상 성장 — 코어의 기대값 계산을 그대로 쓴다.</summary>
    public IReadOnlyDictionary<PrimaryStat, double> Preview(MemberVm member, TrainingActivity activity) =>
        SessionFor(member).PreviewMonth(activity).ToDictionary(p => p.Stat, p => p.Gain);

    /// <summary>한 달을 실행한다 — 배정된 훈련을 코어 세션으로 돌리고 결과를 남긴다.</summary>
    public void RunMonth()
    {
        // 연타 방지 — 버튼을 두 번 눌러도 한 달은 한 번만 굴러간다.
        // 이 가드가 없으면 같은 달 훈련이 중복 실행되어 첫해에 힘 60대가 나온다 (docs/08 #68).
        if (Screen != Screen.Prep) return;
        Go(Screen.MonthResult);

        foreach (var member in Members)
        {
            member.LastOutcome = null;

            if (member.Free && member.Assigned is { } activity)
            {
                var session = SessionFor(member);
                member.LastOutcome = session.AdvanceMonth(activity);

                if (session.IsComplete)
                {
                    session.Settle();
                    member.Session = null;
                }
            }
            else if (!member.Free)
            {
                member.DeployedMonthsLeft--;
            }
        }
    }

    /// <summary>월 결과 확인 — 달력을 넘기고 배정을 비운다. 새해 1월이면 모집이 열린다.</summary>
    public void ConfirmMonth()
    {
        // 연타 방지 — 확인을 두 번 눌러도 달력은 한 번만 넘어간다.
        if (Screen != Screen.MonthResult) return;

        foreach (var member in Members) member.Assigned = null;
        Selected = null;

        Month++;
        if (Month > 12) { Month = 1; Year++; }

        if (RecruitOpen) OpenRecruit();
        else Go(Screen.Main);
    }
}
