using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;

namespace Guildwright.Cli;

/// <summary>모험가와 전황을 화면에 그리는 방법. 규칙은 코어에 있고 여기는 표시만 합니다.</summary>
public static class Display
{
    /// <summary>원천 능력치와 파생 수치를 나란히 보여줍니다.</summary>
    public static void StatSheet(Adventurer a)
    {
        Ui.Line($"   {a.Name} · {a.Title} ({a.Age}세)  [{a.EquippedStyle.ToKorean()}·{a.EquippedClass.ToKorean()}]");
        Ui.Line($"   ┌ 원천 ──────────┬ 파생 ─────────────────────────");
        Ui.Line($"   │ 힘   {a.Stats.Strength,3}       │ 물리 위력 {a.PhysicalPower,4}   최대 HP {a.MaxHp,4}");
        Ui.Line($"   │ 민첩 {a.Stats.Agility,3}       │ 마법 위력 {a.MagicPower,4}   최대 MP {DerivedStats.MaxMana(a.Stats, a.Bonuses),4}");
        Ui.Line($"   │ 기교 {a.Stats.Finesse,3}       │ 물리 방어 {a.PhysicalGuard,4}   치명타율 {a.CritChance,4:P0}");
        Ui.Line($"   │ 활력 {a.Stats.Vitality,3}       │ 마법 방어 {a.MagicGuard,4}   회피율   {a.EvasionChance,4:P0}");
        Ui.Line($"   │ 지능 {a.Stats.Intellect,3}       │ 판단력    {a.Judgement,4}   숙련     {a.Proficiency[a.EquippedStyle],4}");
        Ui.Line($"   │ 정신 {a.Stats.Spirit,3}       │ 연봉      {a.AnnualWage,4}");
        Ui.Line($"   └────────────────┴───────────────────────────────");

        if (a.Bonuses.HasAny) Ui.Note($"겪어서 얻은 것: {a.Bonuses}");
        if (a.Support.ToString() != "") Ui.Note($"비전투 역량: {a.Support}");
    }

    /// <summary>플레이어가 볼 수 있는 정보만 담긴 평가서.</summary>
    public static void Scouting(Adventurer a, ScoutingReport r)
    {
        Ui.Line($"   {a.Name} ({a.Age}세)  힘{a.Stats.Strength} 민{a.Stats.Agility} 기{a.Stats.Finesse} " +
                $"활{a.Stats.Vitality} 지{a.Stats.Intellect} 정{a.Stats.Spirit}");
        Ui.Line($"   [평가서] 확신도 {r.Confidence:P0} ({r.ConfidenceLabel}) {Ui.Bar(r.Confidence)}");
        Ui.Line($"     · {r.TimingText}");
        Ui.Line($"     · {r.TemperamentText}");
        Ui.Line($"     · 추정 잠재력: 힘{r.EstimatedPotential.Strength} 민{r.EstimatedPotential.Agility} " +
                $"기{r.EstimatedPotential.Finesse} 활{r.EstimatedPotential.Vitality} " +
                $"지{r.EstimatedPotential.Intellect} 정{r.EstimatedPotential.Spirit}");

        var top = r.AptitudeHints.OrderByDescending(kv => kv.Value).Take(3)
                   .Select(kv => $"{kv.Key.ToKorean()} {kv.Value}");
        Ui.Line($"     · 어울려 보이는 무기: {string.Join(", ", top)}");
    }

