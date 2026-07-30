using Guildwright.Core.Adventurers;
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
    Main,
    Prep,
    MonthResult
}

/// <summary>
/// 단원 하나 — 코어 <see cref="Adventurer"/>를 감싸고, 화면 상태(배정·파견 잠금)만 얹는다.
/// 능력치·성장·예보는 전부 코어가 계산한다. 파견은 아직 코어와 연결 전이라
/// 남은 달수만 표시용으로 든다.
/// </summary>
public sealed class MemberVm
{
    public required Adventurer Adventurer { get; init; }

    /// <summary>진행 중인 훈련 세션. 12달을 채우면 결산하고 비운다.</summary>
    public TrainingYearSession? Session { get; set; }

    /// <summary>0이면 자유. 1 이상이면 파견 중(잠김)이고 남은 달수다. ⚠️ 표시용 자리표시.</summary>
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
/// 화면 전환과 월 루프의 겉면. 성장 계산은 전부 코어(<see cref="TrainingYearSession"/>)가 한다.
/// 파견·정산·소식은 아직 연결 전이다.
/// </summary>
public sealed class GameSession
{
    // ⚠️ 자리표시 시드 — 본 게임에서는 새 게임마다 시드를 뽑고 저장 파일에 든다.
    private readonly DeterministicRandom _rng = new("guildwright-web-prototype");

    public Screen Screen { get; private set; } = Screen.Title;
    public event Action? Changed;

    public string GuildName { get; set; } = "";
    public bool TutorialChosen { get; set; }

    public int Year { get; private set; } = 1274;
    public int Month { get; private set; } = 4;
    public int Gold { get; private set; } = 320;
    public int Fame { get; private set; } = 12;
    public int Capacity { get; } = 4;

    public List<MemberVm> Members { get; }
    public MemberVm? Selected { get; set; }

    /// <summary>훈련 카드 위에 포커스된 활동 — 오른쪽 전체 스탯에 예상 수치를 만든다.</summary>
    public TrainingActivity? Focused { get; set; }

    public GameSession()
    {
        Members =
        [
            new() { Adventurer = Adventurer.Recruit("web-1", "세라", _rng.Fork("recruit:1")) },
            new() { Adventurer = Adventurer.Recruit("web-2", "리안", _rng.Fork("recruit:2")) },
            new()
            {
                Adventurer = Adventurer.Recruit("web-3", "브렌", _rng.Fork("recruit:3")),
                DeployedMonthsLeft = 2, DeploymentName = "가도 호위"
            }
        ];
    }

    public string Season => Month switch { <= 3 => "봄", <= 6 => "여름", <= 9 => "가을", _ => "겨울" };
    public IEnumerable<MemberVm> FreeMembers => Members.Where(m => m.Free);
    public int AssignedCount => FreeMembers.Count(m => m.Assigned is not null);
    public int FreeCount => FreeMembers.Count();
    public bool AllAssigned => FreeCount > 0 && AssignedCount == FreeCount;

    public void Go(Screen screen)
    {
        Screen = screen;
        Raise();
    }

    public void Raise() => Changed?.Invoke();

    /// <summary>
    /// 이 단원의 훈련 세션. 없으면 연다 — 콘솔과 같은 포크 라벨 방식이라 결정적이다.
    /// </summary>
    public TrainingYearSession SessionFor(MemberVm member)
    {
        if (member.Session is { } existing) return existing;

        var session = new TrainingYearSession(
            member.Adventurer, _rng.Fork($"train:{Year}:{Month}:{member.Adventurer.Id}"));
        member.Session = session;
        return session;
    }

    /// <summary>
    /// 예상 성장 — 코어의 기대값 계산을 그대로 쓴다. 성공/실패에 따라 실제 값은
    /// 달라지지만, 얼마나 오르는 활동인지는 이 수치가 말해 준다.
    /// </summary>
    public IReadOnlyDictionary<PrimaryStat, double> Preview(MemberVm member, TrainingActivity activity) =>
        SessionFor(member).PreviewMonth(activity).ToDictionary(p => p.Stat, p => p.Gain);

    /// <summary>
    /// 한 달을 실행한다 — 배정된 훈련을 코어 세션으로 돌리고 결과를 남긴다.
    /// 파견 경과는 아직 표시용 감산만 한다.
    /// </summary>
    public void RunMonth()
    {
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

        Go(Screen.MonthResult);
    }

    /// <summary>월 결과 확인 — 달력을 넘기고 배정을 비운다.</summary>
    public void ConfirmMonth()
    {
        foreach (var member in Members) member.Assigned = null;
        Selected = null;

        Month++;
        if (Month > 12) { Month = 1; Year++; }

        Go(Screen.Main);
    }
}
