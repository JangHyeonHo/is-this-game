using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;

using Guildwright.Core.Skills;

namespace Guildwright.Core.Careers;

/// <summary>파견이 실패한 이유. <see cref="None"/>이면 성공입니다.</summary>
public enum DeploymentFailure
{
    None,

    /// <summary>아무도 서 있지 못했습니다.</summary>
    Wiped,

    /// <summary>견적이 안 나와 물러났습니다 (§17.5).</summary>
    Retreated,

    /// <summary>플레이어가 손절했습니다 (§17.7). <b>실패는 실패입니다.</b></summary>
    Abandoned,

    /// <summary>지켜야 할 것을 잃었습니다. 지킴형에만 있습니다.</summary>
    ObjectiveLost,

    /// <summary>못 찾았습니다. 발견형에만 있습니다.</summary>
    NotFound,

    /// <summary>
    /// 기간을 채웠지만 일을 다 못 했습니다.
    /// <para>
    /// 쉬는 달이 많았다는 뜻입니다. <b>휴식 회복이 후해도 공짜가 아닌 이유</b>가 여기입니다 (§17.5b).
    /// </para>
    /// </summary>
    Unfinished
}

/// <summary>파견 실패 사유의 화면 표기.</summary>
public static class DeploymentFailures
{
    public static string ToKorean(this DeploymentFailure failure) => failure switch
    {
        DeploymentFailure.None => "성공",
        DeploymentFailure.Wiped => "전원 전투 불능",
        DeploymentFailure.Retreated => "후퇴",
        DeploymentFailure.Abandoned => "중도 포기",
        DeploymentFailure.ObjectiveLost => "대상 상실",
        DeploymentFailure.NotFound => "발견 실패",
        DeploymentFailure.Unfinished => "기간 내 미달",
        _ => failure.ToString()
    };
}

/// <summary>그 달에 무엇을 했는가. <b>모험가 AI가 고릅니다</b> — 플레이어는 편성과 보급만 합니다.</summary>
public enum MonthWork
{
    /// <summary>일했습니다.</summary>
    Work,

    /// <summary>쉬었습니다. 회복하지만 진척이 없습니다.</summary>
    Rest
}

/// <summary>한 달의 기록.</summary>
/// <param name="Month">파견 1달째부터.</param>
/// <param name="Work">일했는지 쉬었는지.</param>
/// <param name="Note">표시용 설명.</param>
/// <param name="Progress">그 달에 오른 진척.</param>
/// <param name="Fought">전투가 있었는지.</param>
public sealed record DeploymentMonth(int Month, MonthWork Work, string Note, int Progress, bool Fought);

/// <summary>파견 한 건의 결과. <b>성공 아니면 실패입니다</b> — 부분 성공도 초과 성과도 없습니다.</summary>
/// <param name="Contract">수행한 의뢰.</param>
/// <param name="Failure">실패 사유. <see cref="DeploymentFailure.None"/>이면 성공.</param>
/// <param name="Progress">진척. 표시용이며 성패는 <paramref name="Failure"/>로만 가릅니다.</param>
/// <param name="MonthsSpent">실제로 보낸 달. 중도 이탈이면 기간보다 짧습니다.</param>
/// <param name="Months">달마다의 기록.</param>
public sealed record DeploymentResult(
    Contract Contract,
    DeploymentFailure Failure,
    int Progress,
    int MonthsSpent,
    IReadOnlyList<DeploymentMonth> Months)
{
    public bool Succeeded => Failure == DeploymentFailure.None;

    /// <summary>보수를 받는가. 실패는 전부 0입니다.</summary>
    public bool Paid => Succeeded;

    public override string ToString() =>
        $"{Contract.Name} — {(Succeeded ? "성공" : $"실패 ({Failure})")} " +
        $"· {MonthsSpent}/{Contract.Months}달 · 진척 {Progress}/{Contract.Intensity}";
}

