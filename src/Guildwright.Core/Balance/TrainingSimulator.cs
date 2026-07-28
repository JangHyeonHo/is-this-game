using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
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
    double MeanFatigue);

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
        WeaponStyle style = WeaponStyle.SwordAndShield)
    {
        var totalStats = new double[PrimaryStats.AllStats.Count];
        double totalGain = 0, totalProf = 0, totalJudge = 0;
        double totalFailed = 0, totalRest = 0, totalFatigue = 0;
        int fatigueSamples = 0;
        int completed = 0;

        var root = new DeterministicRandom(seed);

        for (int t = 0; t < trials; t++)
        {
            // 캐릭터 생성은 방침과 무관한 스트림에서 — 방침을 바꿔도 같은 사람이 나옵니다.
            var adventurer = Adventurer.Recruit($"S{t}", $"표본{t}", root.Fork($"char:{t}"));
            adventurer.Equip(style, WeaponClass.Blade);

            int startTotal = adventurer.Stats.Total;

            for (int y = 0; y < years; y++)
            {
                if (adventurer.Status != AdventurerStatus.Active) break;

                var session = new TrainingYearSession(adventurer, root.Fork($"train:{t}:{y}"));

                while (!session.IsComplete)
                {
                    var chosen = policy.ChooseFor(session);
                    var outcome = session.AdvanceMonth(chosen);

                    totalFatigue += outcome.FatigueAfter;
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
            policy.Name,
            completed,
            years,
            mean,
            totalStats.Sum() / completed,
            totalGain / completed,
            totalProf / completed,
            totalJudge / completed,
            totalFailed / completed,
            totalRest / completed,
            totalFatigue / fatigueSamples);
    }

    /// <summary>
    /// 활동 하나만 12개월 내내 시키는 방침. <b>활동별 효율을 직접 재기 위한 것</b>이고
    /// 실제 플레이에서 권장되는 방식은 아닙니다.
    /// </summary>
    public static TrainingPolicy SingleActivity(TrainingActivity activity, int restThreshold = 42) =>
        new([activity], restThreshold, TrainingActivities.NameOf(activity) + "만");
}
