namespace Guildwright.Core.Weapons;

/// <summary>
/// 스타일별 숙련도. <b>랜덤이 아니라 이력입니다.</b>
/// <para>
/// 적성이 "얼마나 빨리 느는가"라면 숙련도는 "실제로 얼마나 익혔는가"입니다.
/// 그 무기를 들고 보낸 햇수가 쌓이므로, "이 아이는 창을 10년 썼다"가 되고
/// <b>무기를 바꾸는 데 시간이라는 기회비용</b>이 생깁니다.
/// </para>
/// <para>
/// 무기를 바꿔도 예전 숙련도는 사라지지 않습니다. 돌아갈 수는 있되, 새 무기는 처음부터입니다.
/// </para>
/// </summary>
public sealed class WeaponProficiency
{
    /// <summary>숙련도 상한.</summary>
    public const int Max = 100;

    /// <summary>훈련 1년으로 오르는 기본 숙련도.</summary>
    public const double PerTrainingYear = 9.0;

    /// <summary>실전 1년으로 오르는 기본 숙련도. 무기는 실전이 가르칩니다.</summary>
    public const double PerDeploymentYear = 15.0;

    private readonly Dictionary<WeaponStyle, double> _points =
        WeaponStyles.All.ToDictionary(s => s, _ => 0.0);

    public int this[WeaponStyle style] => (int)Math.Round(_points[style]);

    /// <summary>가장 많이 익힌 스타일.</summary>
    public WeaponStyle Best =>
        _points.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;

    /// <summary>
    /// 전투 효율 배율. 숙련도 0에서 0.75배, 100에서 1.30배.
    /// <para>
    /// 하한을 0으로 두지 않는 이유는, 무기를 바꾼 캐릭터가 한동안 완전히 쓸모없어지면
    /// 플레이어가 아예 바꾸지 않게 되어 선택지가 죽기 때문입니다.
    /// </para>
    /// </summary>
    public double EffectivenessOf(WeaponStyle style) =>
        0.75 + _points[style] / Max * 0.55;

    internal void Advance(WeaponStyle style, AptitudeGrade aptitude, double baseGain)
    {
        double gain = baseGain * aptitude.GrowthMultiplier();

        // 상한에 가까울수록 느려집니다. 100에 도달하는 데 시간이 걸려야 이력이 의미를 갖습니다.
        double remaining = Max - _points[style];
        double scaled = gain * Math.Clamp(remaining / Max, 0.12, 1.0);

        _points[style] = Math.Min(Max, _points[style] + scaled);
    }

    public IEnumerable<KeyValuePair<WeaponStyle, int>> All =>
        _points.OrderBy(kv => kv.Key).Select(kv => new KeyValuePair<WeaponStyle, int>(kv.Key, (int)Math.Round(kv.Value)));

    public override string ToString() =>
        string.Join(" ", All.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key.ToKorean()}:{kv.Value}"));
}
