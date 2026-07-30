using Guildwright.Core.Parties;

namespace Guildwright.Core.Careers;

/// <summary>
/// 의뢰의 <b>형태</b> — 완료 판정으로 가릅니다.
/// <para>
/// <b>만들 것은 4개, 플레이어가 보는 종류는 얼마든지 늘어납니다.</b> 진행 규칙은 형태별로
/// 하나씩만 짜고, 종류는 목표물과 장소만 갈아끼웁니다. 예전 <c>ContractKind</c> 3종
/// (Combat/Gathering/Exploration)은 <b>소재 분류</b>라 진행 규칙과 대응하지 않아 폐기했습니다.
/// </para>
/// <para>
/// <b>"전투 없음"은 없습니다.</b> 마물이 언제든 올 수 있는 세계라 밭에 있든 성벽에 있든
/// 싸울 수 있습니다. 형태가 정하는 것은 전투가 <b>목적인가 · 방해인가 · 사고인가</b>입니다.
/// </para>
/// 근거: docs/08-design-revision.md §17.3
/// </summary>
public enum ContractForm
{
    /// <summary>토벌 — 나가서 찾아 싸운다. <b>전투가 목적.</b></summary>
    Subjugate,

    /// <summary>
    /// 지킴 — 자리를 지키며 기다린다. <b>전투가 방해</b>이고
    /// <b>아무 일 없이 끝나는 게 성공</b>입니다. 호위·전선 수비·농사 지원·보수공사.
    /// </summary>
    Defend,

    /// <summary>수집 — 캐고 모은다. 전투는 사고. 약초·광물 채집, 마물 소재.</summary>
    Gather,

    /// <summary>발견 — 뒤진다. <b>못 찾을 수도 있습니다.</b> 정찰·던전 발견·실종자 수색.</summary>
    Discover
}

/// <summary>의뢰의 출처. 보상의 성격이 여기서 갈립니다.</summary>
/// 근거: docs/08-design-revision.md §17.2
public enum ContractSource
{
    /// <summary>나라 · 영주. 주기적. 마물 토벌, 전선 수비, 마물령 침범.</summary>
    Realm,

    /// <summary>마을 · 의뢰주. 호위, 마물 정리, 채집, 농사 지원, 보수공사, 광산.</summary>
    Village,

    /// <summary>
    /// 길드 자체. 정찰·탐색, 던전 발견, 보물찾기.
    /// <b>길드가 자기 돈 들여 하는 투자</b>이므로 보수가 아니라 명성을 얻습니다.
    /// </summary>
    Guild
}

/// <summary>보상의 성격.</summary>
public enum RewardKind
{
    /// <summary>보수 — 돈.</summary>
    Pay,

    /// <summary>명성 — 길드가 자기 돈을 들인 대가.</summary>
    Renown
}

