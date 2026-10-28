using System.Text.RegularExpressions;

namespace WebToAppStudio.Core;

public sealed record IosDevice(string Udid, string Name)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Udid : $"{Name}  ({Udid})";
}

/// <summary>Talks to a USB-connected iPhone through the libimobiledevice command-line tools.</summary>
public static class DeviceInstaller
{
    public static async Task<List<IosDevice>> ListAsync(AppConfig c, Action<string> log, CancellationToken ct)
    {
        var hint = string.IsNullOrWhiteSpace(c.IdeviceInstallerPath) ? null : Path.GetDirectoryName(c.IdeviceInstallerPath);
        var idevId = ToolLocator.FindTool("idevice_id", hint);
        if (idevId == null)
        {
            log("idevice_id.exe not found. Copy the libimobiledevice tools into the 'tools' folder (see README).");
            return new List<IosDevice>();
        }

        var lines = new List<string>();
        await ProcessRunner.RunAsync(idevId, new[] { "-l" }, null, l => lines.Add(l.Trim()), ct, null, null, false);
        var udids = lines.Where(l => Regex.IsMatch(l, @"^[0-9A-Fa-f\-]{25,40}$")).Distinct().ToList();

        var info = ToolLocator.FindTool("ideviceinfo", hint);
        var result = new List<IosDevice>();
        foreach (var udid in udids)
        {
            string name = "";
            if (info != null)
            {
                var o = new List<string>();
                await ProcessRunner.RunAsync(info, new[] { "-u", udid, "-k", "DeviceName" }, null, l => o.Add(l.Trim()), ct, null, null, false);
                name = o.FirstOrDefault(x => x.Length > 0 && !x.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)) ?? "";
            }
            result.Add(new IosDevice(udid, name));
        }

        if (result.Count == 0)
            log("No iPhone found. Connect it with the cable, unlock it, tap \"Trust\", and make sure iTunes or Apple Devices (Microsoft Store) is installed.");
        return result;
    }

    public static async Task<bool> InstallAsync(AppConfig c, string udid, string ipa, Action<string> log, CancellationToken ct)
    {
        var installer = ToolLocator.Resolve(c.IdeviceInstallerPath, "ideviceinstaller");
        if (installer == null)
        {
            log("ideviceinstaller.exe not found. Put it in the 'tools' folder or choose it on the iOS page.");
            return false;
        }
        if (!File.Exists(ipa)) { log("IPA not found: " + ipa); return false; }

        var output = new List<string>();
        void Tee(string s) { output.Add(s); log(s); }

        log("Installing on iPhone...");
        int code = await ProcessRunner.RunAsync(installer, new[] { "-u", udid, "install", ipa }, null, Tee, ct);
        if (code != 0 && output.Any(l => Regex.IsMatch(l, "usage|unknown|invalid option|unrecognized", RegexOptions.IgnoreCase)))
        {
            log("Retrying with the legacy -i syntax...");
            code = await ProcessRunner.RunAsync(installer, new[] { "-u", udid, "-i", ipa }, null, log, ct);
        }

        if (code == 0)
            log("Installed. If iOS says \"Untrusted Developer\": Settings > General > VPN & Device Management > trust your certificate.");
        return code == 0;
    }
}
