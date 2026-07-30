using Guildwright.Core.Adventurers;
using Guildwright.Core.Rng;

namespace Guildwright.Core.Careers;

/// <summary>
/// 의뢰에 맞는 적을 만듭니다.
///
/// <para>
/// <b>적 수는 파티 인원을 따라가고, 난이도는 적의 강함으로만 나타냅니다.</b>
/// </para>
///
/// <para>
/// 예전에는 콘솔 안에서 <c>난이도/2 + 1</c>로 정했습니다. 파티 인원을 전혀 보지 않았고,
/// 정수 나눗셈 때문에 난이도 2에서 갑자기 1명 → 2명이 되었습니다.
/// 혼자 나간 신입이 2명을 상대하게 되어 <b>승률 90% → 14%</b>로 떨어지고
/// 사망·불구가 15% 났습니다. 실제 플레이에서 "시작하자마자 죽었다"로 나타났습니다.
/// </para>
///
/// <para>
/// 수로 난이도를 나타내면 <b>파티 인원과 곱해져 난이도가 두 번 반영됩니다.</b>
/// 4인 파티의 난이도 2와 1인의 난이도 2가 완전히 다른 전투가 되어버립니다.
/// </para>
///
/// 근거: docs/08-balance-log.md #33
/// </summary>
public static class EncounterGenerator
{
    /// <summary>한 전투에 설 수 있는 적의 최대 수. 전열/후열이 무너지지 않는 선.</summary>
    public const int MaxEnemies = 4;

    /// <summary>이 난이도부터는 수적 열세도 함께 걸립니다.</summary>
    public const int OutnumberedFrom = 6;

    /// <summary>파티 인원과 난이도로 적 수를 정합니다.</summary>
    public static int CountFor(int partySize, int difficulty) =>
        Math.Clamp(partySize + (difficulty >= OutnumberedFrom ? 1 : 0), 1, MaxEnemies);

    /// <summary>
    /// 적 무리를 만듭니다.
    /// </summary>
    /// <param name="difficulty">의뢰 난이도. 적의 잠재력 등급과 훈련 연차를 정합니다.</param>
    /// <param name="partySize">파티 인원.</param>
    /// <param name="rng">난수원.</param>
    /// <param name="nameFor">적 이름을 짓는 함수. 코어는 표시 문구를 알지 못합니다.</param>
    public static IReadOnlyList<Adventurer> Generate(
        int difficulty,
        int partySize,
        IRandomSource rng,
        Func<IRandomSource, string> nameFor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(difficulty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partySize);

        int count = CountFor(partySize, difficulty);
        var enemies = new List<Adventurer>(count);

        for (int i = 0; i < count; i++)
        {
            var stream = rng.Fork($"foe:{i}");

            var foe = Adventurer.Recruit(
                $"X{i}", nameFor(stream), stream,
                potentialTier: Math.Clamp(difficulty / 2, 1, 6));

            // 난이도 - 1 만큼 해를 보내 강해집니다. 여기가 난이도의 유일한 표현입니다.
            // 난이도 1이 "실전 1년차"면 첫 실전의 상대가 항상 선배라, 신입의 첫 의뢰와
            // 튜토리얼이 구조적으로 전패였습니다 (docs/08 #40 · #65).
            for (int y = 0; y < difficulty - 1; y++)
            {
                if (foe.Status != AdventurerStatus.Active) break;
                CareerSimulator.ResolveTrainingYear(foe, stream.Fork($"train:{y}"));
            }

            enemies.Add(foe);
        }

        return enemies;
    }
}
