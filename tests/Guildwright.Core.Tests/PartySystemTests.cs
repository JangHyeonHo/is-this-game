using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Parties;
using Guildwright.Core.Rng;
using Guildwright.Core.Skills;
using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 파티는 <b>두 층</b>입니다 — 가상 파티(= 임시 조합)와 정규 파티.
/// <para>
/// 이 파일이 지키는 것: <b>자격 없이 그냥 같이 나가는 층은 없고</b>, 누적은 자동이며,
/// 등록은 선택이고, 파티 등급은 개인 등급에서 파생되지 않습니다.
/// </para>
/// <para>
/// 한때 이 문서에는 3층(임시 조합 / 가상 파티 / 정규 파티)으로 적혀 있었습니다 —
/// <b>에이전트가 늘린 층</b>이고 폐기되었습니다. 그 층이 다시 생기면 이 파일이 깨집니다.
/// </para>
/// 근거: docs/08-design-revision.md §6
/// </summary>
public class PartySystemTests(ITestOutputHelper output)
{
    private static readonly PrimaryStats Decent = new(
        Strength: 24, Agility: 14, Finesse: 16, Vitality: 26, Intellect: 10, Spirit: 12);

    /// <summary>파견 가능한 모험가 하나. 등록 첫 해는 무조건 훈련이라 한 해를 보냅니다.</summary>
    private static Adventurer Member(string id, JobId job = JobId.SwordApprentice, Rank rank = Rank.F)
    {
        var a = new Adventurer(
            id, id, Decent, 20,
            GrowthProfile.Roll(new DeterministicRandom(StableSeed(id)), 3),
            job: job);

        CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(StableSeed(id) + 1));

