using Guildwright.Core.Adventurers;
using Guildwright.Core.Balance;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;

namespace Guildwright.Cli;

/// <summary>
/// 배치 시뮬레이션 결과를 표로 출력합니다.
/// <para>
/// <b>밸런스 수치를 감으로 바꾸지 않기 위한 도구입니다.</b> 1~2년 돌려보고 "이 정도면 되겠지"로
/// 정하면, 개화 곡선과 잠재력 굴림 때문에 방침 차이인지 캐릭터 운인지 구분이 안 됩니다.
/// </para>
/// <para><c>dotnet run --project src/Guildwright.Console -- sim</c></para>
/// </summary>
public static class BalanceReport
{
    public static void TrainingPolicies(int trials, int years)
    {
        Ui.Title($"훈련 방침 배치 시뮬레이션 — {trials}회 × {years}년");
        Ui.Note("같은 캐릭터를 방침만 바꿔 돌립니다. 차이는 오직 방침에서만 나옵니다.");

        var policies = new List<TrainingPolicy>
        {
            TrainingPolicy.Balanced,
            TrainingPolicy.Vanguard,
            TrainingPolicy.Mage,
            TrainingPolicy.Skirmisher,
            TrainingPolicy.Weaponmaster,
            TrainingPolicy.Balanced.Cautious(),
            TrainingPolicy.Balanced.Aggressive(),
            TrainingPolicy.Balanced.Opportunistic()
        };

        Section("방침 비교", policies.Select(p => TrainingSimulator.Run(p, trials, years)).ToList());

        // 활동 하나만 계속 시켜서 활동별 효율을 직접 잽니다.
        // 실제 플레이 방식이 아니라 "이 활동이 얼마나 이득인가"를 재기 위한 것입니다.
        var singles = TrainingActivities.Trainings
            .Select(a => TrainingSimulator.SingleActivity(a.Activity))
            .Select(p => TrainingSimulator.Run(p, trials, years))
            .ToList();

        Section("활동별 효율 (한 활동만 계속)", singles);

        // ★ 이 게임의 근본 질문 — "손으로 잘 키우는 게 얼마나 이득인가"
        SkillCeiling(trials, years);

        Ui.Line();
        Ui.Note("⚠ 이 수치는 밸런스 판단 재료일 뿐입니다. 어느 쪽이 '재미있는가'는 사람이 정합니다.");
    }

    /// <summary>
    /// <b>실력의 폭</b>을 잽니다 — 육성을 잘하는 것이 결과를 얼마나 바꾸는가.
    /// <para>
    /// 육성이 메인인 게임에서 이 폭이 좁으면 <b>육성 실력이 결과를 안 바꾼다</b>는 뜻이라
    /// 훈련 설계를 다시 봐야 합니다. 위임(방침)이 어디에 있는지도 여기서 보입니다.
    /// </para>
    /// </summary>
    private static void SkillCeiling(int trials, int years)
    {
        var results = new List<TrainingTrial>
        {
            TrainingSimulator.Run("전투 오라클", TrainingSimulator.CombatOracle, trials, years),
            TrainingSimulator.Run("총합 오라클", TrainingSimulator.Oracle, trials, years),
            TrainingSimulator.Run(TrainingPolicy.Balanced.Opportunistic(), trials, years),
            TrainingSimulator.Run(TrainingPolicy.Balanced, trials, years),
            TrainingSimulator.Run("무작위", TrainingSimulator.Random, trials, years)
        };

        Ui.Section("실력의 폭 — 잘 키우는 것이 얼마나 이득인가");
        Ui.Note("오라클 = 숨겨진 성장 곡선을 다 알고 최선을 고름 (사람의 천장)");
        Ui.Note("무작위 = 아무거나 고름 (바닥)");

        Ui.Line();
        Ui.Line("   방식          총합   증가 │ 휴식  평균피로   바닥 대비");
        Ui.Line("   " + new string('─', 56));

        double floor = results[^1].MeanTotal;

        foreach (var r in results)
        {
            Ui.Line("   " + PadWide(r.PolicyName, 14) +
                    $"{r.MeanTotal,5:F0} {r.MeanGain,6:F0} │ " +
                    $"{r.MeanRestMonths,4:F1} {r.MeanFatigue,9:F0} " +
                    $"{r.MeanTotal / floor,11:P0}");
        }

        double ceiling = results[0].MeanTotal;
        double policy = results[2].MeanTotal;

        Ui.Line();
        Ui.Note($"능력치 총합 기준 — 실력의 폭 (오라클 ÷ 무작위) = {ceiling / floor:P0}");
        Ui.Note($"위임이 천장의 {policy / ceiling:P0} 지점에 있습니다.");

        // ★ 총합으로는 육성 실력을 잴 수 없습니다. 같은 사람을 다르게 키워서 붙여봅니다.
        Ui.Line();
        Ui.Line("   같은 사람을 두 방식으로 키워 1:1로 붙였을 때 승률");
        Ui.Line("   " + new string('─', 46));

        int duels = Math.Max(100, trials / 2);

        Func<Adventurer, TrainingYearSession, IRandomSource, TrainingActivity> delegated =
            (_, s, _) => TrainingPolicy.Balanced.ChooseFor(s);

        Ui.Line($"     전투 오라클 vs 무작위   " +
                $"{TrainingSimulator.HeadToHead(TrainingSimulator.CombatOracle, TrainingSimulator.Random, duels, years),6:P0}");
        Ui.Line($"     전투 오라클 vs 위임     " +
                $"{TrainingSimulator.HeadToHead(TrainingSimulator.CombatOracle, delegated, duels, years),6:P0}");
        Ui.Line($"     전투 오라클 vs 총합오라클 " +
                $"{TrainingSimulator.HeadToHead(TrainingSimulator.CombatOracle, TrainingSimulator.Oracle, duels, years),6:P0}");
        Ui.Line($"     위임        vs 무작위   " +
                $"{TrainingSimulator.HeadToHead(delegated, TrainingSimulator.Random, duels, years),6:P0}");

        Ui.Line();
        Ui.Note("50%에 가까울수록 그 차이가 전투에서 의미가 없다는 뜻입니다.");
    }

