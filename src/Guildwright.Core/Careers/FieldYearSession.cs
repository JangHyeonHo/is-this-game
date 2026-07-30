using Guildwright.Core.Adventurers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;

namespace Guildwright.Core.Careers;

/// <summary>파견 나간 한 달 동안 무엇을 할지.</summary>
public enum FieldAction
{
    /// <summary>수색 — 적극적으로 찾아다닙니다. 조우가 잦고 지칩니다.</summary>
    Search,
    /// <summary>순찰 — 길목을 지킵니다. 무난합니다.</summary>
    Patrol,
    /// <summary>야영 — 쉽니다. HP와 피로가 회복되지만 목표는 안 줄어듭니다.</summary>
    Camp
}

/// <summary>조우한 무리와, 피할 수 있는 가능성.</summary>
/// <param name="Enemies">마주친 적.</param>
/// <param name="AvoidChance">싸우지 않고 빠져나갈 확률.</param>
public sealed record Encounter(IReadOnlyList<Adventurer> Enemies, double AvoidChance);

/// <summary>한 달에 실제로 일어난 일.</summary>
/// <param name="Month">1~12.</param>
/// <param name="Action">고른 행동.</param>
/// <param name="Note">표시용 설명.</param>
/// <param name="Killed">그 달에 처치한 수.</param>
public sealed record FieldMonth(int Month, FieldAction Action, string Note, int Killed);

/// <summary>파견 1년의 결과.</summary>
/// <param name="Killed">총 처치 수.</param>
/// <param name="Quota">목표 수치.</param>
/// <param name="Months">달마다의 기록.</param>
/// <param name="Retreated">중간에 물러났는지 (전멸 직전 귀환).</param>
public sealed record FieldYearResult(int Killed, int Quota, IReadOnlyList<FieldMonth> Months, bool Retreated)
{
    public bool Achieved => Killed >= Quota;
}

/// <summary>
/// 파견 1년을 <b>월 단위</b>로 진행합니다.
///
/// <para>
/// 예전에는 파견 = <b>전투 한 판</b>이었습니다. 그러면 한 해의 성패가 3~4라운드에 결정되고,
/// 회복약도 짐꾼도 쓸 일이 없으며, 개입할 순간도 한두 번뿐입니다.
/// 실제 플레이 피드백이 <b>"개입으로 할 수 있는 게 없다"</b>였는데 원인이 여기였습니다.
/// </para>
///
/// <para>
/// 이제 훈련 연도와 같은 리듬입니다 — <b>12개월 동안 매달 무엇을 할지 고르고,
/// 조우하면 싸울지 피할지 고릅니다.</b> HP와 회복약은 <b>전투 사이에 저절로 회복되지 않습니다.</b>
/// 그래서 이런 판단이 생깁니다.
/// </para>
///
/// <list type="bullet">
///   <item>HP가 반인데 지금 싸울까, 피하고 야영할까</item>
///   <item>회복약 두 개를 언제 쓸까 — 지금 아니면 12월에</item>
///   <item>목표까지 3마리 남았는데 11월이다. 무리해서 수색할까</item>
/// </list>
///
/// 근거: docs/08-design-revision.md §8
/// </summary>
public sealed class FieldYearSession
{
    private readonly IReadOnlyList<Adventurer> _party;
    private readonly Contract _contract;
    private readonly IRandomSource _rng;
    private readonly List<FieldMonth> _months = [];

    /// <summary>전투 사이에 이어지는 HP. <b>저절로 차지 않습니다.</b></summary>
    private readonly Dictionary<string, int> _hp;

    /// <summary>남은 회복약. 한 해 내내 이것만 씁니다 — 짐꾼이 의미를 갖는 지점입니다.</summary>
    private readonly Dictionary<string, int> _potions;

    private readonly Dictionary<string, CombatContribution> _contributions = [];

    private Encounter? _pending;
    private bool _retreated;

    public FieldYearSession(
        IReadOnlyList<Adventurer> party,
        Contract contract,
        int quota,
        IRandomSource rng,
        int potionsEach = 2)
    {
        if (party.Count == 0) throw new ArgumentException("파티가 비어 있습니다.", nameof(party));

        _party = party;
        _contract = contract;
        _rng = rng;
        Quota = quota;

        _hp = party.ToDictionary(a => a.Id, a => a.MaxHp);
        _potions = party.ToDictionary(a => a.Id, _ => potionsEach);
    }