    /// <summary>
    /// 1년 계획의 예상 결과 — 원천 성장, 전투 수치 변화, 달마다의 피로.
    /// <para>
    /// 성장만 보여주면 계획 화면이 반쪽입니다. "힘 +12"가 전투에서 뭘 바꾸는지,
    /// 그 대가로 피로가 어디까지 쌓이는지가 같이 보여야 판단이 됩니다.
    /// </para>
    /// </summary>
    /// <param name="decidedMonths">
    /// 아직 계획 중일 때 <b>실제로 고른 달의 수</b>. 나머지 달은 "휴식으로 가정"일 뿐이므로
    /// 확정된 것처럼 0을 찍으면 안 됩니다. 생략하면 전부 확정된 것으로 봅니다.
    /// </param>
    public static void Forecast(YearForecast forecast, double confidence, int? decidedMonths = null)
    {
        Ui.Line($"   예상 성장 (확신도 {confidence:P0} — 낮을수록 범위가 넓고 빗나갑니다)");
        foreach (var chunk in forecast.Stats.Chunk(3))
        {
            Ui.Line("     " + string.Join("   ", chunk.Select(f => $"{f.Stat.ToKorean(),-2} +{f.Min,2}~+{f.Max,-3}")));
        }

        var moving = forecast.Derived.Where(d => d.Moves).ToList();
        if (moving.Count > 0)
        {
            Ui.Line("   그에 따른 전투 수치");
            foreach (var chunk in moving.Chunk(2))
            {
                Ui.Line("     " + string.Join("", chunk.Select(d => PadWide(FormatDerived(d), 30))));
            }
        }

        Fatigue(forecast, decidedMonths);
    }

    private static string FormatDerived(DerivedForecast d) =>
        d.IsRate
            ? $"{d.Stat.ToKorean()} +{d.Min * 100:F1}~+{d.Max * 100:F1}%p"
            : $"{d.Stat.ToKorean()} +{d.Min:F0}~+{d.Max:F0}";

    /// <summary>
    /// 한글이 섞인 문자열을 <b>터미널 표시 폭</b> 기준으로 채웁니다.
    /// <para>한글은 두 칸을 차지하므로 <c>string.PadRight</c>로는 열이 맞지 않습니다.</para>
    /// </summary>
    private static string PadWide(string text, int width)
    {
        int shown = text.Sum(c => c >= 0x1100 ? 2 : 1);
        return text + new string(' ', Math.Max(1, width - shown));
    }

    /// <summary>
    /// 달마다의 예상 피로.
    /// <para>
    /// 성장 예상과 달리 <b>피로는 계획만으로 정확히 계산됩니다.</b> (부상이 나지 않는 한)
    /// 숨길 이유가 없고, 오히려 이걸 봐야 "언제 쉴 것인가"라는 선택이 성립합니다.
    /// </para>
    /// </summary>
    private static void Fatigue(YearForecast forecast, int? decidedMonths)
    {
        int count = forecast.FatigueByMonth.Count;
        if (count == 0) return;

        int decided = decidedMonths ?? count;

        Ui.Line($"   예상 피로 (위험선 {TrainingRules.InjuryThreshold} · 훈련 +{TrainingRules.FatiguePerTraining} " +
                $"· 휴식 −{TrainingRules.FatigueRecoveryOnRest} · {TrainingRules.FatigueSoftCap} 넘으면 성장 저하)");

        Ui.Line("     월  " + string.Join("", Enumerable.Range(1, count).Select(m => $"{m,5}")));
        Ui.Line("     피로" + string.Join("", forecast.FatigueByMonth.Select((f, i) =>
            i >= decided ? $"{"·",5}"
            : f > TrainingRules.InjuryThreshold ? $"{f + "!",5}"
            : $"{f,5}")));

        string risk = forecast.MonthsAtInjuryRisk == 0
            ? "부상 위험 없음"
            : $"⚠ 부상 위험 {forecast.MonthsAtInjuryRisk}개월 (!)";

        Ui.Note($"최고 피로 {forecast.PeakFatigue} · {risk}" +
                (decided < count ? $" · {decided + 1}월부터는 아직 미정" : ""));
    }

    /// <summary>문장 안에서 읽히는 이름. 계획 요약줄의 압축 표기(<see cref="FocusName"/>)와 다릅니다.</summary>
    public static string FocusLabel(TrainingFocus focus) =>
        focus == TrainingFocus.Rest ? "휴식" : FocusName(focus) + " 훈련";

