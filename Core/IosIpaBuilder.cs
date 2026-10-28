using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace WebToAppStudio.Core;

/// <summary>
/// Takes the unsigned Template.ipa (built once in the cloud from the exported iOS source),
/// writes the current config into it and signs it with zsign - all on Windows.
/// </summary>
public static class IosIpaBuilder
{
    public static async Task<string> BuildSignedAsync(AppConfig c, Action<string> log, CancellationToken ct)
    {
        var errors = c.Validate();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        if (!File.Exists(c.TemplateIpaPath))
            throw new InvalidOperationException("Select Template.ipa first (download it from the GitHub Actions run - see README).");
        if (!File.Exists(c.P12Path))
            throw new InvalidOperationException("Select your signing certificate (.p12).");
        if (!File.Exists(c.ProvisionPath))
            throw new InvalidOperationException("Select your provisioning profile (.mobileprovision).");

        var zsign = ToolLocator.Resolve(c.ZsignPath, "zsign")
            ?? throw new InvalidOperationException("zsign.exe not found. Put it in the 'tools' folder next to the app or choose it on the iOS page.");

        Directory.CreateDirectory(c.OutputDir);
        var work = Path.Combine(Path.GetTempPath(), "WebToAppStudio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var patched = Path.Combine(work, "patched.ipa");
        File.Copy(c.TemplateIpaPath, patched, true);

        try
        {
            log("Writing app configuration into the template...");
            PatchConfig(patched, IosSourceExporter.ConfigJson(c));

            var output = Path.Combine(c.OutputDir, $"{c.SafeName}-{c.VersionName}-signed.ipa");
            if (File.Exists(output)) File.Delete(output);

            var args = new List<string> { "-k", c.P12Path };
            if (!string.IsNullOrEmpty(c.P12Password)) { args.Add("-p"); args.Add(c.P12Password); }
            args.AddRange(new[]
            {
                "-m", c.ProvisionPath,
                "-b", c.PackageId,
                "-n", c.AppName,
                "-r", c.VersionName,
                "-o", output,
                patched
            });

            log("Signing with zsign...");
            int code = await ProcessRunner.RunAsync(zsign, args, work, log, ct, null, new[] { c.P12Password });
            if (code != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    "zsign failed. Check: certificate password, that the profile contains this certificate, " +
                    "and that the Bundle ID matches the profile (or the profile is a wildcard).");

            log("Signed IPA ready: " + output);
            return output;
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }

    /// <summary>Replaces Payload/*.app/config.json inside the IPA (a zip) without touching other entries.</summary>
    private static void PatchConfig(string ipa, string json)
    {
        using var zip = ZipFile.Open(ipa, ZipArchiveMode.Update);
        var entry = zip.Entries.FirstOrDefault(e => Regex.IsMatch(e.FullName, @"^Payload/[^/]+\.app/config\.json$"))
            ?? throw new InvalidOperationException(
                "config.json was not found inside Template.ipa. Build the template from the iOS source exported by this tool.");
        var name = entry.FullName;
        entry.Delete();
        var created = zip.CreateEntry(name, CompressionLevel.Optimal);
        created.ExternalAttributes = 0x81A4 << 16; // -rw-r--r--
        using var s = created.Open();
        var bytes = new UTF8Encoding(false).GetBytes(json);
        s.Write(bytes, 0, bytes.Length);
    }
}
