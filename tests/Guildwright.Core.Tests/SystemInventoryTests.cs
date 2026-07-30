using Xunit;
using Xunit.Abstractions;

namespace Guildwright.Core.Tests;

/// <summary>
/// 인벤토리가 코드보다 뒤처지지 않게 지킵니다.
/// <para>
/// 이 테스트가 이 장치의 전부입니다. 문서를 손으로 관리하면 반드시 낡고,
/// 낡은 인벤토리는 <b>없는 걸 있다고, 있는 걸 없다고</b> 말하게 만들어 아무것도 안 하는 것보다 나쁩니다.
/// </para>
/// 배경: docs/09-systems.md
/// </summary>
public class SystemInventoryTests(ITestOutputHelper output)
{
    [Fact]
    public void 인벤토리_문서가_코드와_일치한다()
    {
        string expected = SystemInventory.Generate();
        string path = SystemInventory.DocumentPath();

        if (Environment.GetEnvironmentVariable(SystemInventory.UpdateEnvVar) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected);
            output.WriteLine($"인벤토리를 다시 썼습니다: {path}");
            return;
        }

        Assert.True(File.Exists(path), $"인벤토리 문서가 없습니다: {path}");

        string actual = File.ReadAllText(path);

        if (Normalize(actual) == Normalize(expected)) return;

        // 어디가 어긋났는지 바로 보여줍니다. "다시 생성하세요"만 있으면 원인을 못 봅니다.
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            string e = i < expectedLines.Length ? expectedLines[i] : "(없음)";
            string a = i < actualLines.Length ? actualLines[i] : "(없음)";
            if (e == a) continue;

            output.WriteLine($"{i + 1}번째 줄부터 다릅니다.");
            output.WriteLine($"  문서: {a}");
            output.WriteLine($"  코드: {e}");
            break;
        }

        Assert.Fail(
            "구현 현황 인벤토리가 코드와 어긋났습니다.\n" +
            $"공개 타입을 추가·삭제·수정했다면 다음으로 다시 만드세요:\n" +
            $"  {SystemInventory.UpdateEnvVar}=1 dotnet test --filter SystemInventory\n" +
            "그리고 같은 커밋에 포함시키세요. 세션 시작 훅이 이 파일을 읽습니다.");
    }

    /// <summary>
    /// 인벤토리가 실제로 <b>비어 있지 않은지</b> 봅니다.
    /// <para>생성기가 조용히 빈 문자열을 뱉으면 테스트는 통과하는데 장치는 죽습니다.</para>
    /// </summary>
    [Fact]
    public void 인벤토리에_핵심_시스템이_들어_있다()
    {
        string doc = SystemInventory.Generate();

        // 이번 사고에서 "없다"고 잘못 답한 것들입니다. 인벤토리의 존재 이유가 이 세 줄입니다.
        Assert.Contains("enum Row", doc);
        Assert.Contains("TacticRule", doc);
        Assert.Contains("IBattleCommander", doc);

        // 열거형 멤버까지 들어가야 "활동 몇 종이었죠"를 기억으로 답하지 않게 됩니다.
        Assert.Contains("Meditation", doc);   // 훈련 활동
        Assert.Contains("MoveFront", doc);    // 전술 행동

        // 상태 효과는 기전과 이름이 갈려 있습니다. 둘 다 잡혀야 합니다.
        Assert.Contains("EffectMechanism", doc);
        Assert.Contains("LoseControl", doc);   // 지시 불통 — 지휘 제약의 근거
        Assert.Contains("Frostbite", doc);     // 이름은 데이터 쪽
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd();
}
