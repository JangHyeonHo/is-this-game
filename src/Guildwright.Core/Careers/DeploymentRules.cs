using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Careers;

/// <summary>
/// 파견 진행의 수치.
/// <para>
/// ⚠️ <b>여기 있는 수치는 전부 임시값입니다.</b> 배치 시뮬레이션으로 검증하고 근거를
/// docs/06-balance-log.md에 남기세요. 감으로 고치지 마세요.
/// </para>
/// <para>
/// 예외적으로 <see cref="RestHealRatio"/>는 주인님이 준 범위(휴식 달에 50~100%)
/// 안에 있으므로 임시값이라기보다 그 범위 안의 한 점입니다.
/// </para>
/// 근거: docs/08-design-revision.md §17.4, §17.5, §17.5b
/// </summary>
public static class DeploymentRules
{
    /// <summary>
    /// 형태별 한 달 조우 확률.
    /// <para>
    /// <b>토벌은 만나는 것이 전제입니다</b> — 나가서 찾아 싸우는 일이므로 "못 찾았다"가
    /// 성립하지 않습니다. 안 만날 수 있는 것은 <b>지킴</b>입니다(습격이 올 수도, 안 올 수도).
    /// 수집·발견에서는 사고로만 만납니다.
    /// </para>
    /// <para>
    /// ⚠️ 예전에는 토벌을 0.85로 두었습니다. 그러면 조우가 적게 뜬 달이 겹쳐
    /// <b>끝까지 일했는데도 진척 미달로 실패</b>하는 파견이 나옵니다(40번 중 2번 측정).
    /// 그 근거로 삼았던 문서 문장("토벌은 안 만나면 진척이 없다")은 <b>에이전트가 쓴 것</b>이고
    /// 주인님의 모델과 반대였습니다.
    /// </para>
    /// </summary>
    public static double EncounterChanceOf(ContractForm form) => form switch
    {
        ContractForm.Subjugate => 1.00,
        ContractForm.Defend => 0.55,
        ContractForm.Gather => 0.30,
        ContractForm.Discover => 0.35,
        _ => 0.50
    };

    /// <summary>일한 달에 저절로 회복되는 HP 비율. 자연회복입니다 (§17.5b).</summary>
    public const double NaturalHealRatio = 0.08;

    /// <summary>일한 달에 저절로 회복되는 마나 비율. HP보다 빠릅니다.</summary>
    public const double NaturalManaRatio = 0.15;

    /// <summary>
    /// 쉰 달의 HP 회복 비율.
    /// <para>
    /// 주인님: "전투중의 휴식턴에는 거의 뭐 50퍼 이상 100퍼까지도 채울 수도".
    /// 후해도 공짜가 아닌 이유는 <b>쉰 달만큼 진척이 없다</b>는 것입니다 (§17.5b).
    /// </para>
    /// </summary>
    public const double RestHealRatio = 0.60;

    /// <summary>쉰 달의 마나 회복 비율.</summary>
    public const double RestManaRatio = 0.80;

    /// <summary>
    /// 이 아래로 떨어지면 모험가들이 쉬기로 판단합니다.
    /// <para><b>모험가는 생존을 최우선으로 행동합니다</b> (§17.5) — 플레이어가 고르는 게 아닙니다.</para>
    /// </summary>
    public const double RestBelowHpRatio = 0.55;

    /// <summary>한 사람이 기본으로 지니는 회복약. 가방 없이도 이만큼은 듭니다.</summary>
    public const int PersonalCarry = 2;

    /// <summary>발견 판정의 기본 확률 (한 달 작업당). 판단력이 여기에 더해집니다.</summary>
    public const double DiscoveryChancePerMonth = 0.28;

    /// <summary>판단력 100당 발견 확률 보정.</summary>
    public const double DiscoveryPerJudgement = 0.20;

    /// <summary>파견이 성립하는 최대 난이도 — 개인 수주 자격에서 나옵니다.</summary>
    public static int MaxDifficultyFor(IReadOnlyList<Adventurer> party) =>
        party.Count == 0 ? 0 : party.Max(a => a.MaxContractDifficulty);
}

/// <summary>
/// 파견에 들려 보내는 보급.
/// <para>
/// <b>그 파티가 들 수 있는 짐 한도 내에서만</b> 지원할 수 있습니다 (§17.6). 포션을 더
/// 들려 보내고 싶어도 들 사람이 없으면 못 보냅니다 — 짐꾼의 가방이 여기서 값을 갖습니다.
/// </para>
/// </summary>
public sealed record Supplies(int Potions)
{
    /// <summary>
    /// 그 파티가 들 수 있는 총량. <b>1인당 기본 + 가방 적재량</b>입니다.
    /// <para>
    /// 기본 지참량이 0이면 짐꾼이 사실상 필수가 되어 "짐꾼 최대 1명"과 솔로잉이 무의미해집니다.
    /// </para>
    /// </summary>
    public static int CapacityOf(IReadOnlyList<Adventurer> party) =>
        party.Count * DeploymentRules.PersonalCarry + party.Sum(a => a.Loadout.Load);

    /// <summary>한도까지 잘라서 보급을 만듭니다.</summary>
    public static Supplies UpTo(IReadOnlyList<Adventurer> party, int potions) =>
        new(Math.Clamp(potions, 0, CapacityOf(party)));

    /// <summary>기본 보급 — 1인당 기본 지참량만.</summary>
    public static Supplies Default(IReadOnlyList<Adventurer> party) =>
        new(party.Count * DeploymentRules.PersonalCarry);

    /// <summary>한도를 넘는가.</summary>
    public bool ExceedsCapacityOf(IReadOnlyList<Adventurer> party) => Potions > CapacityOf(party);

    /// <summary>
    /// 파티원에게 나눠 담습니다. <b>가방을 든 사람이 더 많이 듭니다.</b>
    /// <para>순서를 서수로 고정합니다 — 나눠 담는 순서가 결과를 바꾸면 재현이 깨집니다.</para>
    /// </summary>
    public IReadOnlyDictionary<string, int> DistributeAmong(IReadOnlyList<Adventurer> party)
    {
        var order = party.OrderByDescending(a => a.Loadout.Load)
                         .ThenBy(a => a.Id, StringComparer.Ordinal)
                         .ToArray();

        var share = order.ToDictionary(a => a.Id, _ => 0, StringComparer.Ordinal);
        int left = Potions;

        // 각자의 한도까지 채우고, 남으면 다시 돌립니다.
        while (left > 0)
        {
            bool placed = false;
            foreach (var a in order)
            {
                int limit = DeploymentRules.PersonalCarry + a.Loadout.Load;
                if (share[a.Id] >= limit) continue;

                share[a.Id]++;
                left--;
                placed = true;
                if (left == 0) break;
            }

            if (!placed) break;   // 전원이 한도를 채웠습니다.
        }

        return share;
    }
}