/// <summary>
/// 파견 한 건을 <b>달 단위</b>로 진행합니다. 예전 <c>FieldYearSession</c>을 대체합니다.
///
/// <para>
/// 예전에는 <b>12개월 = 의뢰 1건</b>이었습니다. 그러면 1달 의뢰도 1년 의뢰도 없고,
/// 한 사람이 한 해에 한 건만 하게 되며, 달력 잠금이라는 기회비용이 성립하지 않습니다.
/// 이제 <b>기간은 의뢰가 정합니다</b> — 1달, 2~3달, 1년짜리도 있습니다.
/// </para>
///
/// <para>
/// 역할이 나뉘어 있습니다 (§17.5). <b>플레이어는 편성과 보급</b>만 하고,
/// <b>일할지 쉴지는 모험가가 판단</b>합니다 — 생존이 최우선이기 때문입니다.
/// 플레이어가 끼어드는 곳은 주인공이 동행한 <b>전투 중</b>뿐입니다.
/// </para>
///
/// <para>
/// <b>HP·마나·회복약이 파견 내내 이어집니다</b> (§17.5b). 예전에는 마나만 매 전투
/// 리셋되어 사실상 무한이었습니다. 자연회복이 있고, 쉬는 달에는 크게 회복합니다.
/// </para>
///
/// <para>
/// 부작용이 없습니다 — 시간·파일에 손대지 않고 난수는 주입받습니다.
/// </para>
/// 근거: docs/07-decisions.md §17.3~§17.7
/// </summary>
public sealed class DeploymentSession
{
    private readonly IReadOnlyList<Adventurer> _party;
    private readonly IRandomSource _rng;
    private readonly Func<IRandomSource, string> _nameFor;
    private readonly List<DeploymentMonth> _months = [];

    /// <summary>파견 내내 이어지는 HP. 저절로는 조금씩만 찹니다.</summary>
    private readonly Dictionary<string, int> _hp;

    /// <summary>파견 내내 이어지는 마나.</summary>
    private readonly Dictionary<string, int> _mana;

    /// <summary>남은 회복약. 보급 한도 안에서 나눠 담은 것입니다.</summary>
    private readonly Dictionary<string, int> _potions;

    private readonly Dictionary<string, int> _maxMana;
    private readonly Dictionary<string, CombatContribution> _contributions = [];

    private DeploymentFailure _failure = DeploymentFailure.None;
    private bool _found;

    public DeploymentSession(
        IReadOnlyList<Adventurer> party,
        Contract contract,
        IRandomSource rng,
        Supplies? supplies = null,
        Func<IRandomSource, string>? nameFor = null)
    {
        if (party.Count == 0) throw new ArgumentException("파티가 비어 있습니다.", nameof(party));

        var given = supplies ?? Supplies.Default(party);
        if (given.ExceedsCapacityOf(party))
        {
            throw new ArgumentException(
                $"짐 한도를 넘습니다 (한도 {Supplies.CapacityOf(party)}, 요청 {given.Potions}). " +
                "가방을 든 사람이 있어야 더 보낼 수 있습니다.", nameof(supplies));
        }

        // 서수 정렬로 고정합니다 — 순회 순서가 전투 결과에 섞이면 재현이 깨집니다.
        _party = [.. party.OrderBy(a => a.Id, StringComparer.Ordinal)];
        Contract = contract;
        Supplies = given;
        _rng = rng;

        _hp = _party.ToDictionary(a => a.Id, a => a.MaxHp, StringComparer.Ordinal);
        _maxMana = _party.ToDictionary(a => a.Id, a => DerivedStats.MaxMana(a.Stats, a.Bonuses), StringComparer.Ordinal);
        _mana = _party.ToDictionary(a => a.Id, a => _maxMana[a.Id], StringComparer.Ordinal);
        _potions = new Dictionary<string, int>(given.DistributeAmong(_party), StringComparer.Ordinal);
        _nameFor = nameFor ?? (_ => "마물");
    }

    public Contract Contract { get; }
    public Supplies Supplies { get; }

    public IReadOnlyList<Adventurer> Party => _party;

    /// <summary>진척. <b>표시용입니다</b> — 성패는 버텼는지로 가립니다.</summary>
    public int Progress { get; private set; }

    /// <summary>다음에 진행할 달 (1부터).</summary>
    public int CurrentMonth => _months.Count + 1;

    public IReadOnlyList<DeploymentMonth> Months => _months;

