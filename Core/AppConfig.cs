using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WebToAppStudio.Core;

/// <summary>Everything the user configures. Passwords are never written to disk.</summary>
public sealed class AppConfig
{
    // ---- General ----
    public string AppName { get; set; } = "My App";
    public string WebUrl { get; set; } = "https://";
    public string PackageId { get; set; } = "com.example.myapp";
    public string VersionName { get; set; } = "1.0.0";
    public int VersionCode { get; set; } = 1;
    public string ThemeColor { get; set; } = "#0A84FF";
    public string IconPath { get; set; } = "";
    public string OutputDir { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WebToAppStudio");

    // ---- Behaviour (shared by Android + iOS) ----
    public bool EnableJavaScript { get; set; } = true;
    public bool PullToRefresh { get; set; } = true;
    public bool AllowZoom { get; set; }
    public bool ExternalLinksInBrowser { get; set; } = true;
    public bool Fullscreen { get; set; }

    // ---- Android ----
    public string KeystorePath { get; set; } = "";
    public string KeyAlias { get; set; } = "release";
    [JsonIgnore] public string KeystorePassword { get; set; } = "";
    public bool BuildAab { get; set; }

    // ---- iOS ----
    public string TemplateIpaPath { get; set; } = "";
    public string P12Path { get; set; } = "";
    [JsonIgnore] public string P12Password { get; set; } = "";
    public string ProvisionPath { get; set; } = "";
    public string ZsignPath { get; set; } = "";
    public string IdeviceInstallerPath { get; set; } = "";

    [JsonIgnore]
    public string SafeName
    {
        get
        {
            var n = string.Concat(AppName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
            return n.Length == 0 ? "app" : n;
        }
    }

    private static readonly HashSet<string> JavaWords = new(StringComparer.Ordinal)
    {
        "abstract","assert","boolean","break","byte","case","catch","char","class","const","continue","default",
        "do","double","else","enum","extends","final","finally","float","for","goto","if","implements","import",
        "instanceof","int","interface","long","native","new","package","private","protected","public","return",
        "short","static","strictfp","super","switch","synchronized","this","throw","throws","transient","try",
        "void","volatile","while","true","false","null"
    };

    /// <summary>Normalises the URL and returns a list of problems (empty = OK).</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(AppName)) errors.Add("App name is required.");

        var url = (WebUrl ?? "").Trim();
        if (url.Length > 0 && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                           && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        WebUrl = url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
            || (!uri.Host.Contains('.') && uri.Host != "localhost"))
            errors.Add("Website URL is not valid (example: https://example.com).");

        if (!Regex.IsMatch(PackageId ?? "", @"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$"))
            errors.Add("Package / Bundle ID must look like com.company.app (letters, digits, underscore, at least two parts).");
        else if ((PackageId ?? "").Split('.').Any(p => JavaWords.Contains(p)))
            errors.Add("Package ID contains a reserved Java word (e.g. 'new', 'class').");

        if (!Regex.IsMatch(VersionName ?? "", @"^[0-9A-Za-z._-]{1,30}$"))
            errors.Add("Version name may only contain letters, digits, dot, dash, underscore.");
        if (VersionCode < 1) errors.Add("Version code must be 1 or higher.");
        if (!Regex.IsMatch(ThemeColor ?? "", "^#[0-9A-Fa-f]{6}$"))
            errors.Add("Theme colour must be like #0A84FF.");
        if (string.IsNullOrWhiteSpace(OutputDir)) errors.Add("Output folder is required.");

        return errors;
    }

    // ---- persistence (last used settings, no secrets) ----
    private static string SettingsFile =>
        Path.Combine(ToolLocator.AppDataDir, "project.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ToolLocator.AppDataDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, Json));
        }
        catch { /* best effort */ }
    }

    public static AppConfig LoadOrDefault()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(SettingsFile)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
    }
}
