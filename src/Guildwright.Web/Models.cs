using Guildwright.Core.Adventurers;
using Guildwright.Core.Training;

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
/// 화면 뼈대용 단원 표시 모델.
/// <para>
/// ⚠️ 코어 연결 전의 자리표시다. 코어 연결 단계에서 <c>Adventurer</c>로 교체되며,
/// 여기 있는 수치는 어떤 규칙의 근거도 아니다.
/// </para>
/// </summary>
public sealed class MemberVm
{
    public required string Name { get; init; }
    public required string Job { get; init; }
    public required int Age { get; init; }
    public required Dictionary<PrimaryStat, int> Stats { get; init; }
    public int Fatigue { get; set; }
    public string Condition { get; set; } = "보통";

    /// <summary>0이면 자유. 1 이상이면 파견 중(잠김)이고 남은 달수다.</summary>
    public int DeployedMonthsLeft { get; set; }
    public string? DeploymentName { get; set; }

    /// <summary>이번 달 배정된 훈련. null이면 미배정.</summary>
    public TrainingActivity? Assigned { get; set; }

    public List<string> Proficiencies { get; init; } = [];
    public List<string> KnownPassives { get; init; } = [];
    public int HiddenPassiveSlots { get; init; }

    public bool Free => DeployedMonthsLeft <= 0;
}

/// <summary>
/// 화면 전환과 표시 상태. 게임 규칙은 들어 있지 않다 — 그건 코어의 일이고,
/// 이 클래스는 코어 연결 단계에서 코어 세션의 겉면이 된다.
/// </summary>
public sealed class GameSession
{
    public Screen Screen { get; private set; } = Screen.Title;
    public event Action? Changed;

    public string GuildName { get; set; } = "";
    public bool TutorialChosen { get; set; }

    public int Year { get; private set; } = 1274;
    public int Month { get; private set; } = 4;
    public int Gold { get; private set; } = 320;
    public int Fame { get; private set; } = 12;
    public int Capacity { get; } = 4;

    public List<MemberVm> Members { get; } = SampleMembers();
    public MemberVm? Selected { get; set; }

    /// <summary>훈련 카드 위에 포커스된 활동 — 오른쪽 전체 스탯에 ▲ 표시를 만든다.</summary>
    public TrainingActivity? Focused { get; set; }

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
    /// 한 달을 넘긴다 — 표시용 최소 동작만. 성장·정산·전투는 코어 연결 단계에서
    /// 코어가 계산한 결과로 바뀐다.
    /// </summary>
    public void AdvanceMonth()
    {
        foreach (var member in Members)
        {
            if (member.Assigned is { } activity)
                member.Fatigue = Math.Max(0, member.Fatigue + TrainingActivities.Of(activity).FatigueCost);
            if (member.DeployedMonthsLeft > 0)
                member.DeployedMonthsLeft--;
        }

        Month++;
        if (Month > 12) { Month = 1; Year++; }
    }

    public void ClearAssignments()
    {
        foreach (var member in Members) member.Assigned = null;
    }

    private static List<MemberVm> SampleMembers() =>
    [
        new()
        {
            Name = "세라", Job = "견습 F", Age = 15,
            Stats = new()
            {
                [PrimaryStat.Strength] = 22, [PrimaryStat.Agility] = 31, [PrimaryStat.Finesse] = 24,
                [PrimaryStat.Vitality] = 26, [PrimaryStat.Intellect] = 18, [PrimaryStat.Spirit] = 20
            },
            Fatigue = 8, Condition = "좋음",
            Proficiencies = ["한손검 6", "방패 2"],
            KnownPassives = ["신중"], HiddenPassiveSlots = 2
        },
        new()
        {
            Name = "리안", Job = "전사 F", Age = 17,
            Stats = new()
            {
                [PrimaryStat.Strength] = 38, [PrimaryStat.Agility] = 29, [PrimaryStat.Finesse] = 25,
                [PrimaryStat.Vitality] = 41, [PrimaryStat.Intellect] = 15, [PrimaryStat.Spirit] = 17
            },
            Fatigue = 21, Condition = "좋음",
            Proficiencies = ["한손검 14", "방패 9"],
            KnownPassives = ["고집"], HiddenPassiveSlots = 1
        },
        new()
        {
            Name = "브렌", Job = "전사 F", Age = 16,
            Stats = new()
            {
                [PrimaryStat.Strength] = 33, [PrimaryStat.Agility] = 27, [PrimaryStat.Finesse] = 22,
                [PrimaryStat.Vitality] = 35, [PrimaryStat.Intellect] = 14, [PrimaryStat.Spirit] = 16
            },
            Fatigue = 30, Condition = "보통",
            DeployedMonthsLeft = 2, DeploymentName = "가도 호위",
            Proficiencies = ["한손검 11", "방패 7"],
            KnownPassives = ["막무가내"], HiddenPassiveSlots = 1
        }
    ];
}
