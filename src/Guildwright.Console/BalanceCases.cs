using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Guildwright.Core.Training;
using Guildwright.Core.Weapons;

namespace Guildwright.Cli;

/// <summary>
/// 밸런스 측정 케이스 모음 — <c>dotnet run --project src/Guildwright.Console -- balance</c>.
/// <para>
/// 조건(캐릭터 사양 · 훈련 패턴 · 무기 · 적 · 시드)이 전부 이 파일에 고정되어 있어,
/// 누가 언제 돌려도 같은 숫자가 나온다. 대화나 감이 아니라 이 출력이 밸런스 논의의
/// 근거다. 결과 기록: docs/08-balance-log.md #69.
/// </para>
/// </summary>
internal static class BalanceCases
{
    // ── 기준 캐릭터 ────────────────────────────────────────────
    // "리안급": 튜토리얼 고정 캐릭터와 같은 사양 (docs/07 §1 · §19 — 평범형).
    //   시작 능력치 (11,11,12,12,10,10) · 판단력 16 · 15세
    //   잠재력 (66,62,64,68,55,58) · 개화 21세 · 균형 기질 · 쇠퇴 36세
    private static Adventurer Rian(string id, WeaponMaterial material)
    {
        var loadout = new Loadout();
        loadout.Equip(WeaponSet.Primary, Hand.Right, WeaponKind.Sword, material);
        loadout.Equip(WeaponSet.Primary, Hand.Left, WeaponKind.Shield);

        return new Adventurer(
            id, "리안",
            new PrimaryStats(11, 11, 12, 12, 10, 10),
            judgement: 16,
            new GrowthProfile
            {
                PeakAge = 21, BloomWidth = 5.0,
                Temperament = Temperament.Balanced,
                Potential = new PrimaryStats(66, 62, 64, 68, 55, 58),
                DeclineAge = 36
            },
            loadout: loadout);
    }

    /// <summary>훈련 패턴. 훈훈휴 = 두 달 훈련 + 한 달 휴식 반복 (실훈련 8달).</summary>
    private static void TrainYear(Adventurer member, TrainingActivity focus, bool withRest, string seed)
    {
        var session = new TrainingYearSession(member, new DeterministicRandom(seed));
        for (int m = 0; m < 12; m++)
            session.AdvanceMonth(withRest && m % 3 == 2 ? TrainingActivity.Rest : focus);
        session.Settle();
    }

    public static void Run()
    {
        Console.WriteLine("Guildwright 밸런스 측정 케이스 — 조건·시드 고정, 항상 같은 결과");
        Console.WriteLine("(기준 캐릭터 '리안급' 사양은 BalanceCases.cs 상단에 명시)");

        Case1_월별_성장_트레이스();
        Case2_첫해_성장_분포();
        Case3_활동별_실전투_승률();
        Case4_전투_로그_전문();
    }

    // ── 케이스 1 — 리안이 근력만 12달: 달마다 힘이 몇 오르나 ──
    private static void Case1_월별_성장_트레이스()
    {
        Console.WriteLine("\n[케이스 1] 리안 · 근력만 12달 내리 (시드 case1) — 월별 힘");
        var rian = Rian("C1", WeaponMaterial.Wood);
        var session = new TrainingYearSession(rian, new DeterministicRandom("case1"));

        var preview = session.PreviewMonth(TrainingActivity.Strength);
        Console.WriteLine("  예보(1달 근력): " + string.Join(" · ", preview.Select(p => $"{p.Stat.ToKorean()}+{p.Gain:F2}")));

        Console.Write("  힘: 11");
        for (int m = 1; m <= 12; m++)
        {
            var outcome = session.AdvanceMonth(TrainingActivity.Strength);
            Console.Write($" → {rian.Stats.Strength}{(outcome.Failed ? "(실패)" : "")}");
        }
        session.Settle();
        Console.WriteLine($"\n  결산 후: 힘 {rian.Stats.Strength} · 활력 {rian.Stats.Vitality} · HP {rian.MaxHp}");
    }

    // ── 케이스 2 — 모집 신입 2,000명: 첫해 힘 분포 ──
    private static void Case2_첫해_성장_분포()
    {
        const int Sample = 2000;
        Console.WriteLine($"\n[케이스 2] 모집 신입 {Sample}명 · 근력 1년 — 첫해 힘 분포 (시드 c2:*)");

        foreach (bool withRest in new[] { false, true })
        {
            var values = new List<int>(Sample);
            for (int i = 0; i < Sample; i++)
            {
                var member = Adventurer.Recruit($"C2{i}", $"C2{i}", new DeterministicRandom($"c2:{i}"));
                TrainYear(member, TrainingActivity.Strength, withRest, $"c2y:{i}");
                values.Add(member.Stats.Strength);
            }
            values.Sort();
            Console.WriteLine($"  {(withRest ? "훈훈휴" : "내리 12달")}: " +
                $"평균 {values.Average():F1} · 중앙값 {values[Sample / 2]} · 상위5% {values[(int)(Sample * 0.95)]} " +
                $"· 최대 {values[^1]} · 50이상 {values.Count(v => v >= 50)}명 · 60이상 {values.Count(v => v >= 60)}명");
        }
    }

