using Guildwright.Core.Adventurers;
using Guildwright.Core.Careers;
using Guildwright.Core.Combat;
using Guildwright.Core.Rng;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 부동소수점 계산이 <b>실행 환경이 바뀌어도 비트 단위로 같은지</b> 감시합니다.
///
/// <para>
/// <b>왜 필요한가.</b> <c>double</c>의 사칙연산과 <see cref="Math.Sqrt"/>는 IEEE-754가
/// 결과를 비트 단위로 규정하므로 어디서든 같은 값이 나옵니다. 하지만
/// <see cref="Math.Exp"/> · <see cref="Math.Pow"/> · <see cref="Math.Log"/>는 그렇지 않습니다 —
/// 플랫폼 수학 라이브러리 구현에 따라 <b>마지막 자리가 달라질 수 있습니다.</b>
/// </para>
///
/// <para>
/// 이 게임의 밸런스는 전부 배치 시뮬레이션으로 잽니다. docs/06-balance-log.md에 적힌
/// "승률 54%" 같은 수치가 <b>런타임을 올리는 것만으로 조용히 재현 불가능해지면</b>,
/// 실패한 조정을 기록해둔 것조차 다시 확인할 수 없게 됩니다.
/// 나중에 재생 검증(결투장 기록 등)을 붙이려 해도 같은 문제가 발목을 잡습니다.
/// </para>
///
/// <para>
/// 기존 재현성 테스트는 "같은 시드로 두 번 돌려 같은가"를 봅니다. 그건 한 실행 안에서만
/// 참이라 <b>런타임이 계산을 바꾸는 상황은 잡지 못합니다.</b> 그래서 값을 리터럴로 박아둡니다.
/// </para>
///
/// <para>
/// <b>이 테스트가 깨졌다면</b> — 코드를 안 고쳤는데 깨졌다면 실행 환경이 계산을 바꾼 것입니다.
/// 그때는 (1) 기록된 밸런스 수치를 다시 측정하거나 (2) 해당 함수를 자체 구현으로 대체해야 합니다.
/// 수식을 의도적으로 고쳐서 깨진 것이라면, 값을 갱신하고 <b>왜 바뀌었는지</b>를
/// docs/06-balance-log.md에 남기세요. 아무 생각 없이 숫자만 갈아끼우면 이 테스트는 무의미해집니다.
/// </para>
/// </summary>
public class FloatingPointStabilityTests
{
    /// <summary>비트 단위로 비교합니다. 0.0000001 차이도 재생을 어긋나게 하므로 오차 허용은 무의미합니다.</summary>
    private static void PinnedBits(long expected, double actual, string what)
    {
        long bits = BitConverter.DoubleToInt64Bits(actual);
        Assert.True(expected == bits,
            $"{what}이(가) 달라졌습니다.\n" +
            $"  기대: 0x{expected:X16}\n" +
            $"  실제: 0x{bits:X16}  ({actual:R})\n" +
            $"  코드를 안 고쳤는데 이게 떴다면 실행 환경이 계산을 바꾼 것입니다. 클래스 주석을 읽으세요.");
    }

    // ── 초월함수 자체 ────────────────────────────────────────
    //
    // 게임에서 실제로 쓰는 인자 근처의 값으로 고정합니다.

    [Fact]
    public void Math_Exp가_비트_단위로_고정되어_있다()
    {
        // GrowthProfile.BloomFactorAt, Appraiser.ComputeConfidence에서 씁니다.
        PinnedBits(0x3FCE53A61786F313L, Math.Exp(-1.44), "Math.Exp(-1.44)");
        PinnedBits(0x3FAF227F1C7C6D86L, Math.Exp(-2.8), "Math.Exp(-2.8)");
    }

    [Fact]
    public void Math_Pow가_비트_단위로_고정되어_있다()
    {
        // CareerSimulator의 사고 위험(지수 2.2), TacticalBrain의 위험도 판단(지수 3.0).
        PinnedBits(0x4009B563A7B7DAE1L, Math.Pow(1.7, 2.2), "Math.Pow(1.7, 2.2)");
        PinnedBits(0x3FD000C521DDA05AL, Math.Pow(0.63, 3.0), "Math.Pow(0.63, 3.0)");
    }

    [Fact]
    public void Math_Log와_Sqrt가_비트_단위로_고정되어_있다()
    {
        // DeterministicRandom.NextGaussian의 Box-Muller 변환에서 씁니다.
        // Sqrt는 IEEE-754가 정확히 규정하므로 원래 안전하지만, 같이 봐서 손해 볼 게 없습니다.
        PinnedBits(unchecked((long)0xBFEFD0EA24BF89B7UL), Math.Log(0.37), "Math.Log(0.37)");
        PinnedBits(0x3FF6A09E667F3BCDL, Math.Sqrt(2.0), "Math.Sqrt(2.0)");
    }

