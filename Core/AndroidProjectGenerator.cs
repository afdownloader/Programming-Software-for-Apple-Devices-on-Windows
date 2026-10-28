namespace WebToAppStudio.Core;

/// <summary>Writes a complete, self-contained Android Studio / Gradle project that wraps the website in a WebView.</summary>
public static class AndroidProjectGenerator
{
    public static void Generate(AppConfig c, string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var pkgPath = c.PackageId.Replace('.', '/');
        bool cleartext = c.WebUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        Escape.WriteFile(dir, "settings.gradle", SettingsGradle);
        Escape.WriteFile(dir, "build.gradle", RootGradle);
        Escape.WriteFile(dir, "gradle.properties", GradleProps);
        Escape.WriteFile(dir, "app/build.gradle", AppGradle
            .Replace("__PKG__", c.PackageId)
            .Replace("__VCODE__", c.VersionCode.ToString())
            .Replace("__VNAME__", c.VersionName));
        Escape.WriteFile(dir, "app/proguard-rules.pro", "# no rules needed\n");

        Escape.WriteFile(dir, "app/src/main/AndroidManifest.xml",
            Manifest.Replace("__CLEARTEXT__", cleartext ? "true" : "false"));
        Escape.WriteFile(dir, $"app/src/main/java/{pkgPath}/MainActivity.java",
            MainActivity.Replace("__PKG__", c.PackageId));

        Escape.WriteFile(dir, "app/src/main/res/values/strings.xml", Strings
            .Replace("__NAME__", Escape.AndroidString(c.AppName))
            .Replace("__URL__", Escape.AndroidString(c.WebUrl))
            .Replace("__COLOR__", c.ThemeColor));
        Escape.WriteFile(dir, "app/src/main/res/values/config.xml", ConfigXml
            .Replace("__JS__", c.EnableJavaScript ? "true" : "false")
            .Replace("__PULL__", c.PullToRefresh ? "true" : "false")
            .Replace("__ZOOM__", c.AllowZoom ? "true" : "false")
            .Replace("__EXT__", c.ExternalLinksInBrowser ? "true" : "false")
            .Replace("__FULL__", c.Fullscreen ? "true" : "false"));
        Escape.WriteFile(dir, "app/src/main/res/values/colors.xml", ColorsXml.Replace("__COLOR__", c.ThemeColor));
        Escape.WriteFile(dir, "app/src/main/res/mipmap-anydpi-v26/ic_launcher.xml", AdaptiveIcon);

        string[] density = { "mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi" };
        int[] legacy = { 48, 72, 96, 144, 192 };
        int[] foreground = { 108, 162, 216, 324, 432 };
        for (int i = 0; i < density.Length; i++)
        {
            var res = Path.Combine(dir, "app", "src", "main", "res", "mipmap-" + density[i]);
            IconService.SavePng(c, Path.Combine(res, "ic_launcher.png"), legacy[i], IconKind.Legacy);
            IconService.SavePng(c, Path.Combine(res, "ic_launcher_foreground.png"), foreground[i], IconKind.AdaptiveForeground);
        }
    }

    // ------------------------------------------------------------------ templates

    private const string SettingsGradle = """
pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}
rootProject.name = "WebApp"
include ':app'
""";

    private const string RootGradle = """
plugins {
    id 'com.android.application' version '8.7.3' apply false
}
""";

    private const string GradleProps = """
org.gradle.jvmargs=-Xmx2048m -Dfile.encoding=UTF-8
android.useAndroidX=true
android.nonTransitiveRClass=true
""";

    private const string AppGradle = """
plugins {
    id 'com.android.application'
}

android {
    namespace '__PKG__'
    compileSdk 35

    defaultConfig {
        applicationId '__PKG__'
        minSdk 24
        targetSdk 35
        versionCode __VCODE__
        versionName '__VNAME__'
    }

    signingConfigs {
        release {
            storeFile file(System.getenv('KS_PATH') ?: 'missing.jks')
            storePassword System.getenv('KS_PASS')
            keyAlias System.getenv('KEY_ALIAS')
            keyPassword System.getenv('KEY_PASS')
        }
    }

    buildTypes {
        release {
            minifyEnabled false
            signingConfig signingConfigs.release
            proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'), 'proguard-rules.pro'
        }
    }

    compileOptions {
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
    }
}

dependencies {
    implementation 'androidx.swiperefreshlayout:swiperefreshlayout:1.1.0'
}
""";

