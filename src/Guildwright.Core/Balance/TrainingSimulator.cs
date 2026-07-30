using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;

namespace Guildwright.Core.Balance;

/// <summary>한 방침으로 여러 해를 육성했을 때의 결과 분포.</summary>
/// <param name="PolicyName">방침 이름.</param>
/// <param name="Trials">시행 수.</param>
/// <param name="Years">육성 연차.</param>
/// <param name="MeanStats">평균 최종 능력치.</param>
/// <param name="MeanTotal">평균 능력치 총합.</param>
/// <param name="MeanGain">평균 능력치 증가량 (시작 대비).</param>
/// <param name="MeanProficiency">평균 장착 무기 숙련도.</param>
/// <param name="MeanJudgement">평균 판단력.</param>
/// <param name="MeanFailedMonths">평균 실패 개월 수.</param>
/// <param name="MeanRestMonths">평균 휴식 개월 수.</param>
/// <param name="MeanFatigue">평균 피로도 (매달 측정).</param>
/// <param name="ConditionShare">
/// 컨디션 단계별로 보낸 개월 비율. <b>합이 1.0</b>입니다.
/// 인덱스는 <see cref="Condition"/> 순서 (최악 · 저조 · 보통 · 양호 · 절호조).
/// </param>
public sealed record TrainingTrial(
    string PolicyName,
    int Trials,
    int Years,
    PrimaryStats MeanStats,
    double MeanTotal,
    double MeanGain,
    double MeanProficiency,
    double MeanJudgement,
    double MeanFailedMonths,
    double MeanRestMonths,
    double MeanFatigue,
    IReadOnlyList<double> ConditionShare);

/// <summary>
/// 훈련 방침을 배치로 돌려 성장 분포를 냅니다.
/// <para>
/// <b>1~2년 돌려보고 판단하면 안 됩니다.</b> 개화 곡선이 나이에 따라 크게 달라지고
/// 잠재력도 캐릭터마다 굴려지므로, 몇 판으로는 방침 차이인지 캐릭터 운인지 구분이 안 됩니다.
/// </para>
/// <para>
/// <b>같은 캐릭터를 방침만 바꿔 돌립니다.</b> 시행 번호가 같으면 잠재력·개화 시기·적성이
/// 완전히 동일하고, 훈련에 쓰는 난수 스트림도 같습니다. 그래야 차이가 방침에서만 나옵니다.
/// </para>
/// 근거: CLAUDE.md "밸런스 수치를 임의로 적당해 보이게 바꾸지 마세요"
/// </summary>
public static class TrainingSimulator
{
    /// <param name="policy">방침.</param>
    /// <param name="trials">시행 수.</param>
    /// <param name="years">육성 연차.</param>
    /// <param name="seed">
    /// 캐릭터 생성 시드. <b>방침 간 비교에서는 반드시 같은 값을 써야</b> 같은 캐릭터를 비교하게 됩니다.
    /// </param>
    /// <param name="style">장착 무기. 숙련도 비교를 위해 고정합니다.</param>
    public static TrainingTrial Run(
        TrainingPolicy policy,
        int trials = 400,
        int years = 5,
        ulong seed = 900_1,
        WeaponKind style = WeaponKind.Sword)
        => Run(policy.Name, (_, session, _) => policy.ChooseFor(session), trials, years, seed, style);

