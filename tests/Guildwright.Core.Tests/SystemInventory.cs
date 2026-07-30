using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Tests;

/// <summary>
/// <b>구현 현황 인벤토리를 코드에서 생성합니다.</b>
/// <para>
/// 손으로 관리하는 목록은 반드시 낡습니다. "추가하면 문서도 고치세요"라는 규칙에
/// 기대는 방식은 이미 한 번 실패했습니다 — CLAUDE.md에 "먼저 읽으세요"가 적혀 있었는데도
/// 이미 구현된 전열/후열·전술 규칙·전투 개입을 "없다"고 답한 사고가 났습니다.
/// </para>
/// <para>
/// 그래서 목록을 어셈블리에서 직접 뽑고, 낡으면 <see cref="SystemInventoryTests"/>가 깨집니다.
/// 사람이 할 일은 <c>docs/09-systems.md</c>의 <b>판단</b>(아직 없는 것, 제약이 막는 곳)뿐입니다.
/// </para>
/// <para>
/// <b>담는 것은 "무엇이 존재하는가"뿐입니다.</b> 밸런스 상수는 넣지 않습니다 —
/// 튜닝할 때마다 문서가 흔들려 스냅샷을 기계적으로 다시 만들게 되고, 그러면 장치가 죽습니다.
/// 수치는 한 번의 grep으로 확인할 수 있고, 실제로 틀렸던 건 존재 여부였습니다.
/// </para>
/// </summary>
public static class SystemInventory
{
    /// <summary>생성 결과가 들어가는 파일. 손으로 고치지 않습니다.</summary>
    public const string FileName = "09-systems.generated.md";

    /// <summary>이 환경 변수를 켜고 테스트를 돌리면 파일을 다시 씁니다.</summary>
    public const string UpdateEnvVar = "UPDATE_INVENTORY";

    private static readonly Assembly Core = typeof(Adventurer).Assembly;

    public static string Generate()
    {
        var docs = LoadXmlDocs();
        var sb = new StringBuilder();

        var types = Core.GetExportedTypes().Where(t => !t.IsNested).ToList();

        sb.AppendLine("# 구현 현황 — 자동 생성");
        sb.AppendLine();
        sb.AppendLine("> ⚠ **이 파일은 손으로 고치지 않습니다.** `Guildwright.Core` 어셈블리에서 생성됩니다.");
        sb.AppendLine("> 코드와 어긋나면 `SystemInventoryTests`가 깨집니다.");
        sb.AppendLine(">");
        sb.AppendLine($"> 다시 만들기: `{UpdateEnvVar}=1 dotnet test --filter SystemInventory`");
        sb.AppendLine(">");
        sb.AppendLine("> **여기 없는 공개 타입은 존재하지 않는 것입니다.** 그게 이 파일의 쓸모입니다.");
        sb.AppendLine("> 수치(상수)는 일부러 넣지 않았습니다 — 코드를 직접 보세요.");
        sb.AppendLine("> 설계 맥락·미구현 목록·막혀 있는 기능은 [09-systems.md](09-systems.md)에 있습니다.");
        sb.AppendLine();
        sb.AppendLine($"공개 타입 {types.Count}개");
        sb.AppendLine();

        // 순회 순서가 어셈블리 배치에 따라 흔들리면 diff가 매번 납니다.
        // 이 프로젝트의 결정론 원칙을 문서 생성에도 그대로 적용합니다.
        var groups = types
            .GroupBy(t => t.Namespace ?? "")
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            sb.AppendLine($"## {group.Key.Replace("Guildwright.Core.", "")}");
            sb.AppendLine();

            foreach (var type in group.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                sb.Append($"- `{Kind(type)} {type.Name}`");

                if (docs.GetValueOrDefault("T:" + type.FullName) is { Length: > 0 } summary)
                {
                    sb.Append($" — {summary}");
                }

                // 열거형 멤버는 그 자체가 "무엇이 있는가"입니다.
                // 활동 7종·상태이상 7종처럼 정확히 이게 잘못 기억되는 부분이라 반드시 넣습니다.
                if (type.IsEnum)
                {
                    sb.Append($"<br>　└ {string.Join(" · ", Enum.GetNames(type).Select(n => $"`{n}`"))}");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string Kind(Type type)
    {
        if (type.IsEnum) return "enum";
        if (type.IsInterface) return "interface";

        // record는 컴파일러가 EqualityContract / <Clone>$ 를 붙여줍니다.
        bool isRecord =
            type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is not null ||
            type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null;

        if (type.IsValueType) return isRecord ? "record struct" : "struct";
        if (isRecord) return "record";
        if (type is { IsAbstract: true, IsSealed: true }) return "static class";
        return "class";
    }

    /// <summary>
    /// XML 주석에서 타입 설명을 읽습니다.
    /// <para>주석이 곧 설명이므로, 별도로 문서를 쓰지 않아도 인벤토리가 채워집니다.</para>
    /// </summary>
    private static Dictionary<string, string> LoadXmlDocs()
    {
        string path = Path.ChangeExtension(Core.Location, ".xml");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!File.Exists(path)) return result;

        foreach (var member in XDocument.Load(path).Descendants("member"))
        {
            string? name = member.Attribute("name")?.Value;
            var summary = member.Element("summary");
            if (name is null || summary is null) continue;

            string text = FirstSentence(summary);
            if (text.Length > 0) result[name] = text;
        }

        return result;
    }

    /// <summary>
    /// 첫 문장만 뽑습니다. 요약은 한 줄이어야 목록으로 읽힙니다.
    /// <para><c>&lt;para&gt;</c> 이후는 배경 설명이라 인벤토리에는 넣지 않습니다.</para>
    /// </summary>
    private static string FirstSentence(XElement summary)
    {
        var head = summary.Nodes()
            .TakeWhile(n => n is not XElement { Name.LocalName: "para" });

        string text = string.Concat(head.Select(n => n is XElement e ? e.Value : n.ToString()));
        text = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        int stop = text.IndexOf(". ", StringComparison.Ordinal);
        if (stop < 0 && text.EndsWith('.')) stop = text.Length - 1;
        if (stop >= 0) text = text[..stop];

        // 목록 한 줄을 깨뜨리는 문자만 최소한으로 처리합니다.
        return text.Replace("\r", "").Replace("\n", " ").Replace("⚠️", "⚠").Trim();
    }

    /// <summary>
    /// 테스트 실행 위치에서 저장소 루트를 찾습니다. <c>CLAUDE.md</c>가 있는 곳입니다.
    /// <para>
    /// ⚠️ 못 찾으면 <b>왜 못 찾았는지</b>를 말해야 합니다. 예전에는 그냥 "저장소 루트를
    /// 찾지 못했습니다"라고만 던졌고, Docker 빌드에서 <c>.dockerignore</c>가 문서를
    /// 제외해 실패했을 때 <b>"인벤토리가 코드와 어긋났습니다"</b>로 보였습니다 —
    /// 실제로 어긋난 게 아니라 비교 대상이 없었던 것입니다 (docs/06 #58).
    /// </para>
    /// </summary>
    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            $"저장소 루트를 찾지 못했습니다 — {AppContext.BaseDirectory} 위로 올라가며 " +
            "CLAUDE.md를 찾았지만 없었습니다.\n" +
            "Docker 안이라면 .dockerignore가 CLAUDE.md나 docs/를 제외했는지 확인하세요. " +
            "인벤토리 테스트는 docs/09-systems.generated.md를 어셈블리와 대조하므로 둘 다 필요합니다.");
    }

    public static string DocumentPath() => Path.Combine(RepositoryRoot(), "docs", FileName);
}
