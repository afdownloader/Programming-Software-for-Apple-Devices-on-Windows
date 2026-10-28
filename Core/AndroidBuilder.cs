using System.Security.Cryptography;

namespace WebToAppStudio.Core;

public sealed record AndroidBuildResult(string ApkPath, string? AabPath, string KeystorePath);

/// <summary>Generates the Gradle project and builds a signed APK (and optionally an AAB for Google Play).</summary>
public static class AndroidBuilder
{
    public static async Task<AndroidBuildResult> BuildAsync(AppConfig c, bool acceptLicenses, Action<string> log, CancellationToken ct)
    {
        var errors = c.Validate();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var jdk = ToolLocator.FindJdkHome()
            ?? throw new InvalidOperationException("JDK 17+ not found. Install Android Studio (it bundles a JDK) or Temurin JDK 17/21.");
        var sdk = ToolLocator.FindAndroidSdk()
            ?? throw new InvalidOperationException("Android SDK not found. Install Android Studio once and open it so it downloads the SDK.");
        log("JDK : " + jdk);
        log("SDK : " + sdk);

        if (acceptLicenses) ToolLocator.AcceptAndroidLicenses(sdk, log);

        var gradleHome = await ToolLocator.EnsureGradleAsync(log, ct);
        var launcher = Directory.GetFiles(Path.Combine(gradleHome, "lib"), "gradle-launcher-*.jar").First();

        Directory.CreateDirectory(c.OutputDir);
        var projectDir = Path.Combine(c.OutputDir, "android-project");

        log("Generating Android project...");
        AndroidProjectGenerator.Generate(c, projectDir);
        File.WriteAllText(Path.Combine(projectDir, "local.properties"), "sdk.dir=" + sdk.Replace('\\', '/') + "\n");

        var (ksPath, ksPass) = await EnsureKeystoreAsync(c, jdk, log, ct);

        var tasks = new List<string> { "assembleRelease" };
        if (c.BuildAab) tasks.Add("bundleRelease");

        var args = new List<string>
        {
            "-Xmx64m", "-Xms64m", "-Dorg.gradle.appname=gradle",
            "-classpath", launcher, "org.gradle.launcher.GradleMain"
        };
        args.AddRange(tasks);
        args.AddRange(new[] { "--console=plain", "--no-daemon" });

        var env = new Dictionary<string, string>
        {
            ["JAVA_HOME"] = jdk,
            ["ANDROID_HOME"] = sdk,
            ["ANDROID_SDK_ROOT"] = sdk,
            ["KS_PATH"] = ksPath,
            ["KS_PASS"] = ksPass,
            ["KEY_ALIAS"] = c.KeyAlias,
            ["KEY_PASS"] = ksPass
        };

        log("Running Gradle (first build downloads Android components - this can take several minutes)...");
        int code = await ProcessRunner.RunAsync(
            Path.Combine(jdk, "bin", ToolLocator.Exe("java")), args, projectDir, log, ct, env, new[] { ksPass });
        if (code != 0)
            throw new InvalidOperationException(
                "Gradle failed (exit code " + code + "). Read the log above. If it mentions licences, tick " +
                "\"Accept Android SDK licenses\" and build again.");

        var apkSrc = Path.Combine(projectDir, "app", "build", "outputs", "apk", "release", "app-release.apk");
        if (!File.Exists(apkSrc)) throw new FileNotFoundException("Gradle finished but the APK was not found.", apkSrc);

        var apkOut = Path.Combine(c.OutputDir, $"{c.SafeName}-{c.VersionName}.apk");
        File.Copy(apkSrc, apkOut, true);

        string? aabOut = null;
        if (c.BuildAab)
        {
            var aabSrc = Path.Combine(projectDir, "app", "build", "outputs", "bundle", "release", "app-release.aab");
            if (File.Exists(aabSrc))
            {
                aabOut = Path.Combine(c.OutputDir, $"{c.SafeName}-{c.VersionName}.aab");
                File.Copy(aabSrc, aabOut, true);
            }
        }

        log("APK ready: " + apkOut);
        return new AndroidBuildResult(apkOut, aabOut, ksPath);
    }

    private static async Task<(string path, string pass)> EnsureKeystoreAsync(AppConfig c, string jdk, Action<string> log, CancellationToken ct)
    {
        var ksPath = string.IsNullOrWhiteSpace(c.KeystorePath)
            ? Path.Combine(c.OutputDir, "keystore", c.PackageId + ".jks")
            : c.KeystorePath;
        var passFile = ksPath + ".password.txt";
        var alias = string.IsNullOrWhiteSpace(c.KeyAlias) ? "release" : c.KeyAlias;
        c.KeyAlias = alias;

        if (File.Exists(ksPath))
        {
            var pass = c.KeystorePassword;
            if (string.IsNullOrEmpty(pass) && File.Exists(passFile)) pass = File.ReadAllText(passFile).Trim();
            if (string.IsNullOrEmpty(pass))
                throw new InvalidOperationException("Keystore exists but its password is unknown. Enter the keystore password on the Android page.");
            log("Using keystore: " + ksPath);
            return (ksPath, pass);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ksPath)!);
        var password = string.IsNullOrEmpty(c.KeystorePassword) ? RandomPassword() : c.KeystorePassword;
        var cn = new string(c.AppName.Where(ch => !",=+<>#;\\\"".Contains(ch)).ToArray()).Trim();
        if (cn.Length == 0) cn = "App";

        log("Creating a new signing keystore...");
        int code = await ProcessRunner.RunAsync(
            Path.Combine(jdk, "bin", ToolLocator.Exe("keytool")),
            new[]
            {
                "-genkeypair", "-keystore", ksPath, "-alias", alias, "-keyalg", "RSA", "-keysize", "2048",
                "-validity", "10000", "-storepass", password, "-keypass", password,
                "-dname", $"CN={cn}, OU=Mobile, O={cn}, C=US"
            },
            null, log, ct, null, new[] { password });
        if (code != 0 || !File.Exists(ksPath)) throw new InvalidOperationException("keytool failed to create the keystore.");

        File.WriteAllText(passFile, password);
        log("IMPORTANT: back up " + ksPath + " and " + passFile + " - without them you can never update this app on Google Play.");
        return (ksPath, password);
    }

    private static string RandomPassword()
    {
        const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(20);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    /// <summary>Installs the APK on a phone connected by USB (needs USB debugging enabled on the phone).</summary>
    public static async Task<bool> InstallViaAdbAsync(string apk, Action<string> log, CancellationToken ct)
    {
        var adb = ToolLocator.FindAdb(ToolLocator.FindAndroidSdk());
        if (adb == null) { log("adb not found (install Android SDK Platform-Tools)."); return false; }
        int code = await ProcessRunner.RunAsync(adb, new[] { "install", "-r", apk }, null, log, ct);
        return code == 0;
    }
}
