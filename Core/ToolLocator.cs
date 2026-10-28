using System.IO.Compression;

namespace WebToAppStudio.Core;

/// <summary>Finds (or downloads) the external tools the builders need.</summary>
public static class ToolLocator
{
    public const string GradleVersion = "8.10.2";

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebToAppStudio");

    public static string ToolsDir => Path.Combine(AppDataDir, "tools");

    public static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    public static string? Which(string name)
    {
        var exe = Exe(name);
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim().Trim('"'), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    /// <summary>Looks for a tool next to the app (./tools), in %LOCALAPPDATA%\WebToAppStudio\tools, then on PATH.</summary>
    public static string? FindTool(string name, string? hintDir = null)
    {
        var exe = Exe(name);
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(hintDir)) dirs.Add(hintDir);
        dirs.Add(Path.Combine(AppContext.BaseDirectory, "tools"));
        dirs.Add(AppContext.BaseDirectory);
        dirs.Add(ToolsDir);
        try
        {
            if (Directory.Exists(ToolsDir)) dirs.AddRange(Directory.GetDirectories(ToolsDir));
            var local = Path.Combine(AppContext.BaseDirectory, "tools");
            if (Directory.Exists(local)) dirs.AddRange(Directory.GetDirectories(local));
        }
        catch { }

        foreach (var d in dirs)
        {
            foreach (var sub in new[] { "", "bin" })
            {
                var p = Path.Combine(d, sub, exe);
                if (File.Exists(p)) return p;
            }
        }
        return Which(name);
    }

    /// <summary>Uses the configured path when it points to a real file, otherwise auto-detects.</summary>
    public static string? Resolve(string configured, string toolName)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        return FindTool(toolName, string.IsNullOrWhiteSpace(configured) ? null : configured);
    }

    private static bool IsJdk(string home) =>
        File.Exists(Path.Combine(home, "bin", Exe("java"))) && File.Exists(Path.Combine(home, "bin", Exe("keytool")));

    /// <summary>Returns a JDK home (Android Studio's bundled JBR first, then JAVA_HOME, then common installs).</summary>
    public static string? FindJdkHome()
    {
        var candidates = new List<string>();
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        candidates.Add(Path.Combine(pf, "Android", "Android Studio", "jbr"));
        candidates.Add(Path.Combine(local, "Programs", "Android Studio", "jbr"));
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome)) candidates.Add(javaHome.Trim('"'));

        foreach (var vendor in new[] { "Eclipse Adoptium", "Java", "Microsoft", "Amazon Corretto", "Zulu", "BellSoft" })
        {
            var root = Path.Combine(pf, vendor);
            try
            {
                if (Directory.Exists(root))
                    candidates.AddRange(Directory.GetDirectories(root).OrderByDescending(d => d));
            }
            catch { }
        }

        var onPath = Which("java");
        if (onPath != null)
        {
            var home = Path.GetDirectoryName(Path.GetDirectoryName(onPath));
            if (home != null) candidates.Add(home);
        }

        return candidates.FirstOrDefault(IsJdk);
    }

    public static string? FindAndroidSdk()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
        };
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && Directory.Exists(c));
    }

    public static string? FindAdb(string? sdk)
    {
        if (sdk != null)
        {
            var p = Path.Combine(sdk, "platform-tools", Exe("adb"));
            if (File.Exists(p)) return p;
        }
        return Which("adb");
    }

    /// <summary>Best-effort acceptance of the standard Android SDK licences (user ticked the box).</summary>
    public static void AcceptAndroidLicenses(string sdk, Action<string> log)
    {
        var dir = Path.Combine(sdk, "licenses");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "android-sdk-license"),
            "\n8933bad161af4178b1185d1a37fbf41ea5269c55\nd56f5187479451eabf01fb78af6dfcb131a6481e\n24333f8a63b6825ea9c5514f83c2829b004d1fee\n");
        log("Android SDK licences written to " + dir);
    }

    /// <summary>Downloads Gradle once into %LOCALAPPDATA%\WebToAppStudio\tools and returns its home folder.</summary>
    public static async Task<string> EnsureGradleAsync(Action<string> log, CancellationToken ct)
    {
        var home = Path.Combine(ToolsDir, "gradle-" + GradleVersion);
        var lib = Path.Combine(home, "lib");
        if (Directory.Exists(lib) && Directory.GetFiles(lib, "gradle-launcher-*.jar").Length > 0) return home;

        Directory.CreateDirectory(ToolsDir);
        var zipPath = Path.Combine(ToolsDir, $"gradle-{GradleVersion}-bin.zip");
        var url = $"https://services.gradle.org/distributions/gradle-{GradleVersion}-bin.zip";
        log($"Downloading Gradle {GradleVersion} (one time only)...");

        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
        using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1;
            await using var fs = File.Create(zipPath);
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            var buf = new byte[81920];
            long done = 0; int lastPct = -1; int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0)
                {
                    int pct = (int)(done * 100 / total);
                    if (pct / 10 != lastPct / 10) { lastPct = pct; log($"  Gradle download {pct}%"); }
                }
            }
        }

        log("Extracting Gradle...");
        ZipFile.ExtractToDirectory(zipPath, ToolsDir, true);
        try { File.Delete(zipPath); } catch { }
        return home;
    }
}