    public int Quota { get; }
    public int Killed { get; private set; }
    public int Fatigue { get; private set; }

    public int CurrentMonth => _months.Count + 1;

    /// <summary>목표를 채웠거나 12개월을 다 썼거나 물러났으면 끝입니다.</summary>
    public bool IsComplete =>
        _retreated || Killed >= Quota || _months.Count >= TrainingRules.MonthsPerYear;

    public IReadOnlyList<FieldMonth> Months => _months;

    /// <summary>남은 HP (모험가 Id → HP). 화면에 그대로 보여줍니다.</summary>
    public IReadOnlyDictionary<string, int> Hp => _hp;

    /// <summary>남은 회복약.</summary>
    public IReadOnlyDictionary<string, int> Potions => _potions;

    /// <summary>조우 중이라 <see cref="Fight"/>나 <see cref="Avoid"/>를 기다리는 상태인지.</summary>
    public bool HasPendingEncounter => _pending is not null;

    /// <summary>
    /// 한 달을 시작합니다. 조우하면 <see cref="Encounter"/>를 돌려주고,
    /// 그때는 반드시 <see cref="Fight"/> 또는 <see cref="Avoid"/>를 호출해야 다음 달로 넘어갑니다.
    /// </summary>
    public Encounter? StartMonth(FieldAction action)
    {
        if (IsComplete) throw new InvalidOperationException("이미 끝난 파견입니다.");
        if (_pending is not null) throw new InvalidOperationException("조우를 아직 처리하지 않았습니다.");

        int month = CurrentMonth;

        Fatigue = Math.Clamp(Fatigue + FieldRules.FatigueOf(action), 0, TrainingRules.MaxFatigue);

        if (action == FieldAction.Camp)
        {
            HealAll(FieldRules.CampHealRatio);
            _months.Add(new FieldMonth(month, action, $"{month}월: 야영 — 상처를 돌보고 쉬었다", 0));
            return null;
        }

        // 지쳐 있으면 놓칩니다. 무작정 수색만 하는 게 답이 아니게 만드는 장치입니다.
        double chance = FieldRules.EncounterChanceOf(action) * FieldRules.Alertness(Fatigue);

        if (!_rng.Chance(chance))
        {
            _months.Add(new FieldMonth(month, action, $"{month}월: {FieldRules.NameOf(action)} — 아무것도 마주치지 않았다", 0));
            return null;
        }

        var enemies = EncounterGenerator.Generate(
            _contract.Difficulty, _party.Count, _rng.Fork($"enc:{month}"), _ => "고블린");

        _pending = new Encounter(enemies, AvoidChanceAgainst(enemies));
        return _pending;
    }

    /// <summary>
    /// 싸우지 않고 빠져나갑니다.
    /// <para>실패하면 <b>기습당한 채로</b> 싸우게 됩니다 — 그래서 회피도 도박입니다.</para>
    /// </summary>
    /// <returns>성공했으면 true. 실패하면 조우가 그대로 남아 <see cref="Fight"/>를 불러야 합니다.</returns>
    public bool Avoid()
    {
        var encounter = _pending ?? throw new InvalidOperationException("조우 중이 아닙니다.");

        if (!_rng.Chance(encounter.AvoidChance))
        {
            _ambushed = true;
            return false;
        }

        int month = CurrentMonth;
        _pending = null;
        _months.Add(new FieldMonth(month, LastAction(), $"{month}월: 발각되기 전에 물러났다", 0));
        return true;
    }

    private bool _ambushed;

