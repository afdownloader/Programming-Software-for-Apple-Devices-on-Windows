using System.Diagnostics;
using System.Text;

namespace WebToAppStudio.Core;

public static class ProcessRunner
{
    /// <summary>
    /// Runs a program, streams its output line by line to <paramref name="log"/> and returns the exit code
    /// (-1 if the program could not be started). Anything in <paramref name="secrets"/> is masked in the log.
    /// </summary>
    public static async Task<int> RunAsync(
        string exe,
        IEnumerable<string> args,
        string? workDir,
        Action<string> log,
        CancellationToken ct = default,
        IDictionary<string, string>? env = null,
        IEnumerable<string>? secrets = null,
        bool echoCommand = true)
    {
        var argList = args.ToList();
        var mask = (secrets ?? Array.Empty<string>()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        string Masked(string s)
        {
            foreach (var m in mask) s = s.Replace(m, "******");
            return s;
        }

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workDir ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in argList) psi.ArgumentList.Add(a);
        if (env != null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;

        if (echoCommand)
            log("> " + Masked(Path.GetFileName(exe) + " " + string.Join(' ', argList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))));

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) log(Masked(e.Data)); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) log(Masked(e.Data)); };

        try { p.Start(); }
        catch (Exception ex)
        {
            log("ERROR: cannot start " + Path.GetFileName(exe) + ": " + ex.Message);
            return -1;
        }

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            throw;
        }
        return p.ExitCode;
    }
}
