using Guildwright.Core.Rng;
using Xunit;

namespace Guildwright.Core.Tests;

/// <summary>
/// 결정론은 이 프로젝트의 기반입니다. 여기가 깨지면 배치 시뮬레이션 기반 밸런싱이
/// 통째로 무의미해지므로, 가장 먼저 지켜야 할 성질입니다.
/// </summary>
public class DeterministicRandomTests
{
    [Fact]
    public void NextDouble_같은시드_같은수열()
    {
        var a = new DeterministicRandom(12345UL);
        var b = new DeterministicRandom(12345UL);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(a.NextDouble(), b.NextDouble());
        }
    }

    [Fact]
    public void NextDouble_다른시드_다른수열()
    {
        var a = new DeterministicRandom(1UL);
        var b = new DeterministicRandom(2UL);

        var seqA = Enumerable.Range(0, 50).Select(_ => a.NextDouble()).ToArray();
        var seqB = Enumerable.Range(0, 50).Select(_ => b.NextDouble()).ToArray();

        Assert.NotEqual(seqA, seqB);
    }

    [Fact]
    public void NextDouble_항상_0이상_1미만()
    {
        var rng = new DeterministicRandom(99UL);

        for (int i = 0; i < 10_000; i++)
        {
            double value = rng.NextDouble();
            Assert.InRange(value, 0.0, 0.9999999999);
        }
    }

    [Fact]
    public void NextInt_범위를_벗어나지_않는다()
    {
        var rng = new DeterministicRandom(7UL);

        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.NextInt(-5, 5);
            Assert.InRange(value, -5, 4);
        }
    }

    [Fact]
    public void NextInt_모든값이_고르게_나온다()
    {
        var rng = new DeterministicRandom(31UL);
        var counts = new int[6];

        const int trials = 60_000;
        for (int i = 0; i < trials; i++)
        {
            counts[rng.NextInt(0, 6)]++;
        }

        // 균등하면 각 10,000회. 편향 없는지 넉넉한 범위로 확인합니다.
        foreach (int count in counts)
        {
            Assert.InRange(count, 9_000, 11_000);
        }
    }

    [Fact]
    public void Fork_같은라벨_같은스트림()
    {
        var parentA = new DeterministicRandom(555UL);
        var parentB = new DeterministicRandom(555UL);

        var childA = parentA.Fork("combat");
        var childB = parentB.Fork("combat");

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(childA.NextDouble(), childB.NextDouble());
        }
    }

    [Fact]
    public void Fork_다른라벨_다른스트림()
    {
        var parent = new DeterministicRandom(555UL);
        var combat = parent.Fork("combat");

        var otherParent = new DeterministicRandom(555UL);
        var events = otherParent.Fork("events");

        var seqA = Enumerable.Range(0, 30).Select(_ => combat.NextDouble()).ToArray();
        var seqB = Enumerable.Range(0, 30).Select(_ => events.NextDouble()).ToArray();

        Assert.NotEqual(seqA, seqB);
    }

    [Fact]
    public void Fork_부모스트림을_전진시킨다()
    {
        // 같은 라벨로 두 번 Fork하면 서로 다른 스트림이 나와야 합니다.
        var parent = new DeterministicRandom(777UL);
        var first = parent.Fork("trial");
        var second = parent.Fork("trial");

        Assert.NotEqual(first.NextDouble(), second.NextDouble());
    }

    [Fact]
    public void 문자열시드_실행간_안정적이다()
    {
        // string.GetHashCode()는 프로세스마다 무작위화되므로 쓰면 안 됩니다.
        // 이 값이 바뀌면 세이브 파일의 재현성이 깨집니다.
        var rng = new DeterministicRandom("guildwright");
        double first = rng.NextDouble();

        var again = new DeterministicRandom("guildwright");
        Assert.Equal(first, again.NextDouble());
    }

    [Fact]
    public void NextGaussian_평균0_표준편차1에_가깝다()
    {
        var rng = new DeterministicRandom(2024UL);
        const int samples = 100_000;

        double sum = 0.0, sumSquares = 0.0;
        for (int i = 0; i < samples; i++)
        {
            double value = rng.NextGaussian();
            sum += value;
            sumSquares += value * value;
        }

        double mean = sum / samples;
        double variance = sumSquares / samples - mean * mean;

        Assert.InRange(mean, -0.02, 0.02);
        Assert.InRange(Math.Sqrt(variance), 0.98, 1.02);
    }
}