    // ── 게임 수식 ────────────────────────────────────────────
    //
    // 초월함수를 직접 고정해도, 그걸 감싼 수식에서 연산 순서가 바뀌면 결과가 달라집니다.
    // 그래서 실제로 쓰는 함수의 출력도 함께 박아둡니다.

    [Fact]
    public void BloomFactorAt이_비트_단위로_고정되어_있다()
    {
        var growth = new GrowthProfile
        {
            PeakAge = 24,
            BloomWidth = 3.5,
            Temperament = Temperament.Balanced,
            Potential = new PrimaryStats(70, 65, 60, 75, 55, 50),
            DeclineAge = 31
        };

        PinnedBits(0x3FD4D5FFFD4D3F6CL, growth.BloomFactorAt(17), "개화 배율(17세)");
        PinnedBits(0x3FF0000000000000L, growth.BloomFactorAt(24), "개화 배율(24세, 정점)");
        PinnedBits(0x3FD9909DEB25C7FBL, growth.BloomFactorAt(30), "개화 배율(30세)");
    }

    [Fact]
    public void ComputeConfidence가_비트_단위로_고정되어_있다()
    {
        PinnedBits(0x0000000000000000L, Appraiser.ComputeConfidence(0, 0.0), "확신도(0년, 역량 0)");
        PinnedBits(0x3FE2897D7E607F76L, Appraiser.ComputeConfidence(3, 0.4), "확신도(3년, 역량 0.4)");
        PinnedBits(0x3FED8D6D27407436L, Appraiser.ComputeConfidence(7, 0.9), "확신도(7년, 역량 0.9)");
    }

    // ── 난수원 ───────────────────────────────────────────────

    [Fact]
    public void DeterministicRandom_출력이_비트_단위로_고정되어_있다()
    {
        // 난수원이 흔들리면 그 위의 모든 것이 흔들립니다. 여기가 가장 중요합니다.
        var rng = new DeterministicRandom(20260728UL);

        PinnedBits(0x3F90D55C3BBE3D20L, rng.NextDouble(), "NextDouble 1번째");
        PinnedBits(0x3FE83403A1D626A8L, rng.NextDouble(), "NextDouble 2번째");
        PinnedBits(0x3FD5CBB78B71997EL, rng.NextDouble(), "NextDouble 3번째");

        // Box-Muller라 Log/Sqrt를 타므로 별도로 봅니다.
        PinnedBits(0x3FD577EA6C5E93B6L, rng.NextGaussian(), "NextGaussian");
    }

    [Fact]
    public void 문자열_시드_해시가_고정되어_있다()
    {
        // FNV-1a. string.GetHashCode()는 실행마다 값이 달라져 절대 쓰면 안 됩니다.
        // 이게 흔들리면 Fork로 갈라놓은 모든 스트림이 통째로 어긋납니다.
        PinnedBits(0x3FABF90CC80C7150L, new DeterministicRandom("train:1:A0").NextDouble(), "문자열 시드 스트림");
    }

    // ── 통합 ─────────────────────────────────────────────────
    //
    // 개별 함수가 다 맞아도 조합에서 어긋날 수 있습니다. 실제 시뮬레이션 결과를 박아둡니다.

    [Fact]
    public void 같은_시드의_전투가_항상_같은_결과로_끝난다()
    {
        var result = new BattleResolver(recordLog: true)
            .Resolve(TestParty.MirrorMatch(70, 70), new DeterministicRandom(4242));

        Assert.Equal(BattleOutcome.EnemyVictory, result.Outcome);
        Assert.Equal(29, result.Rounds);
        Assert.Equal(122, result.Log.Count);
    }

    [Fact]
    public void 같은_시드의_훈련_1년이_항상_같은_능력치를_낸다()
    {
        var a = Adventurer.Recruit("P", "핀", new DeterministicRandom(31));
        CareerSimulator.ResolveTrainingYear(a, new DeterministicRandom(32));

        // 2026-07 개정으로 두 번 바뀌었습니다. 근거: docs/06-balance-log.md #29, #30
        //   활동 기반 전환      (16,15,17,24,23,23) → (16,13,14,25,22,21)
        //   활동별 피로 + 모의전 (16,13,14,25,22,21) → (16,14,15,25,19,20)
        Assert.Equal(new PrimaryStats(16, 14, 15, 25, 19, 20), a.Stats);
    }
}