    public static string FocusName(TrainingFocus focus) => focus switch
    {
        TrainingFocus.Strength => "힘",
        TrainingFocus.Agility => "민첩",
        TrainingFocus.Finesse => "기교",
        TrainingFocus.Vitality => "활력",
        TrainingFocus.Intellect => "지능",
        TrainingFocus.Spirit => "정신",
        _ => "휴식"
    };

    /// <summary>
    /// 월별 행동 메뉴. 피로 증감을 라벨에 함께 적습니다 —
    /// 휴식이 얼마나 회복시키는지 모르면 "언제 쉴까"를 계산할 수 없습니다.
    /// </summary>
    public static IReadOnlyList<string> FocusMenu() =>
    [
        $"힘 훈련 (피로 +{TrainingRules.FatiguePerTraining})",
        $"민첩 훈련 (피로 +{TrainingRules.FatiguePerTraining})",
        $"기교 훈련 (피로 +{TrainingRules.FatiguePerTraining})",
        $"활력 훈련 (피로 +{TrainingRules.FatiguePerTraining})",
        $"지능 훈련 (피로 +{TrainingRules.FatiguePerTraining})",
        $"정신 훈련 (피로 +{TrainingRules.FatiguePerTraining})",
        $"휴식 (피로 −{TrainingRules.FatigueRecoveryOnRest}, 컨디션 회복)"
    ];

    public static TrainingFocus FocusFromIndex(int index) => (TrainingFocus)index;

    /// <summary>전투 한 라운드의 진영 상태.</summary>
    public static void Formation(BattleState state)
    {
        foreach (var team in new[] { Team.Player, Team.Enemy })
        {
            string label = team == Team.Player ? "아군" : "적군";
            Ui.Line($"   {label}");

            foreach (var row in new[] { Row.Front, Row.Back })
            {
                var members = state.LivingIn(team, row);
                if (members.Count == 0) continue;

                string rowName = row == Row.Front ? "전열" : "후열";
                foreach (var c in members)
                {
                    Ui.Line($"     {rowName} {c.Name,-10} {Ui.Bar(c.HpRatio, 10)} {c.Hp,4}/{c.MaxHp,-4} " +
                            $"{c.Style.ToKorean()}{FormatEffects(c)}");
                }
            }
        }
    }

    private static string FormatEffects(Combatant c)
    {
        if (c.Effects.Count == 0) return "";
        return "  <" + string.Join(",", c.Effects.Select(e => StatusEffect.ToKorean(e.Kind))) + ">";
    }

    public static string ActionName(TacticAction action) => action switch
    {
        TacticAction.AttackNearest => "가까운 적 공격",
        TacticAction.AttackWeakest => "약한 적 공격",
        TacticAction.AttackStrongest => "강한 적 공격",
        TacticAction.AttackBackRow => "적 후열 공격",
        TacticAction.AttackAll => "광역 공격",
        TacticAction.HealAlly => "회복",
        TacticAction.BuffAlly => "아군 강화",
        TacticAction.DebuffEnemy => "적 약화",
        TacticAction.Taunt => "도발",
        TacticAction.UsePotion => "회복약",
        TacticAction.Defend => "방어",
        TacticAction.MoveBack => "후열로 물러남",
        TacticAction.MoveFront => "전열로 나섬",
        _ => action.ToString()
    };

    public static void Contract(Contract c, int index)
    {
        string kind = c.Kind switch
        {
            ContractKind.Combat => "전투형",
            ContractKind.Gathering => "채집형",
            _ => "탐색형"
        };

        Ui.Line($"   {index}) [{c.Name}] 난이도 {c.Difficulty} · {kind}");

        if (c.Preferences.Count > 0)
        {
            var prefs = c.Preferences.OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key.ToKorean()} {new string('●', (int)Math.Round(kv.Value * 3))}");
            Ui.Line($"        유리: {string.Join("  ", prefs)}");
        }
    }
}