    /// <summary>
    /// 임의의 선택 방식으로 돌립니다.
    /// <para>
    /// <b>"손으로 키우는 게 얼마나 이득인가"를 재기 위한 것</b>입니다.
    /// 숨겨진 성장 곡선을 다 아는 <see cref="Oracle"/>(실력의 천장)과
    /// <see cref="Random"/>(바닥) 사이 어디에 방침이 있는지를 봅니다.
    /// </para>
    /// </summary>
    /// <param name="name">표시 이름.</param>
    /// <param name="chooser">
    /// (모험가, 세션, 난수) → 이번 달 활동.
    /// <b>모험가를 통째로 넘기므로 숨겨진 값도 볼 수 있습니다</b> — 오라클 측정용입니다.
    /// 실제 게임 코드에서 이렇게 하면 정보 비대칭이 무너집니다.
    /// </param>
    /// <param name="trials">시행 수.</param>
    /// <param name="years">육성 연차.</param>
    /// <param name="seed">캐릭터 생성 시드.</param>
    /// <param name="style">장착 무기.</param>
    public static TrainingTrial Run(
        string name,
        Func<Adventurer, TrainingYearSession, IRandomSource, TrainingActivity> chooser,
        int trials = 400,
        int years = 5,
        ulong seed = 900_1,
        WeaponKind style = WeaponKind.Sword)
    {
        var totalStats = new double[PrimaryStats.AllStats.Count];
        double totalGain = 0, totalProf = 0, totalJudge = 0;
        double totalFailed = 0, totalRest = 0, totalFatigue = 0;
        var conditionMonths = new double[5];
        int fatigueSamples = 0;
        int completed = 0;

        var root = new DeterministicRandom(seed);

        for (int t = 0; t < trials; t++)
        {
            // 캐릭터 생성은 방침과 무관한 스트림에서 — 방침을 바꿔도 같은 사람이 나옵니다.
            var adventurer = Adventurer.Recruit($"S{t}", $"표본{t}", root.Fork($"char:{t}"));
            adventurer.Equip(WeaponSet.Primary, Hand.Right, style);

            int startTotal = adventurer.Stats.Total;

            for (int y = 0; y < years; y++)
            {
                if (adventurer.Status != AdventurerStatus.Active) break;

                var session = new TrainingYearSession(adventurer, root.Fork($"train:{t}:{y}"));

                var choiceStream = root.Fork($"choose:{t}:{y}");

                while (!session.IsComplete)
                {
                    var chosen = chooser(adventurer, session, choiceStream);
                    var outcome = session.AdvanceMonth(chosen);

                    totalFatigue += outcome.FatigueAfter;
                    conditionMonths[(int)outcome.ConditionAfter]++;
                    fatigueSamples++;
                    if (outcome.Failed) totalFailed++;
                    if (outcome.Activity == TrainingActivity.Rest) totalRest++;
                }

                session.Complete();
            }

            foreach (var stat in PrimaryStats.AllStats)
            {
                totalStats[(int)stat] += adventurer.Stats[stat];
            }

            totalGain += adventurer.Stats.Total - startTotal;
            totalProf += adventurer.Proficiency[style];
            totalJudge += adventurer.Judgement;
            completed++;
        }

        var mean = PrimaryStats.Zero;
        foreach (var stat in PrimaryStats.AllStats)
        {
            mean = mean.With(stat, (int)Math.Round(totalStats[(int)stat] / completed));
        }

        return new TrainingTrial(
            name,
            completed,
            years,
            mean,
            totalStats.Sum() / completed,
            totalGain / completed,
            totalProf / completed,
            totalJudge / completed,
            totalFailed / completed,
            totalRest / completed,
            totalFatigue / fatigueSamples,
            conditionMonths.Select(m => m / fatigueSamples).ToArray());
    }

    /// <summary>
    /// 활동 하나만 12개월 내내 시키는 방침. <b>활동별 효율을 직접 재기 위한 것</b>이고
    /// 실제 플레이에서 권장되는 방식은 아닙니다.
    /// </summary>
    public static TrainingPolicy SingleActivity(TrainingActivity activity, int restThreshold = 42) =>
        new([activity], restThreshold, TrainingActivities.NameOf(activity) + "만");