/// <summary>
/// 길드가 받는 의뢰 한 건.
/// <para>
/// <b>기간이 고정이고 성공/실패가 이분법입니다.</b> 목표를 빨리 채워도 정해진 달만큼
/// 작업하고, 부분 성공도 초과 성과도 없습니다. 그래서 판단이 옮겨갑니다 —
/// <b>"채울 수 있나"가 아니라 "버틸 수 있나".</b>
/// </para>
/// <para>
/// <see cref="Intensity"/>는 관리해야 할 할당량이 아니라 <b>그 의뢰의 강도 표시</b>입니다.
/// "고블린 10마리"는 그 기간에 그 정도 싸움이 있다는 뜻이고, 달성은 전제입니다.
/// </para>
/// 근거: docs/08-design-revision.md §17.3, §17.4
/// </summary>
/// <param name="Id">식별자. 게시판에서 같은 의뢰를 두 번 세지 않기 위해 필요합니다.</param>
/// <param name="Name">표시용 이름.</param>
/// <param name="Form">형태. 진행과 완료 판정이 여기서 갈립니다.</param>
/// <param name="Source">출처. 보상의 성격을 정합니다.</param>
/// <param name="Difficulty">난이도. 적의 강도와 보수를 좌우합니다.</param>
/// <param name="Months">기간(달). <b>고정입니다</b> — 조기 종료가 없습니다.</param>
/// <param name="Intensity">그 기간의 싸움 강도 표시. 목표 수량으로 보여줍니다.</param>
/// <param name="PartyOnly">정규 파티만 받을 수 있는가 (§6.3).</param>
/// <param name="Persists">
/// 그 달에 수락하지 않아도 남는가. <b>승급 의뢰와 전개상 필수 의뢰</b>가 여기입니다 (§6.5).
/// </param>
/// <param name="RequiredRank">수주 자격 등급.</param>
/// <param name="Objective">지켜야 할 것 · 찾아야 할 것의 이름. 표시용.</param>
/// <param name="PromotionTo">
/// 승급 의뢰라면 <b>올라가는 등급</b>. 아니면 <c>null</c>.
/// <para>
/// 이것이 승급의 유일한 경로입니다 (§6.5) — 이 값이 있고 성공했을 때만 등급이 오릅니다.
/// Id 문자열을 파싱하지 않고 값으로 두는 이유는, 배선이 빠졌는지를 타입으로 볼 수 있게
/// 하기 위함입니다. 실제로 <c>Promotion()</c>이 만들어지기만 하고 아무 곳에서도
/// 쓰이지 않아 등급이 영원히 오르지 않던 기간이 있었습니다.
/// </para>
/// </param>
public sealed record Contract(
    string Id,
    string Name,
    ContractForm Form,
    ContractSource Source,
    int Difficulty,
    int Months,
    int Intensity,
    bool PartyOnly = false,
    bool Persists = false,
    Rank RequiredRank = Rank.F,
    string? Objective = null,
    Rank? PromotionTo = null)
{
    /// <summary>승급 의뢰인가.</summary>
    public bool IsPromotion => PromotionTo is not null;

    /// <summary>보상의 성격. 길드가 자기 돈으로 하는 일은 명성으로 돌아옵니다.</summary>
    public RewardKind Reward => Source == ContractSource.Guild ? RewardKind.Renown : RewardKind.Pay;

    /// <summary>지켜야 할 대상이 있는가. 실패 사유 하나가 여기서만 생깁니다.</summary>
    public bool HasWard => Form == ContractForm.Defend;

    /// <summary>못 찾고 끝날 수 있는가.</summary>
    public bool CanComeUpEmpty => Form == ContractForm.Discover;

    /// <summary>그 사람이 이 의뢰를 받을 자격이 있는가. <b>파티 전용은 여기서 걸리지 않습니다.</b></summary>
    public bool IsOpenTo(Rank rank) => rank >= RequiredRank;

    /// <summary>
    /// 전투 비중. 사고 위험과 보수 산정에 곱합니다.
    /// <para>
    /// <b>전투가 목적인가 · 방해인가 · 사고인가</b>가 그대로 값이 됩니다. 수집 의뢰는
    /// 전투 비중이 낮으므로 <b>전투력이 낮은 캐릭터도 제 몫을 할 자리</b>가 됩니다.
    /// </para>
    /// <para>⚠️ 임시값. 토벌 1.0 · 수집 0.25는 예전 <c>ContractKind</c>에서 그대로 옮겼습니다.</para>
    /// </summary>
    public double CombatWeight => Form switch
    {
        ContractForm.Subjugate => 1.0,
        ContractForm.Defend => 0.7,
        ContractForm.Discover => 0.6,
        _ => 0.25
    };

    public override string ToString() =>
        $"[{Name}] {Form.ToKorean()} · 난이도 {Difficulty} · {Months}달 · 강도 {Intensity}" +
        (PartyOnly ? " · 파티 전용" : "") +
        (Persists ? " · 지속" : "");
}

/// <summary>형태·출처의 한국어 이름.</summary>
public static class ContractNames
{
    public static string ToKorean(this ContractForm form) => form switch
    {
        ContractForm.Subjugate => "토벌",
        ContractForm.Defend => "지킴",
        ContractForm.Gather => "수집",
        ContractForm.Discover => "발견",
        _ => form.ToString()
    };

    public static string ToKorean(this ContractSource source) => source switch
    {
        ContractSource.Realm => "나라·영주",
        ContractSource.Village => "마을",
        ContractSource.Guild => "길드",
        _ => source.ToString()
    };

    /// <summary>그 형태에서 목표 수량을 무엇으로 부르는가.</summary>
    public static string IntensityLabel(this ContractForm form) => form switch
    {
        ContractForm.Subjugate => "마리",
        ContractForm.Defend => "차례의 습격",
        ContractForm.Gather => "개",
        ContractForm.Discover => "곳",
        _ => "건"
    };
}