    // ── 케이스 3 — 리안급(훈훈휴·철검)이 활동별로 컸을 때, 난이도 1·2 실전투 ──
    private static void Case3_활동별_실전투_승률()
    {
        const int Sample = 200;
        const int Battles = 5;
        var activities = new TrainingActivity?[]
        {
            null,
            TrainingActivity.Strength, TrainingActivity.Endurance, TrainingActivity.Technique,
            TrainingActivity.Study, TrainingActivity.Meditation, TrainingActivity.Sparring
        };

        Console.WriteLine($"\n[케이스 3] 리안급 · 훈훈휴 · 철검+방패 — 난이도 1·2 적과 1:1 실전투 ({Sample}×{Battles}판, 시드 c3:*)");
        foreach (int difficulty in new[] { 1, 2 })
        {
            Console.WriteLine($"  ── 난이도 {difficulty} ──");
            foreach (var activity in activities)
            {
                int wins = 0, total = 0, rounds = 0, oneShot = 0, probes = 0;
                for (int i = 0; i < Sample; i++)
                {
                    var rian = Rian($"C3{difficulty}{activity}{i}", WeaponMaterial.Iron);
                    if (activity is { } chosen) TrainYear(rian, chosen, withRest: true, $"c3t:{chosen}:{i}");

                    var foe = EncounterGenerator.Generate(
                        difficulty, 1, new DeterministicRandom($"c3f:{difficulty}:{i}"), _ => "고블린")[0];

                    // 첫 타 한방컷 표본 (전투와 별도)
                    var probe = CombatantFactory.FormParty([rian], [foe]);
                    var me = probe.All.First(c => c.Team == Team.Player);
                    var enemy = probe.All.First(c => c.Team == Team.Enemy);
                    var hit = DamageModel.ResolveAttack(me, enemy, new DeterministicRandom($"c3p:{difficulty}:{i}"));
                    if (!hit.Evaded) { probes++; if (hit.Damage >= enemy.MaxHp) oneShot++; }

                    for (int b = 0; b < Battles; b++)
                    {
                        var state = CombatantFactory.FormParty([rian], [foe]);
                        var result = new BattleResolver().Resolve(
                            state, new DeterministicRandom($"c3b:{difficulty}:{i}:{b}"));
                        total++;
                        rounds += result.Rounds;
                        if (result.Outcome == BattleOutcome.PlayerVictory) wins++;
                    }
                }

                string label = activity is { } a ? TrainingActivities.NameOf(a) : "무훈련";
                Console.WriteLine($"    {label,-7} 승률 {100.0 * wins / total,5:F1}% · 한방컷 {100.0 * oneShot / Math.Max(1, probes),4:F0}% · 평균 {(double)rounds / total:F1}라운드");
            }
        }
    }

    // ── 케이스 4 — 전투 로그 전문: 통계가 아니라 단일 판의 계산 과정 ──
    private static void Case4_전투_로그_전문()
    {
        Console.WriteLine("\n[케이스 4] 전투 로그 전문 — 관전 로그와 같은 출력 (계산 과정 포함)");

        var strong = Rian("C4A", WeaponMaterial.Steel);
        TrainYear(strong, TrainingActivity.Strength, withRest: true, "c4a");
        var foe1 = EncounterGenerator.Generate(1, 1, new DeterministicRandom("c4f1"), _ => "고블린")[0];
        Trace("근력 1년(강철검) vs 난이도 1", strong, foe1, "c4b1");

        var scholar = Rian("C4B", WeaponMaterial.Iron);
        TrainYear(scholar, TrainingActivity.Study, withRest: true, "c4c");
        var foe2 = EncounterGenerator.Generate(2, 1, new DeterministicRandom("c4f2"), _ => "고블린")[0];
        Trace("학술 1년(철검) vs 난이도 2", scholar, foe2, "c4b2");
    }

    private static void Trace(string title, Adventurer player, Adventurer foe, string seed)
    {
        Console.WriteLine($"\n  ═══ {title} (전투 시드 {seed}) ═══");
        Sheet("아군", player);
        Sheet("적", foe);
        var state = CombatantFactory.FormParty([player], [foe]);
        var result = new BattleResolver(recordLog: true, explainAttacks: true)
            .Resolve(state, new DeterministicRandom(seed), onLine: line => Console.WriteLine("  " + line));
        var me = state.All.First(c => c.Team == Team.Player);
        Console.WriteLine($"  결과: {result.Outcome} · {result.Rounds}라운드 · 아군 HP {me.Hp}/{me.MaxHp}");
    }

    private static void Sheet(string side, Adventurer a)
    {
        var c = CombatantFactory.Create(a, Team.Player, Row.Front);
        Console.WriteLine($"  [{side}] {a.Name} · 무장 {a.Loadout} · " +
            $"힘{a.Stats.Strength} 민{a.Stats.Agility} 기{a.Stats.Finesse} 활{a.Stats.Vitality} 지{a.Stats.Intellect} 정{a.Stats.Spirit} · " +
            $"HP {c.MaxHp} · 위력 {c.EffectivePhysicalPower} · 방어 {c.EffectivePhysicalGuard} · 속도 {c.EffectiveSpeed:F1}");
    }
}
