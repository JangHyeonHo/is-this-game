namespace Guildwright.Cli;

/// <summary>콘솔 입출력 헬퍼. 게임 규칙은 여기 두지 않습니다.</summary>
public static class Ui
{
    /// <summary>
    /// 입력을 한 줄 읽습니다.
    /// <para>
    /// stdin이 닫히면 <see cref="Console.ReadLine"/>이 계속 null을 돌려주는데,
    /// 이걸 처리하지 않으면 메뉴가 무한히 반복 출력됩니다.
    /// (파이프로 입력을 넣어 테스트하다가 실제로 겪었습니다.)
    /// </para>
    /// </summary>
    private static string ReadLineOrQuit()
    {
        string? input = Console.ReadLine();
        if (input is not null)
        {
            // 입력을 파이프로 넣으면 터미널이 대신 메아리쳐 주지 않아, 기록만 보면
            // "무엇을 골랐는지"가 사라집니다. 대본 플레이 기록을 남기려면 필요합니다.
            if (Console.IsInputRedirected) Console.WriteLine(input);
            return input;
        }

        Console.WriteLine();
        Console.WriteLine("   입력이 끝나 종료합니다.");
        Environment.Exit(0);
        return "";
    }

    public static void Title(string text)
    {
        Console.WriteLine();
        Console.WriteLine("═══ " + text + " " + new string('═', Math.Max(0, 56 - text.Length)));
    }

    public static void Section(string text)
    {
        Console.WriteLine();
        Console.WriteLine("── " + text + " " + new string('─', Math.Max(0, 52 - text.Length)));
    }

    public static void Line(string text = "") => Console.WriteLine(text);

    public static void Note(string text) => Console.WriteLine("   " + text);

    public static void Pause(string message = "계속하려면 Enter")
    {
        Console.Write($"   [{message}] ");
        ReadLineOrQuit();
    }

    /// <summary>1부터 시작하는 번호를 하나 고릅니다. 기본값이 있으면 Enter로 그것을 고릅니다.</summary>
    public static int Choose(string prompt, IReadOnlyList<string> options, int? defaultIndex = null)
    {
        while (true)
        {
            Console.WriteLine();
            for (int i = 0; i < options.Count; i++)
            {
                string mark = i == defaultIndex ? " ← Enter" : "";
                Console.WriteLine($"   {i + 1}) {options[i]}{mark}");
            }

            Console.Write($"{prompt} > ");
            string input = ReadLineOrQuit();

            if (string.IsNullOrWhiteSpace(input) && defaultIndex is { } d) return d;

            if (int.TryParse(input.Trim(), out int n) && n >= 1 && n <= options.Count)
            {
                return n - 1;
            }

            Console.WriteLine("   1~" + options.Count + " 사이의 번호를 입력하세요.");
        }
    }

    /// <summary>여러 번호를 고릅니다. 빈 입력이면 아무것도 고르지 않습니다.</summary>
    public static IReadOnlyList<int> ChooseMany(string prompt, IReadOnlyList<string> options, int max = int.MaxValue)
    {
        while (true)
        {
            Console.WriteLine();
            for (int i = 0; i < options.Count; i++)
            {
                Console.WriteLine($"   {i + 1}) {options[i]}");
            }

            Console.Write($"{prompt} (번호를 띄어쓰기로, 없으면 Enter) > ");
            string input = ReadLineOrQuit();

            if (string.IsNullOrWhiteSpace(input)) return [];

            var picked = new List<int>();
            bool ok = true;

            foreach (var token in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, out int n) && n >= 1 && n <= options.Count && !picked.Contains(n - 1))
                {
                    picked.Add(n - 1);
                }
                else { ok = false; break; }
            }

            if (ok && picked.Count <= max) return picked;

            Console.WriteLine(picked.Count > max
                ? $"   최대 {max}명까지 고를 수 있습니다."
                : "   올바른 번호를 입력하세요.");
        }
    }

    public static bool Confirm(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt} (y/N) > ");
            string input = ReadLineOrQuit().Trim().ToLowerInvariant();
            if (input is "y" or "yes") return true;
            if (input is "n" or "no" or "") return false;
            Console.WriteLine("   y 또는 n으로 답해주세요.");
        }
    }

    /// <summary>0~1 비율을 막대로 표시합니다.</summary>
    public static string Bar(double ratio, int width = 12)
    {
        int filled = Math.Clamp((int)Math.Round(ratio * width), 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }
}