    /// <summary>
    /// <b>실력의 천장.</b> 숨겨진 성장 곡선을 전부 알고 매달 최선을 고릅니다.
    /// <para>
    /// 남은 잠재력이 큰 능력치를 가장 많이 올려주는 활동을 고르되,
    /// 실패 위험과 컨디션을 함께 봅니다. 사람이 완벽한 정보를 갖고 최선을 다한 경우에 해당합니다.
    /// </para>
    /// <para>
    /// ⚠️ <b>측정 전용입니다.</b> 게임 코드가 이렇게 하면 정보 비대칭이 통째로 무너집니다.
    /// </para>
    /// </summary>
    public static TrainingActivity Oracle(Adventurer a, TrainingYearSession session, IRandomSource _)
    {
        var growth = a.Growth;

        // 이번 달 이 활동으로 얼마나 자라는가. 남은 잠재력이 클수록 값어치가 큽니다.
        double GainOf(TrainingActivityProfile p)
        {
            double sum = 0.0;
            foreach (var stat in PrimaryStats.AllStats)
            {
                double weight = p.WeightOf(stat);
                if (weight <= 0.0) continue;

                double remaining = growth.Potential[stat] - a.Stats[stat];
                if (remaining > 0) sum += remaining * weight;
            }
            return sum;
        }

        // 성장 저하선(45)을 넘지 않는 선에서 고릅니다.
        // 넘으면 성장이 깎이고 실패 확률까지 붙어서, 그 달을 반쯤 버리게 됩니다.
        var affordable = TrainingActivities.Trainings
            .Where(p => session.Fatigue + p.FatigueCost <= TrainingRules.FatigueSoftCap)
            .ToList();

        // 다 넘으면 피로가 늘지 않는 활동으로 버팁니다 (명상).
        // 쉬는 것보다 낫습니다 — 한 달을 통째로 버리지 않으니까요.
        if (affordable.Count == 0)
        {
            affordable = TrainingActivities.Trainings.Where(p => p.FatigueCost <= 0).ToList();
        }

        if (affordable.Count == 0) return TrainingActivity.Rest;

        var best = affordable
            .OrderByDescending(GainOf)
            .ThenBy(p => p.Activity)      // 동점은 열거 순서로 — 결정론 유지
            .First();

        // 아무것도 자랄 게 없으면(잠재력을 다 채웠으면) 쉽니다.
        return GainOf(best) <= 0.0 ? TrainingActivity.Rest : best.Activity;
    }

    /// <summary>
    /// 두 육성 방식으로 <b>같은 사람</b>을 키워서 서로 붙입니다.
    ///
    /// <para>
    /// <b>능력치 총합으로는 육성 실력을 잴 수 없습니다.</b> 마법사에게 근력 100을 준 것과
    /// 지능 100을 준 것은 총합이 같아도 전혀 다른 캐릭터입니다.
    /// 무작위로 키우면 골고루 흩어져서 총합만 높고 쓸모는 없을 수 있습니다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>전투 승률</b>로 잽니다. 이게 육성이 실제로 뭘 바꾸는지 보는 유일한 방법입니다.
    /// </para>
    /// </summary>
    /// <returns>왼쪽 방식의 승률.</returns>
    public static double HeadToHead(
        Func<Adventurer, TrainingYearSession, IRandomSource, TrainingActivity> left,
        Func<Adventurer, TrainingYearSession, IRandomSource, TrainingActivity> right,
        int trials = 300,
        int years = 5,
        ulong seed = 5150,
        WeaponKind style = WeaponKind.Sword)
    {
        int leftWins = 0, decided = 0;
        var root = new DeterministicRandom(seed);

        for (int t = 0; t < trials; t++)
        {
            // 완전히 같은 사람을 두 방식으로 키웁니다. 차이는 오직 육성에서만 나옵니다.
            var a = Grow(left, t, years, style, root);
            var b = Grow(right, t, years, style, root);

            if (a is null || b is null) continue;

            // 같은 사람을 키운 둘이라 Id가 같습니다. 전투 대상 선택에 Id를 쓰므로 갈라줍니다.
            // (Id를 바꿔서 새로 만들면 성장 곡선이 달라져 '같은 사람'이 아니게 됩니다.)
            var state = new BattleState([
                Duelist(a, "L", Team.Player),
                Duelist(b, "R", Team.Enemy)
            ]);

            var result = new BattleResolver().Resolve(state, root.Fork($"duel:{t}"));

            if (result.Outcome == BattleOutcome.Draw) continue;

            decided++;
            if (result.Outcome == BattleOutcome.PlayerVictory) leftWins++;
        }

        return decided == 0 ? 0.5 : (double)leftWins / decided;
    }

    private static Combatant Duelist(Adventurer a, string id, Team team) =>
        new(id: id,
            name: $"{id}:{a.Name}",
            team: team,
            stats: a.Stats,
            judgement: a.Judgement,
            loadout: a.Loadout,
            weaponEffectiveness: a.WeaponEffectiveness,
            bonuses: a.Bonuses,
            row: Row.Front,
            tactics: CombatantFactory.DefaultTacticsFor(a.Loadout),
            potions: 2);