    private const string Manifest = """
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">

    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />

    <application
        android:allowBackup="true"
        android:icon="@mipmap/ic_launcher"
        android:label="@string/app_name"
        android:supportsRtl="true"
        android:usesCleartextTraffic="__CLEARTEXT__"
        android:theme="@android:style/Theme.DeviceDefault.Light.NoActionBar">

        <activity
            android:name=".MainActivity"
            android:exported="true"
            android:configChanges="orientation|screenSize|smallestScreenSize|screenLayout|keyboardHidden|uiMode"
            android:windowSoftInputMode="adjustResize">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>
    </application>
</manifest>
""";

    private const string Strings = """
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">__NAME__</string>
    <string name="web_url" formatted="false">__URL__</string>
    <string name="theme_color">__COLOR__</string>
    <string name="offline_title">No connection · اتصال برقرار نیست</string>
    <string name="offline_text">Check your internet connection and try again.</string>
    <string name="offline_text_fa">اینترنت خود را بررسی کنید و دوباره تلاش کنید.</string>
    <string name="offline_retry">Retry · تلاش دوباره</string>
</resources>
""";

    private const string ConfigXml = """
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <bool name="enable_js">__JS__</bool>
    <bool name="pull_to_refresh">__PULL__</bool>
    <bool name="allow_zoom">__ZOOM__</bool>
    <bool name="external_links">__EXT__</bool>
    <bool name="fullscreen">__FULL__</bool>
</resources>
""";

    private const string ColorsXml = """
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <color name="ic_launcher_background">__COLOR__</color>
</resources>
""";

    private const string AdaptiveIcon = """
<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@color/ic_launcher_background" />
    <foreground android:drawable="@mipmap/ic_launcher_foreground" />
</adaptive-icon>
""";