    /// <summary>기간을 다 썼거나 중간에 끝났으면 끝입니다. <b>조기 종료는 없습니다.</b></summary>
    public bool IsComplete => _failure != DeploymentFailure.None || _months.Count >= Contract.Months;

    public IReadOnlyDictionary<string, int> Hp => _hp;
    public IReadOnlyDictionary<string, int> Mana => _mana;
    public IReadOnlyDictionary<string, int> Potions => _potions;

    /// <summary>서 있는 사람.</summary>
    /// <summary>
    /// 전투 전력으로 셈하는 인원. 짐꾼과 가방을 든 사람은 싸우지 못하므로 (§16.8b)
    /// 적 머릿수 계산에 넣지 않습니다 — 넣으면 비전투 요원이 적만 늘립니다.
    /// </summary>
    public static int Combatants(IEnumerable<Adventurer> party) =>
        Math.Max(1, party.Count(a => Jobs.Of(a.Job).Combat && !a.Loadout.CarryingPack));

    public IReadOnlyList<Adventurer> Standing =>
        [.. _party.Where(a => a.IsAlive && _hp[a.Id] > 0)];

    /// <summary>
    /// <b>서 있는 사람</b>의 평균 HP 비율. 쉴지 말지의 판단 기준입니다.
    /// <para>
    /// ⚠️ 예전에는 쓰러진 사람을 분모에 그대로 뒀습니다. 파견 중에는 쓰러진 사람이
    /// 일어나지 않으므로, 2인 파티에서 한 명이 쓰러지면 비율이 <b>영구히 0.5</b>가 되어
    /// 문턱(0.55) 아래에 고정되고, <b>만피인 동료까지 남은 모든 달을 쉬게</b> 됩니다.
    /// 지킴형은 그 순간 실패 경로가 사라져 성공이 확정되고 수집형은 미달이 확정됐습니다.
    /// </para>
    /// </summary>
    public double HealthRatio
    {
        get
        {
            var standing = Standing;
            if (standing.Count == 0) return 0.0;

            // 순서를 고정합니다 — Standing은 이미 서수 정렬된 _party에서 나옵니다.
            return standing.Sum(a => (double)_hp[a.Id] / a.MaxHp) / standing.Count;
        }
    }

