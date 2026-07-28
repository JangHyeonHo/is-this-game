namespace Guildwright.Core.Rng;

/// <summary>
/// xoshiro256** 기반 결정론적 난수 생성기.
/// <para>
/// <c>System.Random</c>을 쓰지 않는 이유: .NET 버전에 따라 내부 알고리즘이 바뀔 수 있어
/// "같은 시드 → 같은 결과"가 런타임 간에 보장되지 않습니다. 세이브 파일에 시드를 저장하고
/// 나중에 재현해야 하는 게임에서는 이게 치명적입니다.
/// </para>
/// </summary>
public sealed class DeterministicRandom : IRandomSource
{
    private ulong _s0, _s1, _s2, _s3;

    // NextGaussian은 Box-Muller로 한 번에 두 개를 만들므로 하나를 캐시해 둡니다.
    private double _spareGaussian;
    private bool _hasSpareGaussian;

    public DeterministicRandom(ulong seed)
    {
        // SplitMix64로 시드를 흩뿌립니다. 시드가 0이거나 작은 값이어도 초기 출력이 편향되지 않게 합니다.
        ulong z = seed;
        _s0 = SplitMix64(ref z);
        _s1 = SplitMix64(ref z);
        _s2 = SplitMix64(ref z);
        _s3 = SplitMix64(ref z);
    }

    /// <summary>문자열 시드로 생성합니다. 같은 문자열은 항상 같은 스트림을 만듭니다.</summary>
    public DeterministicRandom(string seed) : this(StableHash(seed)) { }

    public double NextDouble()
    {
        // 상위 53비트를 써서 [0, 1) 범위의 double을 만듭니다.
        return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                $"maxExclusive({maxExclusive})는 minInclusive({minInclusive})보다 커야 합니다.");
        }

        ulong range = (ulong)((long)maxExclusive - minInclusive);

        // 나머지 연산의 모듈로 편향을 제거합니다.
        ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value >= limit);

        return (int)((long)minInclusive + (long)(value % range));
    }

    public bool Chance(double probability)
    {
        if (probability <= 0.0) return false;
        if (probability >= 1.0) return true;
        return NextDouble() < probability;
    }

    public double NextGaussian()
    {
        if (_hasSpareGaussian)
        {
            _hasSpareGaussian = false;
            return _spareGaussian;
        }

        // Box-Muller 변환. u가 0이면 Log가 발산하므로 배제합니다.
        double u, v, s;
        do
        {
            u = NextDouble() * 2.0 - 1.0;
            v = NextDouble() * 2.0 - 1.0;
            s = u * u + v * v;
        }
        while (s >= 1.0 || s == 0.0);

        double factor = Math.Sqrt(-2.0 * Math.Log(s) / s);
        _spareGaussian = v * factor;
        _hasSpareGaussian = true;
        return u * factor;
    }

    public IRandomSource Fork(string label)
    {
        // 현재 상태와 라벨을 섞어 하위 스트림 시드를 만듭니다.
        // 부모 스트림도 한 칸 전진시켜, 같은 라벨로 두 번 Fork해도 서로 다른 스트림이 나오게 합니다.
        ulong mixed = NextUInt64() ^ StableHash(label);
        return new DeterministicRandom(mixed);
    }

    private ulong NextUInt64()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// 문자열의 안정적인 64비트 해시 (FNV-1a).
    /// <para>
    /// <c>string.GetHashCode()</c>를 쓰면 안 됩니다 — .NET은 해시 DoS 방어를 위해
    /// 프로세스마다 문자열 해시를 무작위화하므로, 실행할 때마다 값이 달라집니다.
    /// </para>
    /// </summary>
    internal static ulong StableHash(string text)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;
        foreach (char c in text)
        {
            hash ^= (byte)(c & 0xFF);
            hash *= prime;
            hash ^= (byte)(c >> 8);
            hash *= prime;
        }
        return hash;
    }
}