    private const string MainActivity = """
package __PKG__;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.ProgressBar;

import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

public class MainActivity extends Activity {

    private static final int FILE_REQUEST = 4711;

    private WebView web;
    private SwipeRefreshLayout swipe;
    private ProgressBar bar;
    private ValueCallback<Uri[]> filePicker;
    private String home;
    private String homeBase;
    private boolean externalLinks;

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);

        home = getString(R.string.web_url);
        homeBase = baseDomain(Uri.parse(home).getHost());
        externalLinks = getResources().getBoolean(R.bool.external_links);
        boolean fullscreen = getResources().getBoolean(R.bool.fullscreen);
        int theme = Color.parseColor(getString(R.string.theme_color));

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(theme);

        web = new WebView(this);
        swipe = new SwipeRefreshLayout(this);
        swipe.addView(web, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        swipe.setEnabled(getResources().getBoolean(R.bool.pull_to_refresh));
        swipe.setOnRefreshListener(() -> web.reload());
        swipe.setOnChildScrollUpCallback((parent, child) -> web.getScrollY() > 0);
        root.addView(swipe, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        bar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        bar.setMax(100);
        int barHeight = (int) (3 * getResources().getDisplayMetrics().density);
        root.addView(bar, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, barHeight));
        setContentView(root);

        View decor = getWindow().getDecorView();
        if (fullscreen) {
            decor.setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                    | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                    | View.SYSTEM_UI_FLAG_FULLSCREEN
                    | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        } else {
            // Android 15 draws edge-to-edge: keep the page clear of the system bars.
            root.setOnApplyWindowInsetsListener((v, insets) -> {
                v.setPadding(insets.getSystemWindowInsetLeft(), insets.getSystemWindowInsetTop(),
                        insets.getSystemWindowInsetRight(), insets.getSystemWindowInsetBottom());
                return insets;
            });
            double luma = (0.299 * Color.red(theme) + 0.587 * Color.green(theme) + 0.114 * Color.blue(theme)) / 255.0;
            if (luma > 0.6 && Build.VERSION.SDK_INT >= 23) {
                decor.setSystemUiVisibility(decor.getSystemUiVisibility() | View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR);
            }
        }

        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(getResources().getBoolean(R.bool.enable_js));
        s.setDomStorageEnabled(true);
        s.setMediaPlaybackRequiresUserGesture(false);
        s.setUseWideViewPort(true);
        s.setLoadWithOverviewMode(true);
        s.setAllowFileAccess(false);
        s.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        boolean zoom = getResources().getBoolean(R.bool.allow_zoom);
        s.setSupportZoom(zoom);
        s.setBuiltInZoomControls(zoom);
        s.setDisplayZoomControls(false);

        web.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                return route(request.getUrl());
            }

            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                bar.setVisibility(View.VISIBLE);
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                bar.setVisibility(View.GONE);
                swipe.setRefreshing(false);
            }

            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request.isForMainFrame()) {
                    swipe.setRefreshing(false);
                    showOffline(view);
                }
            }
        });

        web.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onProgressChanged(WebView view, int progress) {
                bar.setProgress(progress);
            }

            @Override
            public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> callback, FileChooserParams params) {
                if (filePicker != null) filePicker.onReceiveValue(null);
                filePicker = callback;
                try {
                    startActivityForResult(params.createIntent(), FILE_REQUEST);
                } catch (ActivityNotFoundException e) {
                    filePicker = null;
                    return false;
                }
                return true;
            }
        });

        web.setDownloadListener((url, userAgent, contentDisposition, mimeType, length) -> openExternal(Uri.parse(url)));

        if (state != null) {
            web.restoreState(state);
        } else {
            web.loadUrl(home);
        }
    }

    private boolean route(Uri u) {
        String scheme = u.getScheme();
        if ("http".equals(scheme) || "https".equals(scheme)) {
            if (!externalLinks || sameSite(u.getHost())) return false;
            openExternal(u);
            return true;
        }
        if ("about".equals(scheme) || "blob".equals(scheme) || "data".equals(scheme) || "javascript".equals(scheme)) {
            return false;
        }
        openExternal(u); // tel:, mailto:, intent:, market: ...
        return true;
    }

    private void openExternal(Uri u) {
        try {
            startActivity(new Intent(Intent.ACTION_VIEW, u));
        } catch (Exception ignored) {
        }
    }

    private static String baseDomain(String host) {
        if (host == null) return "";
        String[] p = host.toLowerCase().split("\\.");
        return p.length >= 2 ? p[p.length - 2] + "." + p[p.length - 1] : host.toLowerCase();
    }

    private boolean sameSite(String host) {
        return host != null && baseDomain(host).equals(homeBase);
    }

    private void showOffline(WebView view) {
        String html = "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'></head>"
                + "<body style='font-family:sans-serif;text-align:center;padding-top:26vh;color:#555'>"
                + "<h2>" + getString(R.string.offline_title) + "</h2>"
                + "<p>" + getString(R.string.offline_text) + "<br>" + getString(R.string.offline_text_fa) + "</p>"
                + "<a style='font-size:18px' href='" + TextUtils.htmlEncode(home) + "'>"
                + getString(R.string.offline_retry) + "</a></body></html>";
        view.loadDataWithBaseURL(null, html, "text/html", "UTF-8", null);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == FILE_REQUEST && filePicker != null) {
            filePicker.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(resultCode, data));
            filePicker = null;
        } else {
            super.onActivityResult(requestCode, resultCode, data);
        }
    }

    @Override
    protected void onSaveInstanceState(Bundle out) {
        super.onSaveInstanceState(out);
        web.saveState(out);
    }

    @Override
    public void onBackPressed() {
        if (web.canGoBack()) web.goBack();
        else super.onBackPressed();
    }

    @Override
    protected void onPause() {
        web.onPause();
        super.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        web.onResume();
    }

    @Override
    protected void onDestroy() {
        swipe.removeAllViews();
        web.destroy();
        super.onDestroy();
    }
}
""";
}
