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
    /// 달마다의 예상 피로와 실패 확률.
    /// <para>
    /// <b>피로는 계획만으로 정확히 계산됩니다</b> (실패하지 않는 한). 숨길 이유가 없고,
    /// 이걸 봐야 "언제 쉴 것인가"라는 선택이 성립합니다.
    /// </para>
    /// <para>
    /// 실패 확률도 같이 보여줍니다. 확률을 숨기면 "무리할까 말까"가 판단이 아니라 감이 됩니다.
    /// </para>
    /// </summary>
    private static void Fatigue(YearForecast forecast, int? decidedMonths)
    {
        int count = forecast.FatigueByMonth.Count;
        if (count == 0) return;

        int decided = decidedMonths ?? count;

        // 실패 판정은 "그 달을 시작할 때"의 피로로 합니다. 그래서 훈련을 마친 뒤 피로가
        // 실패선을 넘어도 그 달은 위험이 아닙니다. 안 적어두면 화면이 모순처럼 보입니다.
        Ui.Line($"   예상 피로 (활동마다 다름 · 휴식 −{TrainingRules.FatigueRecoveryOnRest} " +
                $"· {TrainingRules.FatigueSoftCap} 넘으면 성장 저하 " +
                $"· 달을 시작할 때 {TrainingRules.FailureThreshold} 넘으면 실패 위험)");

        Ui.Line("     월  " + string.Join("", Enumerable.Range(1, count).Select(m => $"{m,5}")));
        Ui.Line("     피로" + string.Join("", forecast.FatigueByMonth.Select((f, i) =>
            i >= decided ? $"{"·",5}" : $"{f,5}")));

        // 실패 확률이 붙은 달이 하나라도 있을 때만 줄을 늘립니다.
        if (forecast.MonthsAtRisk > 0)
        {
            Ui.Line("     실패" + string.Join("", forecast.FailureChanceByMonth.Select((c, i) =>
                i >= decided ? $"{"·",5}"
                : c <= 0.0 ? $"{"-",5}"
                : $"{$"{c * 100:F0}%",5}")));
        }

        string risk = forecast.MonthsAtRisk == 0
            ? "실패 위험 없음"
            : $"⚠ 실패 위험 {forecast.MonthsAtRisk}개월 · 최대 {forecast.WorstFailureChance:P0} " +
              $"· 기대 실패 {forecast.ExpectedFailedMonths:F1}개월";

        string extra = "";
        if (forecast.ProficiencyGain > 0) extra += $" · 무기 숙련 +{forecast.ProficiencyGain:F0}";
        if (forecast.JudgementGain > 0) extra += $" · 판단력 +{forecast.JudgementGain:F0}";

        Ui.Note($"최고 피로 {forecast.PeakFatigue} · {risk}{extra}" +
                (decided < count ? $" · {decided + 1}월부터는 아직 미정" : ""));
    }

    /// <summary>계획 요약줄의 압축 표기.</summary>
    public static string FocusName(TrainingActivity activity) => activity switch
    {
        TrainingActivity.Strength => "근력",
        TrainingActivity.Endurance => "지구력",
        TrainingActivity.Technique => "기술",
        TrainingActivity.Study => "학술",
        TrainingActivity.Meditation => "명상",
        TrainingActivity.Sparring => "모의전",
        _ => "휴식"
    };

    /// <summary>문장 안에서 읽히는 이름.</summary>
    public static string FocusLabel(TrainingActivity activity) => TrainingActivities.NameOf(activity);

    /// <summary>
    /// 월별 행동 메뉴.
    /// <para>
    /// 무엇이 오르는지를 라벨에 적습니다 — 활동 이름만으로는 "명상이 뭘 올리지"를 알 수 없고,
    /// 그러면 첫 플레이에서 12번 다 찍어봐야 합니다.
    /// 피로 증감도 같이 적습니다. 휴식이 얼마나 회복시키는지 모르면 "언제 쉴까"를 계산할 수 없습니다.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> FocusMenu()
    {
        var menu = new List<string>();

        foreach (var p in TrainingActivities.Trainings)
        {
            string stats = string.Join(" ", p.AffectedStats
                .OrderByDescending(p.WeightOf)
                .Select(s => $"{s.ToKorean()}{Dots(p.WeightOf(s))}"));

            string extra = "";
            if (p.ProficiencyPerMonth > 0) extra += $" 무기숙련+{p.ProficiencyPerMonth:0.#}";
            if (p.JudgementPerMonth > 0) extra += $" 판단력+{p.JudgementPerMonth:0.##}";

            // 피로가 음수인 활동(명상)은 "회복"으로 읽히게 적습니다.
            string fatigue = p.FatigueCost >= 0
                ? $"피로 +{p.FatigueCost}"
                : $"피로 −{-p.FatigueCost} (회복)";

            menu.Add($"{p.Name} ({p.Flavor}) — {stats}{extra} · {fatigue}");
        }

        menu.Add($"휴식 — 피로 −{TrainingRules.FatigueRecoveryOnRest}, 컨디션 회복");
        return menu;
    }

    /// <summary>가중치를 점으로. 숫자를 그대로 보여주면 화면이 표처럼 됩니다.</summary>
    private static string Dots(double weight) => weight switch
    {
        >= 0.9 => "●●●",
        >= 0.45 => "●●",
        _ => "●"
    };

    public static TrainingActivity FocusFromIndex(int index) => (TrainingActivity)index;

    /// <summary>파견 중 현재 상태 — 진행도, 파티 HP, 회복약, 피로.</summary>
    public static void FieldStatus(FieldYearSession session, IReadOnlyList<Adventurer> party)
    {
        Ui.Line();
        Ui.Line($"   ── {session.CurrentMonth}월 · 처치 {session.Killed}/{session.Quota} · 피로 {session.Fatigue} ──");

        foreach (var a in party)
        {
            int hp = session.Hp[a.Id];
            string state = hp <= 0 ? "  전투 불능" : "";
            Ui.Line($"     {a.Name,-16} {Ui.Bar((double)hp / a.MaxHp, 10)} {hp,4}/{a.MaxHp,-4} " +
                    $"회복약 {session.Potions[a.Id]}{state}");
        }
    }

    /// <summary>파견 월별 행동 메뉴. 조우 확률과 피로를 라벨에 적습니다.</summary>
    public static IReadOnlyList<string> FieldMenu() =>
        Enum.GetValues<FieldAction>()
            .Select(a =>
            {
                int fatigue = FieldRules.FatigueOf(a);
                string cost = fatigue >= 0 ? $"피로 +{fatigue}" : $"피로 −{-fatigue}, HP 회복";
                return $"{FieldRules.NameOf(a)} ({FieldRules.FlavorOf(a)}) — " +
                       $"조우 {FieldRules.EncounterChanceOf(a):P0} · {cost}";
            })
            .ToList();

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
        return "  <" + string.Join(",", c.Effects.Select(e => e.ToString())) + ">";
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