        while (a.Rank < rank) a.Promote();
        return a;
    }

    // id에서 시드를 만듭니다. string.GetHashCode는 실행마다 달라지므로 쓰지 않습니다.
    private static ulong StableSeed(string id)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in id)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    /// <summary>그 조합으로 n달 나갑니다.</summary>
    private static void GoOut(PartyLedger ledger, int months, params Adventurer[] members)
    {
        for (int m = 0; m < months; m++) Assert.True(ledger.RecordMonth(members));
    }

    // ---- 층은 둘 ----

    [Fact]
    public void 가상_파티는_객체가_아니라_누적으로만_존재한다()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");

        GoOut(ledger, 3, a, b);

        // 세 달 나갔지만 파티 객체는 아직 없습니다 — 등록해야 생깁니다.
        Assert.Empty(ledger.Parties);
        Assert.Equal(3, ledger.MonthsTogether([a, b]));
        Assert.Null(ledger.RegularPartyOf("A"));
    }

    [Fact]
    public void 자격_없이_그냥_같이_나가는_층은_없다()
    {
        // 3층 구조라면 "제약 없이 같이 나가는" 임시 조합 층이 있어야 합니다. 없습니다 —
        // 조합이 성립하지 않으면 기록 자체가 되지 않습니다.
        var ledger = new PartyLedger();
        var porterA = Member("A", JobId.Porter);
        var porterB = Member("B", JobId.Porter);

        Assert.False(ledger.RecordMonth([porterA, porterB]));
        Assert.Equal(0, ledger.MonthsTogether([porterA, porterB]));
    }

    // ---- 등록 ----

    [Fact]
    public void 여섯_달을_채워야_정규_등록이_된다()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");

        GoOut(ledger, PartyRules.MonthsToRegister - 1, a, b);
        Assert.Equal(RegistrationProblem.NotEnoughMonths, ledger.CheckRegistration([a, b]));
        Assert.Null(ledger.Register("P1", "첫 파티", [a, b]));

        GoOut(ledger, 1, a, b);
        Assert.Equal(RegistrationProblem.None, ledger.CheckRegistration([a, b]));

        var party = ledger.Register("P1", "첫 파티", [a, b]);
        Assert.NotNull(party);
        Assert.Equal(party, ledger.RegularPartyOf("A"));
    }

    [Fact]
    public void 등록은_강제가_아니다()
    {
        // 조건을 채워도 등록 안 한 채로 계속 나갈 수 있습니다. 숙제가 아니라 성취입니다.
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");

        GoOut(ledger, 12, a, b);

        Assert.Empty(ledger.Parties);
        Assert.Equal(RegistrationProblem.None, ledger.CheckRegistration([a, b]));
        Assert.True(ledger.MonthsTogether([a, b]) > PartyRules.MonthsToRegister);
    }

    [Fact]
    public void 누적은_상위_집합으로_세어서_등록이_선택이_된다()
    {
        // A+B+C로 여섯 달 나가면 A+B, B+C, A+C, A+B+C가 모두 조건을 채웁니다.
        // "누구와 등록할까"가 판단이 되는 지점입니다.
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        var c = Member("C");

        GoOut(ledger, PartyRules.MonthsToRegister, a, b, c);

        var options = ledger.RegistrableCompositions([a, b, c]);
        foreach (var option in options) output.WriteLine(option.ToString());

        Assert.Contains(PartyComposition.Of(a, b, c), options);
        Assert.Equal(PartyRules.MonthsToRegister, ledger.MonthsTogether([a, b]));
        Assert.Equal(RegistrationProblem.None, ledger.CheckRegistration([a, b]));
    }

    [Fact]
    public void 한_사람은_정규_파티_하나()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        var c = Member("C");

        GoOut(ledger, PartyRules.MonthsToRegister, a, b, c);
        Assert.NotNull(ledger.Register("P1", "첫 파티", [a, b]));

        // A는 이미 정규 파티가 있으므로 다른 정규 파티를 또 만들 수 없습니다.
        Assert.Equal(RegistrationProblem.AlreadyInRegularParty, ledger.CheckRegistration([a, c]));

        // 다만 조합으로 같이 나가는 것 자체는 됩니다 — 그것이 가상 파티입니다.
        Assert.True(ledger.RecordMonth([a, c]));
    }

    // ---- 등급 ----

    [Fact]
    public void 파티_등급은_개인_등급에서_파생되지_않는다()
    {
        var ledger = new PartyLedger();
        var strong = Member("A", rank: Rank.A);
        var alsoStrong = Member("B", rank: Rank.A);

        GoOut(ledger, PartyRules.MonthsToRegister, strong, alsoStrong);
        var party = ledger.Register("P1", "고수 둘", [strong, alsoStrong])!;

        // A등급 둘이 모였어도 파티는 최하에서 시작합니다.
        Assert.Equal(Ranks.Lowest, party.Rank);
        Assert.Equal(Rank.A, strong.Rank);
    }

    [Fact]
    public void 파티_등급은_평가를_쌓아_오른다()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        GoOut(ledger, PartyRules.MonthsToRegister, a, b);
        var party = ledger.Register("P1", "파티", [a, b])!;

        Assert.Equal(0, ledger.RecordEvaluation(party, PartyRules.EvaluationNeeded(Rank.F) - 1));
        Assert.Equal(Rank.F, party.Rank);

        Assert.Equal(1, ledger.RecordEvaluation(party, 1));
        Assert.Equal(Rank.E, party.Rank);
        output.WriteLine(party.ToString());
    }

    [Fact]
    public void 가입_자격은_등급이_세워진_뒤에만_걸린다()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        var rookie = Member("Z");                 // F등급

        // 가상 파티에는 등급이 없으므로 신입도 그냥 같이 나갑니다.
        Assert.True(ledger.RecordMonth([a, b, rookie]));

        GoOut(ledger, PartyRules.MonthsToRegister, a, b);
        var party = ledger.Register("P1", "파티", [a, b])!;

        // 파티가 B급까지 올라가면 F등급은 못 들어옵니다 ("B + B + F는 안 된다").
        while (party.Rank < Rank.B) ledger.RecordEvaluation(party, PartyRules.EvaluationNeeded(party.Rank));
        Assert.Equal(Rank.D, party.JoinFloor);

        // F등급이라 붙어 나가는 것부터 막힙니다 — 캐리 태우기가 성립하지 않습니다.
        Assert.False(ledger.RecordMonth([a, b, rookie]));
        Assert.Equal(AdmissionProblem.RankTooLow, ledger.CheckAdmission(party, rookie));

        // 승급해서 자격을 채우면 같이 나갈 수 있고, 여섯 달을 채우면 들어옵니다.
        while (rookie.Rank < Rank.D) rookie.Promote();
        Assert.Equal(AdmissionProblem.NotEnoughMonths, ledger.CheckAdmission(party, rookie));

        GoOut(ledger, PartyRules.MonthsToRegister, a, b, rookie);
        Assert.Equal(AdmissionProblem.None, ledger.CheckAdmission(party, rookie));
        Assert.True(ledger.Admit(party, rookie));
    }

    [Fact]
    public void 자격은_들어오는_사람에게만_걸린다()
    {
        // F등급 둘이 모여 B급까지 올린 파티가 있습니다. 자격을 기존 멤버에게도 걸면
        // 이 파티는 성장한 순간 자기 멤버 때문에 못 나가게 됩니다.
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");

        GoOut(ledger, PartyRules.MonthsToRegister, a, b);
        var party = ledger.Register("P1", "F등급 둘", [a, b])!;
        while (party.Rank < Rank.B) ledger.RecordEvaluation(party, PartyRules.EvaluationNeeded(party.Rank));

        Assert.Equal(Rank.F, a.Rank);
        Assert.True(a.Rank < party.JoinFloor);
        Assert.True(ledger.RecordMonth([a, b]));
    }

    // ---- 증원과 해체 ----

    [Fact]
    public void 증원도_그_파티와_여섯_달을_채워야_한다()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        var c = Member("C");

        GoOut(ledger, PartyRules.MonthsToRegister, a, b);
        var party = ledger.Register("P1", "파티", [a, b])!;

        // C와 같이 나간 적이 없습니다.
        Assert.Equal(AdmissionProblem.NotEnoughMonths, ledger.CheckAdmission(party, c));

        GoOut(ledger, PartyRules.MonthsToRegister - 1, a, b, c);
        Assert.Equal(AdmissionProblem.NotEnoughMonths, ledger.CheckAdmission(party, c));

        GoOut(ledger, 1, a, b, c);
        Assert.True(ledger.Admit(party, c));
        Assert.Equal(3, party.Members.Count);
    }

    [Fact]
    public void 남은_멤버가_한_명이면_자동_해체된다()
    {
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        var c = Member("C");

        GoOut(ledger, PartyRules.MonthsToRegister, a, b, c);
        var party = ledger.Register("P1", "셋", [a, b, c])!;

        Assert.True(ledger.Leave("C"));
        Assert.False(party.Disbanded);      // 둘 남았으므로 아직 굴러갑니다.

        Assert.True(ledger.Leave("B"));
        Assert.True(party.Disbanded);       // 한 명 남으면 해체.

        // 해체되면 소속이 사라지므로 다른 파티를 만들 수 있습니다.
        Assert.Null(ledger.RegularPartyOf("A"));
    }

    [Fact]
    public void 유지_조건은_인원뿐이고_활동_조건은_없다()
    {
        // 파티를 만들어두고 안 나가도 해체되지 않습니다 — 규칙을 늘리지 않기로 했습니다.
        var ledger = new PartyLedger();
        var a = Member("A");
        var b = Member("B");
        GoOut(ledger, PartyRules.MonthsToRegister, a, b);
        var party = ledger.Register("P1", "파티", [a, b])!;

        Assert.False(party.Disbanded);
        Assert.Equal(Ranks.Lowest, party.Rank);
    }

    // ---- 짐꾼 규칙 (§16.8b) ----

    [Fact]
    public void 짐꾼은_최대_한_명()
    {
        var fighter = Member("A");
        var porter1 = Member("B", JobId.Porter);
        var porter2 = Member("C", JobId.Porter);

        Assert.Equal(FormationProblem.None, PartyFormation.Check([fighter, porter1]));
        Assert.Equal(FormationProblem.TooManyPorters,
            PartyFormation.Check([fighter, porter1, porter2]));
    }

    [Fact]
    public void 짐꾼만으로는_구성할_수_없다()
    {
        var porter = Member("A", JobId.Porter);
        var fighter = Member("B");

        // 짐꾼 하나만으로도, 짐꾼 여럿으로도 안 됩니다 (여럿은 짐꾼 수에서 먼저 걸립니다).
        Assert.Equal(FormationProblem.NoCombatant, PartyFormation.Check([porter]));
        Assert.Equal(FormationProblem.None, PartyFormation.Check([porter, fighter]));
    }

    [Fact]
    public void 짐꾼은_등급이_높아도_솔로잉할_수_없다()
    {
        // 솔로잉 금지의 근거가 등급이 아니라 직업입니다 — 혼자서는 짐만 들 수 없습니다.
        var porter = Member("A", JobId.Porter, rank: Rank.S);
        Assert.Equal(FormationProblem.NoCombatant, PartyFormation.Check([porter]));
    }

    // ---- 솔로잉 ----

    [Fact]
    public void 솔로잉은_금지가_아니라_등급으로_열린다()
    {
        var rookie = Member("A");                          // F
        var veteran = Member("B", rank: PartyRules.SoloingUnlock);

        Assert.Equal(FormationProblem.SoloingLocked, PartyFormation.Check([rookie]));
        Assert.Equal(FormationProblem.None, PartyFormation.Check([veteran]));
    }

    [Fact]
    public void 혼자서는_정규_등록을_할_수_없다()
    {
        // 솔로잉이 열려도 파티는 최소 2명입니다.
        var ledger = new PartyLedger();
        var veteran = Member("A", rank: Rank.S);

        GoOut(ledger, 12, veteran);
        Assert.Equal(RegistrationProblem.InvalidComposition, ledger.CheckRegistration([veteran]));
    }

    // ---- 등급 눈금 ----

    [Fact]
    public void 등급은_F부터_SS까지_여덟_단계()
    {
        Assert.Equal(8, Ranks.All.Count);
        Assert.Equal(Rank.F, Ranks.Lowest);
        Assert.Equal(Rank.SS, Ranks.Highest);

        // 값의 순서가 곧 높낮이라 비교에 그대로 씁니다.
        Assert.True(Rank.SS > Rank.A);
        Assert.Equal(Rank.D, Rank.B.Below(2));

        // 하한·상한에서 멈춥니다.
        Assert.Equal(Rank.F, Rank.F.Below(3));
        Assert.Equal(Rank.SS, Rank.SS.Above(3));
    }

    [Fact]
    public void 개인_등급은_승급_의뢰로만_오른다()
    {
        var a = Member("A");
        Assert.Equal(Ranks.Lowest, a.Rank);

        // 훈련을 아무리 해도 오르지 않습니다 — 직업과는 다른 축입니다.
        for (int y = 0; y < 5; y++)
        {
            CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom((ulong)y + 100));
        }
        Assert.Equal(Ranks.Lowest, a.Rank);

        Assert.True(a.Promote());
        Assert.Equal(Rank.E, a.Rank);
    }

    // ---- 결정론 ----

    [Fact]
    public void 조합_식별자는_넣은_순서에_흔들리지_않는다()
    {
        var a = Member("A");
        var b = Member("B");
        var c = Member("C");

        Assert.Equal(PartyComposition.Of(a, b, c), PartyComposition.Of(c, a, b));
        Assert.Equal("A|B|C", PartyComposition.Of(c, b, a).Key);
    }

    [Fact]
    public void 같은_기록을_두_번_돌리면_같은_장부가_나온다()
    {
        static string Run()
        {
            var ledger = new PartyLedger();
            var a = Member("A");
            var b = Member("B");
            var c = Member("C");

            GoOut(ledger, 4, a, b, c);
            GoOut(ledger, 4, a, b);
            GoOut(ledger, 2, b, c);

            var party = ledger.Register("P1", "파티", [a, b])!;
            ledger.RecordEvaluation(party, 100);

            var lines = ledger.RegistrableCompositions([a, b, c]).Select(x => x.Key).ToList();
            lines.Add(party.ToString());
            lines.Add($"{ledger.MonthsTogether([a, b])}/{ledger.MonthsTogether([b, c])}");
            return string.Join("\n", lines);
        }

        string first = Run();
        Assert.Equal(first, Run());
        output.WriteLine(first);
    }
}
