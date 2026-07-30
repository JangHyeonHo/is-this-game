using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Parties;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Guildwright.Core.Weapons;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 의뢰는 <b>형태 4종</b>이고 파견은 <b>달 단위</b>입니다.
/// <para>
/// 이 파일이 지키는 것:
/// <b>기간 고정 · 조기 종료 없음 · 성공/실패 이분법 · 자원은 파견 단위 · 보급은 짐 한도 안에서만.</b>
/// </para>
/// <para>
/// 예전에는 <c>FieldYearSession</c>이 <b>12개월 = 의뢰 1건</b>이었습니다. 그러면 1달 의뢰도
/// 1년 의뢰도 없고, 한 사람이 한 해에 한 건만 하게 되며, <b>달력 잠금이라는 기회비용이
/// 성립하지 않습니다</b> — 과잉 전력을 보내는 걸 막는 유일한 브레이크가 그것인데요.
/// </para>
/// 근거: docs/08-design-revision.md §17
/// </summary>
public class ContractAndDeploymentTests(ITestOutputHelper output)
{
    private static readonly PrimaryStats Sturdy = new(
        Strength: 34, Agility: 18, Finesse: 20, Vitality: 40, Intellect: 14, Spirit: 18);

    private static Adventurer Fighter(string id, JobId job = JobId.SwordApprentice, ulong seed = 11)
    {
        var a = new Adventurer(id, id, Sturdy, 40,
            GrowthProfile.Roll(new DeterministicRandom(seed), 3), job: job);

        // 등록 첫 해는 무조건 훈련입니다.
        CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(seed + 1));
        return a;
    }

    private static Contract Make(
        ContractForm form,
        int months = 3,
        int difficulty = 1,
        int intensity = 3,
        ContractSource source = ContractSource.Village) =>
        new("T1", $"시험 {form}", form, source, difficulty, months, intensity, Objective: "지킬 것");

    private static DeploymentResult Run(
        Contract contract, IReadOnlyList<Adventurer> party, ulong seed = 7,
        Supplies? supplies = null)
    {
        var session = new DeploymentSession(party, contract, new DeterministicRandom(seed), supplies);
        while (!session.IsComplete) session.AdvanceMonth();
        return session.Complete();
    }

    // ---- 형태 4종 ----

    [Fact]
    public void 의뢰_형태는_넷이고_소재_분류가_아니다()
    {
        // 예전 ContractKind 3종(Combat/Gathering/Exploration)은 소재 분류라
        // 진행 규칙과 대응하지 않았습니다. 형태는 완료 판정으로 가릅니다.
        Assert.Equal(4, Enum.GetValues<ContractForm>().Length);

        Assert.True(Make(ContractForm.Defend).HasWard);
        Assert.False(Make(ContractForm.Subjugate).HasWard);
        Assert.True(Make(ContractForm.Discover).CanComeUpEmpty);
        Assert.False(Make(ContractForm.Gather).CanComeUpEmpty);
    }

    [Fact]
    public void 전투_없음이라는_형태는_없다()
    {
        // 마물이 언제든 올 수 있는 세계라 밭에 있든 성벽에 있든 싸울 수 있습니다.
        foreach (var form in Enum.GetValues<ContractForm>())
        {
            Assert.True(DeploymentRules.EncounterChanceOf(form) > 0.0,
                $"{form}에 조우가 없으면 '전투 없음' 형태가 생깁니다");
            Assert.True(Make(form).CombatWeight > 0.0);
        }
    }

    [Fact]
    public void 길드가_자기_돈으로_하는_일은_명성으로_돌아온다()
    {
        Assert.Equal(RewardKind.Renown, Make(ContractForm.Discover, source: ContractSource.Guild).Reward);
        Assert.Equal(RewardKind.Pay, Make(ContractForm.Subjugate, source: ContractSource.Realm).Reward);
        Assert.Equal(RewardKind.Pay, Make(ContractForm.Gather, source: ContractSource.Village).Reward);
    }

    // ---- 기간과 판정 ----

    [Fact]
    public void 기간이_고정이고_조기_종료가_없다()
    {
        // 토벌은 한 번의 교전으로 강도를 넘길 수 있습니다. 그래도 3달을 채웁니다.
        var contract = Make(ContractForm.Subjugate, months: 3, intensity: 1);
        var session = new DeploymentSession(
            [Fighter("A"), Fighter("B", seed: 21)], contract, new DeterministicRandom(5));

        session.AdvanceMonth();
        Assert.True(session.Progress >= contract.Intensity, "첫 달 교전으로 강도를 넘겨야 하는 시드입니다");
        Assert.False(session.IsComplete);      // 채웠는데도 안 끝납니다.

        session.AdvanceMonth();
        session.AdvanceMonth();
        Assert.True(session.IsComplete);
        Assert.Equal(3, session.Complete().MonthsSpent);
    }

    [Fact]
    public void 성공과_실패의_이분법이고_부분_성공이_없다()
    {
        var result = Run(Make(ContractForm.Gather, months: 2), [Fighter("A"), Fighter("B", seed: 21)]);

        // 성공이면 실패 사유가 없고, 실패면 사유가 하나 있습니다. 그 사이가 없습니다.
        Assert.Equal(result.Succeeded, result.Failure == DeploymentFailure.None);
        Assert.Equal(result.Succeeded, result.Paid);
        output.WriteLine(result.ToString());
    }

    [Fact]
    public void 중도_이탈은_손절이지만_실패다()
    {
        var contract = Make(ContractForm.Gather, months: 6);
        var session = new DeploymentSession([Fighter("A"), Fighter("B", seed: 21)], contract,
            new DeterministicRandom(9));

        session.AdvanceMonth();
        session.Abandon();

        var result = session.Complete();
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentFailure.Abandoned, result.Failure);
        Assert.Equal(1, result.MonthsSpent);       // 칸이 풀립니다 — 구속이 아니라 예약입니다.
        Assert.False(result.Paid);
    }

    [Fact]
    public void 지킴형은_아무_일_없이_끝나는_게_성공이다()
    {
        // 습격이 한 번도 안 오면 진척이 0입니다. 다른 형태라면 미달이지만
        // 지킴은 그게 성공입니다 — 그래서 진척을 따지지 않습니다.
        var contract = Make(ContractForm.Defend, months: 2, intensity: 5);
        var session = new DeploymentSession([Fighter("A"), Fighter("B", seed: 21)], contract,
            new DeterministicRandom(3));

        // 조우 확률을 우회할 수 없으니, 진척이 0으로 끝난 시드를 찾습니다.
        DeploymentResult? quiet = null;
        for (ulong seed = 0; seed < 200 && quiet is null; seed++)
        {
            var candidate = Run(contract, [Fighter("A"), Fighter("B", seed: 21)], seed);
            if (candidate.Progress == 0) quiet = candidate;
        }

        Assert.NotNull(quiet);
        Assert.True(quiet.Progress < contract.Intensity);
        Assert.True(quiet.Succeeded, "아무 일 없이 끝났는데 실패라면 지킴형의 뜻이 사라집니다");
    }

    [Fact]
    public void 발견형은_못_찾으면_실패다()
    {
        var contract = Make(ContractForm.Discover, months: 1, intensity: 1);

        // 1달짜리라 못 찾고 끝나는 경우가 반드시 있습니다.
        var failures = new List<DeploymentFailure>();
        for (ulong seed = 0; seed < 60; seed++)
        {
            failures.Add(Run(contract, [Fighter("A"), Fighter("B", seed: 21)], seed).Failure);
        }

        Assert.Contains(DeploymentFailure.NotFound, failures);
        Assert.Contains(DeploymentFailure.None, failures);
    }

    [Fact]
    public void 수집은_기간을_전부_일해야_강도에_닿는다()
    {
        // 휴식 회복이 후해도 공짜가 아닌 이유입니다 — 쉰 달만큼 진척이 없습니다.
        // 다 일했으면 정확히 닿고, 한 달이라도 쉬면 못 닿아야 합니다.
        var contract = Make(ContractForm.Gather, months: 5, intensity: 12);

        var session = new DeploymentSession(
            [Fighter("A"), Fighter("B", seed: 21)], contract, new DeterministicRandom(5));

        while (!session.IsComplete) session.AdvanceMonth();

        int rested = session.Months.Count(m => m.Work == MonthWork.Rest);
        output.WriteLine($"쉰 달 {rested} · 진척 {session.Progress}/{contract.Intensity}");

        if (rested == 0)
        {
            // 나머지를 버리지 않아야 12가 정확히 나옵니다 (12/5는 나누어지지 않습니다).
            Assert.Equal(contract.Intensity, session.Progress);
            Assert.True(session.Complete().Succeeded);
        }
        else
        {
            Assert.True(session.Progress < contract.Intensity);
            Assert.Equal(DeploymentFailure.Unfinished, session.Complete().Failure);
        }
    }

    [Fact]
    public void 쉬면_진척_미달로_실패한다()
    {
        // 위 테스트의 else 가지를 확실히 밟습니다 — 다칠 만한 난이도로 길게 보냅니다.
        var contract = Make(ContractForm.Gather, months: 8, intensity: 16, difficulty: 5);

        DeploymentResult? withRest = null;
        for (ulong seed = 0; seed < 200 && withRest is null; seed++)
        {
            var session = new DeploymentSession(
                [Fighter("A"), Fighter("B", seed: 21)], contract, new DeterministicRandom(seed));
            while (!session.IsComplete) session.AdvanceMonth();

            var result = session.Complete();

            // 전멸·후퇴가 아니라 "쉬어서 못 채운" 경우만 봅니다.
            if (session.Months.Any(m => m.Work == MonthWork.Rest)
                && result.Failure is DeploymentFailure.Unfinished or DeploymentFailure.None)
            {
                withRest = result;
            }
        }

        Assert.NotNull(withRest);
        Assert.Equal(DeploymentFailure.Unfinished, withRest.Failure);
        output.WriteLine(withRest.ToString());
    }

    // ---- 자원은 파견 단위 ----

    [Fact]
    public void 마나는_전투마다_채워지지_않는다()
    {
        // 예전에는 Combatant가 언제나 만땅으로 시작했습니다. 그러면 마나가
        // 아무것도 제약하지 않아 자원이 아니게 됩니다.
        var mage = Fighter("M", JobId.StaffApprentice);
        var guard = Fighter("G", JobId.ShieldApprentice, seed: 31);

        var session = new DeploymentSession(
            [mage, guard], Make(ContractForm.Subjugate, months: 6, difficulty: 2),
            new DeterministicRandom(17));

        int start = session.Mana[mage.Id];
        while (!session.IsComplete) session.AdvanceMonth();

        // 자연회복이 있으므로 최대치를 넘지는 않아야 합니다.
        Assert.All(session.Party, a =>
            Assert.True(session.Mana[a.Id] <= DerivedStats.MaxMana(a.Stats, a.Bonuses)));

        output.WriteLine($"마나 {start} → {session.Mana[mage.Id]}");
    }

    [Fact]
    public void HP는_파견_내내_이어지고_자연회복이_있다()
    {
        var a = Fighter("A");
        var b = Fighter("B", seed: 21);
        var session = new DeploymentSession(
            [a, b], Make(ContractForm.Subjugate, months: 12, difficulty: 2),
            new DeterministicRandom(23));

        var seen = new List<double>();
        while (!session.IsComplete)
        {
            session.AdvanceMonth();
            seen.Add(session.HealthRatio);
        }

        output.WriteLine(string.Join(" ", seen.Select(r => $"{r:P0}")));

        // 최대치를 넘지 않고, 파견 중에는 절대 0 미만이 되지 않습니다.
        Assert.All(seen, r => Assert.InRange(r, 0.0, 1.0));
    }

    [Fact]
    public void 상태가_나쁘면_모험가가_스스로_쉰다()
    {
        // 일할지 쉴지는 플레이어가 고르는 게 아닙니다 — 생존이 최우선입니다 (§17.5).
        var a = Fighter("A");
        var b = Fighter("B", seed: 21);

        var session = new DeploymentSession(
            [a, b], Make(ContractForm.Subjugate, months: 12, difficulty: 4),
            new DeterministicRandom(41));

        while (!session.IsComplete) session.AdvanceMonth();

        // 난이도 4를 1년 굴리면 어딘가에서 반드시 쉬는 달이 나옵니다.
        output.WriteLine(string.Join("\n", session.Months.Select(m => m.Note)));
        Assert.All(session.Months.Where(m => m.Work == MonthWork.Rest),
            m => Assert.Equal(0, m.Progress));
    }

    // ---- 보급 ----

    [Fact]
    public void 보급은_짐_한도_안에서만_가능하다()
    {
        var fighter = Fighter("A");
        var mate = Fighter("B", seed: 21);
        IReadOnlyList<Adventurer> plain = [fighter, mate];

        int capacity = Supplies.CapacityOf(plain);
        Assert.True(new Supplies(capacity + 1).ExceedsCapacityOf(plain));

        Assert.Throws<ArgumentException>(() => new DeploymentSession(
            plain, Make(ContractForm.Gather), new DeterministicRandom(1),
            new Supplies(capacity + 1)));
    }

    [Fact]
    public void 가방을_든_사람이_있으면_더_보낼_수_있다()
    {
        var fighter = Fighter("A");
        var porter = Fighter("P", JobId.Porter, seed: 51);

        Assert.True(porter.Loadout.CarryingPack);

        int without = Supplies.CapacityOf([fighter, Fighter("B", seed: 21)]);
        int with = Supplies.CapacityOf([fighter, porter]);

        output.WriteLine($"짐꾼 없이 {without} · 짐꾼과 {with}");
        Assert.True(with > without, "가방이 짐 한도를 늘리지 않으면 짐꾼을 데려갈 이유가 없습니다");
    }

    [Fact]
    public void 보급은_가방을_든_사람에게_먼저_실린다()
    {
        var fighter = Fighter("A");
        var porter = Fighter("P", JobId.Porter, seed: 51);
        IReadOnlyList<Adventurer> party = [fighter, porter];

        var share = Supplies.UpTo(party, Supplies.CapacityOf(party)).DistributeAmong(party);

        output.WriteLine(string.Join(" · ", share.Select(kv => $"{kv.Key} {kv.Value}")));
        Assert.True(share[porter.Id] > share[fighter.Id]);
    }

    // ---- 게시판 ----

    [Fact]
    public void 감당_못_할_의뢰는_아예_뜨지_않는다()
    {
        // 그래서 랭크 상승의 체감이 "새로운 게 보이기 시작한다"로 옵니다.
        var low = ContractBoard.Post(new DeterministicRandom(2), 4, Rank.F);
        var high = ContractBoard.Post(new DeterministicRandom(2), 4, Rank.A);

        int lowCap = ContractBoard.MaxDifficultyAt(Rank.F);
        Assert.All(low, c => Assert.True(c.Difficulty <= lowCap));

        output.WriteLine($"F 최고 난이도 {low.Max(c => c.Difficulty)} · A 최고 {high.Max(c => c.Difficulty)}");
        Assert.True(high.Max(c => c.Difficulty) > low.Max(c => c.Difficulty));
    }

    [Fact]
    public void 랭크가_오르면_양도_늘어난다()
    {
        // 랜덤이라 한 번으로는 못 잡습니다. 여러 시드의 평균을 봅니다.
        double Average(Rank rank) =>
            Enumerable.Range(0, 40)
                .Average(s => ContractBoard.Post(new DeterministicRandom((ulong)s), 1, rank).Count);

        double f = Average(Rank.F);
        double a = Average(Rank.A);

        output.WriteLine($"F {f:F1}건 · A {a:F1}건");
        Assert.True(a > f);
    }

    [Fact]
    public void 계절은_강제가_아니라_가중치일_뿐이다()
    {
        // 어떤 계절에도 네 형태가 다 나올 수 있어야 합니다. 한 형태라도 0이면
        // "이 계절엔 이것만"이 되어 플레이어가 계절표를 외웁니다.
        foreach (var season in Enum.GetValues<Season>())
        {
            var weights = ContractBoard.WeightsIn(season);
            Assert.All(Enum.GetValues<ContractForm>(),
                form => Assert.True(weights[form] > 0.0, $"{season}에 {form}이 0입니다"));
        }
    }

    [Fact]
    public void 지속_의뢰만_다음_달로_넘어간다()
    {
        var previous = ContractBoard.Post(new DeterministicRandom(3), 5, Rank.C);
        var promotion = ContractBoard.Promotion(Rank.D);

        var next = ContractBoard.Post(
            new DeterministicRandom(4), 6, Rank.C, [.. previous, promotion]);

        // 승급 의뢰는 남습니다 — 준비 안 된 달에 떠서 놓치는 일이 없어야 합니다.
        Assert.Contains(promotion, next);

        // 나머지는 사라집니다.
        Assert.DoesNotContain(next, c => previous.Contains(c) && !c.Persists);
    }

    [Fact]
    public void 승급_의뢰는_지속_의뢰다()
    {
        var solo = ContractBoard.Promotion(Rank.C);
        var party = ContractBoard.Promotion(Rank.C, partyOnly: true);

        Assert.True(solo.Persists);
        Assert.True(party.Persists);
        Assert.True(party.PartyOnly);
        Assert.False(solo.PartyOnly);

        // 올라가려는 등급의 한 단 아래여야 받을 수 있습니다.
        Assert.True(solo.IsOpenTo(Rank.D));
        Assert.False(solo.IsOpenTo(Rank.E));
    }

    [Fact]
    public void 파티_전용_의뢰는_개인이_못_받는다()
    {
        var board = new[]
        {
            Make(ContractForm.Subjugate) with { Id = "solo" },
            Make(ContractForm.Subjugate) with { Id = "party", Name = "레이드", PartyOnly = true }
        };

        var alone = ContractBoard.AvailableTo(board, Rank.C, asRegularParty: false, maxDifficulty: 10);
        var together = ContractBoard.AvailableTo(board, Rank.C, asRegularParty: true, maxDifficulty: 10);

        Assert.DoesNotContain(together.Except(alone), c => !c.PartyOnly);
        Assert.Single(alone);
        Assert.Equal(2, together.Count);
    }

    // ---- 기간이 성장·보수·숙련에 반영된다 ----

    [Fact]
    public void 한_달_의뢰가_한_해치_성장을_주지_않는다()
    {
        // 이게 없으면 1달 의뢰만 반복하는 것이 최적해가 됩니다.
        var shortRun = Fighter("S");
        var longRun = Fighter("L");

        var one = CareerSimulator.ResolveDeploymentYear(
            shortRun, 3, new DeterministicRandom(77), contract: Make(ContractForm.Subjugate), months: 1);
        var twelve = CareerSimulator.ResolveDeploymentYear(
            longRun, 3, new DeterministicRandom(77), contract: Make(ContractForm.Subjugate), months: 12);

        output.WriteLine($"1달 보수 {one.Income} · 12달 보수 {twelve.Income}");
        Assert.True(twelve.Income > one.Income);
        Assert.True(twelve.StatChange.Total > one.StatChange.Total);
        Assert.True(longRun.Proficiency[WeaponKind.Sword] > shortRun.Proficiency[WeaponKind.Sword]);
    }

    [Fact]
    public void 나이는_열두_달마다_오른다()
    {
        var a = Fighter("A");            // 훈련 1년 = 12달
        int age = a.Age;

        // 1달 의뢰를 열한 번 해도 아직 한 살을 안 먹습니다.
        for (int i = 0; i < 11; i++)
        {
            CareerSimulator.ResolveDeploymentYear(
                a, 1, new DeterministicRandom((ulong)i + 300),
                contract: Make(ContractForm.Gather), months: 1);
        }

        Assert.Equal(age, a.Age);
        Assert.Equal(23, a.MonthsElapsed);

        CareerSimulator.ResolveDeploymentYear(
            a, 1, new DeterministicRandom(400), contract: Make(ContractForm.Gather), months: 1);

        Assert.Equal(age + 1, a.Age);
        Assert.Equal(2, a.CompletedYears);
    }

    // ---- 결정론 ----

    [Fact]
    public void 같은_시드로_두_번_돌리면_같은_파견이_나온다()
    {
        static string Run1(ulong seed)
        {
            var party = new[] { Fighter("A"), Fighter("B", seed: 21), Fighter("P", JobId.Porter, seed: 51) };
            var contract = new Contract("D1", "재현성 시험", ContractForm.Subjugate,
                ContractSource.Realm, Difficulty: 3, Months: 8, Intensity: 20);

            var session = new DeploymentSession(party, contract, new DeterministicRandom(seed));
            while (!session.IsComplete) session.AdvanceMonth();

            var result = session.Complete();
            return string.Join("\n", session.Months.Select(m => m.Note)) + "\n" + result;
        }

        string first = Run1(1234);
        Assert.Equal(first, Run1(1234));
        output.WriteLine(first);
    }

    [Fact]
    public void 게시판도_같은_시드면_같다()
    {
        static string Board(ulong seed) =>
            string.Join("\n", ContractBoard.Post(new DeterministicRandom(seed), 7, Rank.B)
                .Select(c => c.ToString()));

        Assert.Equal(Board(555), Board(555));
    }

    [Fact]
    public void 짐꾼은_적_머릿수_계산에_들어가지_않는다()
    {
        // §16.8b — 짐꾼은 비전투 요원입니다. 머릿수에 넣으면 짐꾼을 데려갈수록 적만 늘어나,
        // "데려갈 이유"가 설계와 반대로 뒤집힙니다.
        var rng = new DeterministicRandom(7);
        var seed = Adventurer.Recruit("S", "씨앗", rng.Fork("s"));
        var fighter = new Adventurer("F", "전사", PrimaryStats.Uniform(10), 10,
            seed.Growth, job: JobId.SwordApprentice);
        var porter = new Adventurer("P", "짐꾼", PrimaryStats.Uniform(10), 10,
            seed.Growth, job: JobId.Porter);

        Assert.Equal(1, DeploymentSession.Combatants([fighter, porter]));
        Assert.Equal(2, DeploymentSession.Combatants([fighter, fighter]));
        Assert.Equal(1, DeploymentSession.Combatants([porter])); // 최소 1 — 0으로 나누기 방지
    }
}