    private static Adventurer? Grow(
        Func<Adventurer, TrainingYearSession, IRandomSource, TrainingActivity> chooser,
        int trial, int years, WeaponKind style, IRandomSource root)
    {
        var a = Adventurer.Recruit($"D{trial}", $"결투{trial}", root.Fork($"char:{trial}"));
        a.Equip(WeaponSet.Primary, Hand.Right, style);

        for (int y = 0; y < years; y++)
        {
            if (a.Status != AdventurerStatus.Active) return null;

            var session = new TrainingYearSession(a, root.Fork($"train:{trial}:{y}"));
            var stream = root.Fork($"choose:{trial}:{y}");

            while (!session.IsComplete) session.AdvanceMonth(chooser(a, session, stream));
            session.Complete();
        }

        return a.Status == AdventurerStatus.Active ? a : null;
    }

    /// <summary>
    /// <b>진짜 천장.</b> 능력치 총합이 아니라 <b>전투력</b>을 목표로 고릅니다.
    ///
    /// <para>
    /// <see cref="Oracle"/>은 남은 잠재력만 쫓아서 검사에게 지능·정신을 퍼붓습니다.
    /// 총합은 높은데 전투에서는 집니다 — 실제로 무작위한테도 32%로 졌습니다.
    /// <b>육성의 목적은 총합이 아니라 쓸모 있는 캐릭터입니다.</b>
    /// </para>
    ///
    /// <para>
    /// 여기서는 장착 무기가 실제로 쓰는 능력치에 가중치를 둡니다.
    /// 마법 무기면 지능·정신이, 물리 무기면 힘·활력이 값어치가 있습니다.
    /// </para>
    /// </summary>
    public static TrainingActivity CombatOracle(Adventurer a, TrainingYearSession session, IRandomSource _)
    {
        var growth = a.Growth;
        bool magic = Weaponry.Of(a.Loadout.MainWeapon).UsesMagicPower;

        // 능력치 1점이 전투력에 얼마나 기여하는가.
        // 파생 공식(docs/07 §1)에서 그대로 가져왔습니다.
        double ValueOf(PrimaryStat stat) => stat switch
        {
            PrimaryStat.Strength => magic ? 0.2 : 1.6,      // 물리 위력 1.0 + HP 0.8×0.35 + 방어
            PrimaryStat.Vitality => 1.4,                    // HP 2.8×0.35 + 방어 0.35 — 누구에게나 값어치
            PrimaryStat.Finesse => magic ? 0.2 : 0.6,       // 위력 0.3 + 치명타
            PrimaryStat.Agility => 0.7,                     // 속도 + 회피
            PrimaryStat.Intellect => magic ? 1.5 : 0.1,     // 마법 위력 1.0 + 마나
            PrimaryStat.Spirit => magic ? 1.2 : 0.4,        // 마법 방어 0.5 + 마나 2.0
            _ => 0.3
        };

        double GainOf(TrainingActivityProfile p)
        {
            double sum = 0.0;
            foreach (var stat in PrimaryStats.AllStats)
            {
                double weight = p.WeightOf(stat);
                if (weight <= 0.0) continue;

                double remaining = growth.Potential[stat] - a.Stats[stat];
                if (remaining > 0) sum += remaining * weight * ValueOf(stat);
            }
            return sum;
        }

        var affordable = TrainingActivities.Trainings
            .Where(p => session.Fatigue + p.FatigueCost <= TrainingRules.FatigueSoftCap)
            .ToList();

        if (affordable.Count == 0)
        {
            affordable = TrainingActivities.Trainings.Where(p => p.FatigueCost <= 0).ToList();
        }

        if (affordable.Count == 0) return TrainingActivity.Rest;

        var best = affordable.OrderByDescending(GainOf).ThenBy(p => p.Activity).First();
        return GainOf(best) <= 0.0 ? TrainingActivity.Rest : best.Activity;
    }

    /// <summary><b>실력의 바닥.</b> 매달 아무거나 고릅니다 (휴식 포함).</summary>
    public static TrainingActivity Random(Adventurer _, TrainingYearSession __, IRandomSource rng) =>
        (TrainingActivity)rng.NextInt(0, TrainingActivities.All.Count);
}