    /// <summary>
    /// 한 달을 진행합니다.
    /// </summary>
    /// <param name="battleRng">전투용 난수원. 생략하면 파견 난수원에서 갈라 씁니다.</param>
    /// <param name="commander">
    /// 주인공이 동행할 때의 개입 통로. <b>없으면 백그라운드로 처리됩니다</b> (§17.5).
    /// </param>
    /// <param name="onLine">전투 기록 실시간 출력.</param>
    public DeploymentMonth AdvanceMonth(
        IRandomSource? battleRng = null,
        IBattleCommander? commander = null,
        Action<string>? onLine = null)
    {
        if (IsComplete) throw new InvalidOperationException("이미 끝난 파견입니다.");

        int month = CurrentMonth;

        // 아무도 서 있지 못하면 그 자리에서 끝입니다.
        if (Standing.Count == 0)
        {
            _failure = DeploymentFailure.Wiped;
            return Record(new DeploymentMonth(
                month, MonthWork.Rest, $"{month}달째: 아무도 일어서지 못했다", 0, Fought: false));
        }

        // 일할지 쉴지는 모험가가 판단합니다. 생존이 최우선입니다.
        if (HealthRatio < DeploymentRules.RestBelowHpRatio)
        {
            Recover(DeploymentRules.RestHealRatio, DeploymentRules.RestManaRatio);
            return Record(new DeploymentMonth(
                month, MonthWork.Rest, $"{month}달째: 상처를 돌보며 쉬었다 (진척 없음)", 0, Fought: false));
        }

        bool fought = false;
        int gained = 0;
        string note;

        if (_rng.Chance(DeploymentRules.EncounterChanceOf(Contract.Form)))
        {
            // 싸울 수 있는 사람이 없으면 싸움이 성립하지 않습니다 — 짐꾼 혼자 50라운드를
            // 버티는 결투는 게임이 아니라 고문입니다.
            if (Standing.Count(a => Skills.Jobs.Of(a.Job).Combat && !a.Loadout.CarryingPack) == 0)
            {
                _failure = Contract.HasWard ? DeploymentFailure.ObjectiveLost : DeploymentFailure.Retreated;
                return Record(new DeploymentMonth(month, MonthWork.Work,
                    $"{month}달째: 싸울 사람이 없어 물러났다", 0, Fought: false));
            }

            fought = true;
            var (won, killed, foes) = Fight(month, battleRng ?? _rng.Fork($"battle:{month}"), commander, onLine);

            if (!won)
            {
                // 형태에 따라 패배의 뜻이 다릅니다.
                _failure = Standing.Count == 0
                    ? DeploymentFailure.Wiped
                    : Contract.HasWard ? DeploymentFailure.ObjectiveLost : DeploymentFailure.Retreated;

                note = Contract.HasWard
                    ? $"{month}달째: 습격을 막지 못했다 — {Contract.Objective ?? "지킬 것"}을 잃었다"
                    : $"{month}달째: 견적이 안 나와 물러났다";

                return Record(new DeploymentMonth(month, MonthWork.Work, note, 0, Fought: true));
            }

            gained = ProgressFromBattle(killed);
            // 상대가 누구였는지 없이는 승패에서 아무것도 배울 수 없습니다.
            note = $"{month}달째: {foes}와 교전 — {killed}마리 처치";
        }
        else
        {
            gained = ProgressFromQuietMonth();
            note = Contract.Form switch
            {
                ContractForm.Subjugate => $"{month}달째: 흔적만 쫓았다",
                ContractForm.Defend => $"{month}달째: 아무 일 없었다",
                ContractForm.Gather => $"{month}달째: 순조롭게 모았다",
                _ => $"{month}달째: 뒤졌지만 조용했다"
            };
        }

        // 발견형은 뒤지는 것 자체가 판정입니다 — 못 찾고 끝날 수 있습니다.
        if (Contract.CanComeUpEmpty && !_found && _rng.Chance(DiscoveryChance()))
        {
            _found = true;
            note += $" · {Contract.Objective ?? "목표"}을(를) 찾았다";
        }

        Progress += gained;
        Recover(DeploymentRules.NaturalHealRatio, DeploymentRules.NaturalManaRatio);

        return Record(new DeploymentMonth(month, MonthWork.Work, note, gained, fought));
    }

    /// <summary>
    /// 손절합니다 (§17.7). <b>실패는 실패</b>지만, 끝까지 밀어서 무너지는 것보다 나을 수 있습니다.
    /// </summary>
    public void Abandon()
    {
        if (_failure == DeploymentFailure.None && !IsComplete) _failure = DeploymentFailure.Abandoned;
    }

    /// <summary>결과를 확정합니다. 기간을 다 채웠어도 일을 못 했으면 실패입니다.</summary>
    public DeploymentResult Complete()
    {
        var failure = _failure;

        if (failure == DeploymentFailure.None)
        {
            if (Contract.CanComeUpEmpty && !_found) failure = DeploymentFailure.NotFound;

            // 지킴형은 진척을 따지지 않습니다 — 습격이 아예 안 왔으면 진척이 0인데
            // 그게 성공입니다. "아무 일 없이 끝나는 게 성공"이 이 예외의 근거입니다 (§17.3).
            else if (!Contract.HasWard && Progress < Contract.Intensity) failure = DeploymentFailure.Unfinished;
        }

        return new DeploymentResult(Contract, failure, Progress, _months.Count, _months);
    }

