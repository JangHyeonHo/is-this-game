using Guildwright.Core.Weapons;

namespace Guildwright.Core.Careers;

/// <summary>
/// 직업 등급. <b>숙련도에서 파생되는 칭호</b>이지 별도의 능력 축이 아닙니다.
/// <para>
/// 견습 마법사와 대마법사가 <b>하는 일은 같습니다.</b> 다른 것은 <b>가치</b>입니다 —
/// 받는 급여, 맡길 수 있는 의뢰, 길드에 주는 평판.
/// </para>
/// <para>
/// 등급이 성장 속도나 스킬을 주지 않는 이유: 그러면 <b>높은 등급 → 빠른 성장 → 더 높은 등급</b>의
/// 눈덩이가 생겨 한 번 앞선 캐릭터가 영원히 앞섭니다. 실제 강함 차이는
/// 숙련도 자체(전투 효율 0.75~1.30배)와 능력치가 이미 담당합니다.
/// </para>
/// 근거: docs/04-game-design.md §3.6
/// </summary>
public enum JobRank
{
    /// <summary>견습. 숙련도 0~19.</summary>
    Apprentice,
    /// <summary>정식. 숙련도 20~44.</summary>
    Journeyman,
    /// <summary>상급. 숙련도 45~69.</summary>
    Adept,
    /// <summary>대(大). 숙련도 70~89.</summary>
    Master,
    /// <summary>전설. 숙련도 90~100.</summary>
    Grandmaster
}

public static class JobRanks
{
    /// <summary>숙련도로부터 등급을 구합니다.</summary>
    public static JobRank FromProficiency(int proficiency) => proficiency switch
    {
        >= 90 => JobRank.Grandmaster,
        >= 70 => JobRank.Master,
        >= 45 => JobRank.Adept,
        >= 20 => JobRank.Journeyman,
        _ => JobRank.Apprentice
    };

    /// <summary>다음 등급까지 필요한 숙련도. 최고 등급이면 null.</summary>
    public static int? NextThreshold(JobRank rank) => rank switch
    {
        JobRank.Apprentice => 20,
        JobRank.Journeyman => 45,
        JobRank.Adept => 70,
        JobRank.Master => 90,
        _ => null
    };

    /// <summary>
    /// 스타일과 등급을 합친 칭호.
    /// <para>모두 견습으로 시작해, 그 무기를 오래 든 만큼 이름이 달라집니다.</para>
    /// </summary>
    public static string TitleOf(WeaponStyle style, JobRank rank)
    {
        string[] ladder = style switch
        {
            WeaponStyle.SwordAndShield => ["견습 방패병", "방패병", "근위병", "기사", "수호기사"],
            WeaponStyle.DualWield => ["견습 검객", "검객", "쌍검사", "명검객", "검성"],
            WeaponStyle.TwoHanded => ["견습 전사", "전사", "역전의 전사", "맹장", "대전사"],
            WeaponStyle.Bow => ["견습 궁수", "궁수", "사수", "명궁", "신궁"],
            WeaponStyle.Crossbow => ["견습 석궁병", "석궁병", "저격수", "명사수", "관통자"],
            WeaponStyle.Staff => ["견습 마법사", "마법사", "상급 마법사", "대마법사", "현자"],
            WeaponStyle.Polearm => ["견습 창병", "창병", "창기병", "창술사", "창성"],
            _ => ["견습", "숙련자", "상급자", "달인", "전설"]
        };

        return ladder[(int)rank];
    }

    /// <summary>
    /// 연간 급여. <b>등급이 오르면 유지비도 오릅니다.</b>
    /// <para>
    /// 이게 있어야 "훈련만 시키기"가 최적해가 되지 않습니다.
    /// 잘 키운 모험가일수록 놀리는 비용이 비싸지므로 실전으로 밀려납니다.
    /// </para>
    /// <para>
    /// ⚠️ <b>급여는 항상 그 등급의 최대 수주 보수보다 낮아야 합니다.</b>
    /// 처음에 전설 등급을 1,300으로 잡았다가 최대 보수 1,200을 넘겨,
    /// 최고 등급이 순수 적자가 되는 문제가 있었습니다. 테스트로 고정해 두었습니다.
    /// </para>
    /// <para>
    /// 다만 등급이 오를수록 <b>수지는 빠듯해집니다</b> (견습 4.0배 → 전설 1.2배).
    /// 전설급 모험가의 값어치는 의뢰 보수가 아니라 <b>평판과 난이도 10 수주 자격</b>에서 나옵니다.
    /// </para>
    /// </summary>
    public static int AnnualWage(JobRank rank) => rank switch
    {
        JobRank.Apprentice => 60,
        JobRank.Journeyman => 150,
        JobRank.Adept => 340,
        JobRank.Master => 700,
        JobRank.Grandmaster => 1_000,
        _ => 60
    };

    /// <summary>
    /// 이 등급이 수주할 수 있는 의뢰 난이도 상한.
    /// <para>실력이 아니라 <b>자격</b>입니다. 길드가 신뢰를 얻어야 큰 일을 맡습니다.</para>
    /// </summary>
    public static int MaxContractDifficulty(JobRank rank) => rank switch
    {
        JobRank.Apprentice => 2,
        JobRank.Journeyman => 4,
        JobRank.Adept => 6,
        JobRank.Master => 8,
        JobRank.Grandmaster => 10,
        _ => 2
    };

    /// <summary>길드 평판에 기여하는 정도. 대마법사가 소속되어 있다는 사실 자체가 자산입니다.</summary>
    public static int ReputationValue(JobRank rank) => rank switch
    {
        JobRank.Apprentice => 0,
        JobRank.Journeyman => 2,
        JobRank.Adept => 6,
        JobRank.Master => 15,
        JobRank.Grandmaster => 35,
        _ => 0
    };

    public static string ToKorean(this JobRank rank) => rank switch
    {
        JobRank.Apprentice => "견습",
        JobRank.Journeyman => "정식",
        JobRank.Adept => "상급",
        JobRank.Master => "대가",
        JobRank.Grandmaster => "전설",
        _ => "?"
    };
}
