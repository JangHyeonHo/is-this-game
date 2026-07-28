namespace Guildwright.Core.Rng;

/// <summary>
/// 코어의 유일한 난수 공급원.
/// <para>
/// <b>코어 코드에서 <c>System.Random</c>을 직접 쓰지 마세요.</b>
/// 이 게임의 밸런싱은 배치 시뮬레이션(같은 전투를 수천 번 돌려 승률 분포를 보는 것)으로
/// 하므로, 같은 시드 + 같은 입력이면 항상 같은 결과가 나와야 합니다.
/// </para>
/// </summary>
public interface IRandomSource
{
    /// <summary>[0, 1) 범위의 실수.</summary>
    double NextDouble();

    /// <summary>[minInclusive, maxExclusive) 범위의 정수.</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>확률 <paramref name="probability"/>로 참을 반환합니다.</summary>
    bool Chance(double probability);

    /// <summary>평균 0, 표준편차 1인 정규분포 표본.</summary>
    double NextGaussian();

    /// <summary>
    /// 독립적인 하위 난수 스트림을 만듭니다.
    /// <para>
    /// 시스템마다 별도 스트림을 쓰면, 한 시스템의 난수 호출 횟수가 바뀌어도
    /// 다른 시스템의 결과가 흔들리지 않습니다. 밸런스 작업 중에 이게 매우 중요합니다.
    /// 같은 <paramref name="label"/>은 항상 같은 스트림을 만듭니다.
    /// </para>
    /// </summary>
    IRandomSource Fork(string label);
}