    /// <summary>그 파견에서 각자가 무엇을 겪었는지. 성장 계산에 넘깁니다.</summary>
    public IReadOnlyDictionary<string, CombatExperience> Experience =>
        _contributions
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => CombatExperience.From(kv.Value), StringComparer.Ordinal);

    // ── 내부 ────────────────────────────────────────────────

    private DeploymentMonth Record(DeploymentMonth month)
    {
        _months.Add(month);
        return month;
    }

    /// <summary>
    /// 형태별 진척.
    /// <para>
    /// 토벌은 처치 수, 지킴은 막아낸 습격 수, 수집은 일한 달 수, 발견은 뒤진 달 수입니다.
    /// <b>강도(Intensity)와 같은 단위로 맞춰야</b> 진척 표시가 뜻을 가집니다.
    /// </para>
    /// </summary>
    private int ProgressFromBattle(int killed) => Contract.Form switch
    {
        ContractForm.Subjugate => killed,
        ContractForm.Defend => 1,          // 습격 한 차례를 막아냈습니다.
        _ => WorkShare()                   // 수집·발견은 싸움이 진척이 아닙니다.
    };

    private int ProgressFromQuietMonth() => Contract.Form switch
    {
        ContractForm.Subjugate => 0,       // 안 만나면 진척이 없습니다.
        ContractForm.Defend => 0,          // 습격이 없었으면 막아낼 것도 없었습니다.
        _ => WorkShare()
    };

    /// <summary>
    /// 수집·발견에서 일한 달이 올리는 진척.
    /// <para>
    /// 기간을 <b>전부</b> 일하면 정확히 강도에 닿고, <b>한 달이라도 쉬면 못 닿습니다.</b>
    /// 목표 수량은 달성 전제이므로(§17.4) 일하는 한 채워야 하고, 동시에
    /// <b>쉬는 달이 곧 실패 위험</b>이어야 휴식 회복이 후해도 공짜가 되지 않습니다(§17.5b).
    /// </para>
    /// <para>나머지를 버리지 않도록 누적으로 계산합니다 — 5를 2달에 나누면 2 · 3이 됩니다.</para>
    /// </summary>
    private int WorkShare()
    {
        int worked = _workedMonths + 1;
        int soFar = Contract.Intensity * _workedMonths / Math.Max(1, Contract.Months);
        int upTo = Contract.Intensity * worked / Math.Max(1, Contract.Months);

        _workedMonths = worked;
        return upTo - soFar;
    }

    private int _workedMonths;

    private double DiscoveryChance() =>
        Math.Clamp(
            DeploymentRules.DiscoveryChancePerMonth
            + _party.Max(a => a.Judgement) / 100.0 * DeploymentRules.DiscoveryPerJudgement,
            0.0, 0.95);

    private (bool Won, int Killed, string Foes) Fight(
        int month, IRandomSource rng, IBattleCommander? commander, Action<string>? onLine)
    {
        var standing = Standing;
        if (standing.Count == 0) return (false, 0, "");

        var enemies = EncounterGenerator.Generate(
            Contract.Difficulty, Combatants(standing), _rng.Fork($"enc:{month}"), _nameFor);

        // 적 구성 요약 — 이름 × 수와 HP 범위. 순서는 이름 정렬로 고정합니다 (결정론).
        var byName = enemies.GroupBy(e => e.Name).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key);
        int hpMin = enemies.Min(e => e.MaxHp), hpMax = enemies.Max(e => e.MaxHp);
        string foes = $"{string.Join("·", byName)} (HP {(hpMin == hpMax ? hpMin.ToString() : $"{hpMin}~{hpMax}")})";

        var state = CombatantFactory.FormParty(standing, enemies, _hp, _potions, _mana);
        var result = new BattleResolver(recordLog: onLine is not null).Resolve(state, rng, commander, onLine);

        int killed = 0;
        foreach (var c in state.All)
        {
            if (c.Team == Team.Player)
            {
                _hp[c.Id] = c.Hp;
                _mana[c.Id] = c.Mana;
                _potions[c.Id] = c.Potions;
                _contributions[c.Id] = _contributions.TryGetValue(c.Id, out var prev)
                    ? CombatContribution.Merge([prev, c.Contribution])
                    : c.Contribution;
            }
            else if (!c.IsAlive)
            {
                killed++;
            }
        }

        return (result.Outcome == BattleOutcome.PlayerVictory, killed, foes);
    }

    /// <summary>HP와 마나를 비율만큼 회복합니다. 쓰러진 사람은 파견 중에 일어나지 않습니다.</summary>
    private void Recover(double hpRatio, double manaRatio)
    {
        foreach (var a in _party)
        {
            if (_hp[a.Id] <= 0) continue;

            _hp[a.Id] = Math.Min(a.MaxHp, _hp[a.Id] + (int)Math.Round(a.MaxHp * hpRatio));
            _mana[a.Id] = Math.Min(_maxMana[a.Id], _mana[a.Id] + (int)Math.Round(_maxMana[a.Id] * manaRatio));
        }
    }
}