    /// <summary>
    /// 조우한 무리와 싸웁니다.
    /// </summary>
    /// <param name="rng">전투용 난수원.</param>
    /// <param name="commander">플레이어 개입 통로.</param>
    /// <param name="onLine">전투 기록 실시간 출력.</param>
    public BattleResult Fight(IRandomSource rng, IBattleCommander? commander = null, Action<string>? onLine = null)
    {
        var encounter = _pending ?? throw new InvalidOperationException("조우 중이 아닙니다.");
        int month = CurrentMonth;

        var alive = _party.Where(a => a.IsAlive && _hp[a.Id] > 0).ToList();
        if (alive.Count == 0)
        {
            _retreated = true;
            _pending = null;
            return new BattleResult(BattleOutcome.EnemyVictory, 0, Array.Empty<string>());
        }

        var state = CombatantFactory.FormParty(alive, encounter.Enemies, _hp, _potions);

        // 기습당했으면 아군이 한 라운드를 손해 봅니다 — 방어 태세로 시작합니다.
        if (_ambushed)
        {
            foreach (var c in state.All.Where(c => c.Team == Team.Player)) c.BeginDefending();
        }

        var result = new BattleResolver(recordLog: true).Resolve(state, rng, commander, onLine);

        // 전투 후 상태를 이어받습니다. 여기가 이 시스템의 핵심입니다.
        int killed = 0;
        foreach (var c in state.All)
        {
            if (c.Team == Team.Player)
            {
                _hp[c.Id] = c.Hp;
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

        Killed += killed;

        string how = _ambushed ? "기습당했다" : "교전";
        string tail = result.Outcome == BattleOutcome.PlayerVictory
            ? $"{killed}마리 처치 (누적 {Killed}/{Quota})"
            : "물러났다";

        _months.Add(new FieldMonth(month, LastAction(), $"{month}월: {how} — {tail}", killed));

        // 아무도 서 있지 못하면 그 해는 거기서 끝입니다.
        if (_party.All(a => _hp[a.Id] <= 0)) _retreated = true;

        _ambushed = false;
        _pending = null;
        return result;
    }

    /// <summary>그 해에 각자가 무엇을 겪었는지. 성장 계산에 넘깁니다.</summary>
    public IReadOnlyDictionary<string, CombatExperience> Experience =>
        _contributions.ToDictionary(kv => kv.Key, kv => CombatExperience.From(kv.Value));

    public FieldYearResult Complete() => new(Killed, Quota, _months, _retreated);

    // ── 내부 ────────────────────────────────────────────────

    private FieldAction LastAction() =>
        _months.Count > 0 && _months[^1].Month == CurrentMonth
            ? _months[^1].Action
            : FieldAction.Patrol;

    private void HealAll(double ratio)
    {
        foreach (var a in _party)
        {
            if (_hp[a.Id] <= 0) continue;
            _hp[a.Id] = Math.Min(a.MaxHp, _hp[a.Id] + (int)Math.Round(a.MaxHp * ratio));
        }
    }

    /// <summary>
    /// 빠져나갈 확률. 파티가 빠를수록 높습니다.
    /// </summary>
    private double AvoidChanceAgainst(IReadOnlyList<Adventurer> enemies)
    {
        double ours = _party.Max(a => (double)a.Stats.Agility);
        double theirs = enemies.Average(e => (double)e.Stats.Agility);

        return Math.Clamp(
            FieldRules.BaseAvoidChance + (ours - theirs) * FieldRules.AvoidPerAgilityPoint,
            FieldRules.MinAvoidChance,
            FieldRules.MaxAvoidChance);
    }
}

/// <summary>
/// 파견 월 단위 진행의 밸런스 상수.
/// <para>⚠️ 전부 임시값입니다. 배치 시뮬레이션으로 검증하고 근거를 docs/06에 남기세요.</para>
/// </summary>
public static class FieldRules
{
    public static string NameOf(FieldAction action) => action switch
    {
        FieldAction.Search => "수색",
        FieldAction.Patrol => "순찰",
        _ => "야영"
    };

    public static string FlavorOf(FieldAction action) => action switch
    {
        FieldAction.Search => "적극적으로 찾아다닌다",
        FieldAction.Patrol => "길목을 지킨다",
        _ => "쉬면서 상처를 돌본다"
    };

    /// <summary>행동별 조우 확률 (피로가 없을 때).</summary>
    public static double EncounterChanceOf(FieldAction action) => action switch
    {
        FieldAction.Search => 0.80,
        FieldAction.Patrol => 0.50,
        _ => 0.10
    };

    public static int FatigueOf(FieldAction action) => action switch
    {
        FieldAction.Search => 14,
        FieldAction.Patrol => 8,
        _ => -30
    };

    /// <summary>
    /// 지쳐 있으면 놓칩니다.
    /// <para>이게 없으면 12개월 내내 수색만 하는 게 언제나 정답이 됩니다.</para>
    /// </summary>
    public static double Alertness(int fatigue) =>
        Math.Clamp(1.0 - Math.Max(0, fatigue - TrainingRules.FatigueSoftCap) / 110.0, 0.5, 1.0);

    /// <summary>야영 한 달의 HP 회복 비율.</summary>
    public const double CampHealRatio = 0.30;

    public const double BaseAvoidChance = 0.55;
    public const double AvoidPerAgilityPoint = 0.012;
    public const double MinAvoidChance = 0.15;
    public const double MaxAvoidChance = 0.90;
}