    private static void Section(string title, IReadOnlyList<TrainingTrial> results)
    {
        Ui.Section(title);
        Ui.Line();
        Ui.Line("   방침          총합   증가   힘  민첩  기교  활력  지능  정신 │ 숙련  판단  실패  휴식  평균피로");
        Ui.Line("   " + new string('─', 96));

        foreach (var r in results)
        {
            Ui.Line(
                "   " + PadWide(r.PolicyName, 14) +
                $"{r.MeanTotal,5:F0} {r.MeanGain,6:F0}  " +
                $"{r.MeanStats.Strength,3} {r.MeanStats.Agility,4} {r.MeanStats.Finesse,5} " +
                $"{r.MeanStats.Vitality,5} {r.MeanStats.Intellect,5} {r.MeanStats.Spirit,5} │ " +
                $"{r.MeanProficiency,4:F0} {r.MeanJudgement,5:F0} {r.MeanFailedMonths,5:F1} " +
                $"{r.MeanRestMonths,5:F1} {r.MeanFatigue,9:F0}");
        }

        Ui.Line();
        Ui.Line("   컨디션 분포 (그 단계에서 보낸 개월 비율)");
        Ui.Line("   방침          최악  저조  보통  양호 절호조");
        Ui.Line("   " + new string('─', 46));

        foreach (var r in results)
        {
            Ui.Line("   " + PadWide(r.PolicyName, 14) +
                    string.Join("", r.ConditionShare.Select(v => $"{v,5:P0} ")));
        }

        // 가장 총합이 높은 방침을 기준으로 상대 비교를 보여줍니다.
        var best = results.MaxBy(r => r.MeanTotal)!;
        Ui.Line();
        Ui.Note($"최고 총합: {best.PolicyName} ({best.MeanTotal:F0})");

        foreach (var r in results.OrderByDescending(r => r.MeanTotal).Skip(1))
        {
            double ratio = r.MeanTotal / best.MeanTotal;
            Ui.Line($"     {PadWide(r.PolicyName, 14)}{ratio,6:P0}   ({r.MeanTotal - best.MeanTotal:+0;-0})");
        }
    }

    /// <summary>한글은 두 칸을 차지하므로 표시 폭 기준으로 채웁니다.</summary>
    private static string PadWide(string text, int width)
    {
        int shown = text.Sum(c => c >= 0x1100 ? 2 : 1);
        return text + new string(' ', Math.Max(1, width - shown));
    }
}
