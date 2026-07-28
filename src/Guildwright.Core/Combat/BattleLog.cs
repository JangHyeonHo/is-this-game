namespace Guildwright.Core.Combat;

/// <summary>
/// 전투 기록 수집기.
/// <para>
/// <b>관전 중일 때는 줄이 생기는 즉시 흘려보냅니다.</b> 전투가 끝난 뒤에 한꺼번에 출력하면,
/// 수동 개입 화면에서 "무슨 일이 있었길래 내가 지금 HP 10인가"를 알 수 없습니다.
/// 실제로 수동 전투를 돌려보고 발견한 문제입니다.
/// </para>
/// <para>
/// <paramref name="onLine"/>은 <b>출력 전용</b>입니다. 이 콜백 안에서 전투 상태를 건드리면
/// 결정론이 깨집니다 — 콜백을 넣든 안 넣든 전투 결과는 완전히 같아야 합니다.
/// </para>
/// </summary>
public sealed class BattleLog(Action<string>? onLine = null)
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public void Add(string line)
    {
        _lines.Add(line);
        onLine?.Invoke(line);
    }
}
