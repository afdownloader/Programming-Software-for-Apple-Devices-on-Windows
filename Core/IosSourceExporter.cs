using System.Text.Encodings.Web;
using System.Text.Json;

namespace WebToAppStudio.Core;

/// <summary>
/// Windows cannot compile Swift, so this writes a ready-to-build iOS project (XcodeGen + Swift + GitHub Actions).
/// The cloud Mac builds an unsigned Template.ipa (for USB install from Windows) and, if signing secrets exist,
/// a signed App Store IPA.
/// </summary>
public static class IosSourceExporter
{
    public const string ExecutableName = "WebApp";

    public static string ConfigJson(AppConfig c) => JsonSerializer.Serialize(new
    {
        url = c.WebUrl,
        appName = c.AppName,
        themeColor = c.ThemeColor,
        pullToRefresh = c.PullToRefresh,
        allowZoom = c.AllowZoom,
        externalLinks = c.ExternalLinksInBrowser,
        fullscreen = c.Fullscreen,
        enableJavaScript = c.EnableJavaScript
    }, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    public static void Export(AppConfig c, string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        Escape.WriteFile(dir, "project.yml", ProjectYml
            .Replace("__PKG__", c.PackageId)
            .Replace("__VER__", c.VersionName)
            .Replace("__BUILD__", c.VersionCode.ToString()));
        Escape.WriteFile(dir, "Config/Info.plist", InfoPlist.Replace("__NAME__", Escape.Xml(c.AppName)));
        Escape.WriteFile(dir, "Sources/AppDelegate.swift", AppDelegate);
        Escape.WriteFile(dir, "Sources/WebViewController.swift", WebViewController);
        Escape.WriteFile(dir, "Sources/config.json", ConfigJson(c));
        Escape.WriteFile(dir, "Sources/Assets.xcassets/Contents.json", "{\n  \"info\" : { \"author\" : \"xcode\", \"version\" : 1 }\n}\n");
        Escape.WriteFile(dir, "Sources/Assets.xcassets/AppIcon.appiconset/Contents.json", AppIconContents);
        IconService.SavePng(c, Path.Combine(dir, "Sources", "Assets.xcassets", "AppIcon.appiconset", "icon-1024.png"), 1024, IconKind.Opaque);
        Escape.WriteFile(dir, ".github/workflows/ios.yml", Workflow.Replace("__PKG__", c.PackageId));
        Escape.WriteFile(dir, ".gitignore", "*.xcodeproj\nbuild/\nPayload/\nTemplate.ipa\nexport/\n");
    }

    private const string AppIconContents = """
{
  "images" : [
    { "filename" : "icon-1024.png", "idiom" : "universal", "platform" : "ios", "size" : "1024x1024" }
  ],
  "info" : { "author" : "xcode", "version" : 1 }
}
""";

    private const string ProjectYml = """
name: WebApp
options:
  deploymentTarget:
    iOS: "15.0"
targets:
  WebApp:
    type: application
    platform: iOS
    sources:
      - path: Sources
    settings:
      base:
        PRODUCT_NAME: WebApp
        PRODUCT_BUNDLE_IDENTIFIER: __PKG__
        MARKETING_VERSION: "__VER__"
        CURRENT_PROJECT_VERSION: "__BUILD__"
        INFOPLIST_FILE: Config/Info.plist
        GENERATE_INFOPLIST_FILE: NO
        ASSETCATALOG_COMPILER_APPICON_NAME: AppIcon
        TARGETED_DEVICE_FAMILY: "1,2"
        SWIFT_VERSION: "5.0"
""";

    private const string InfoPlist = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key><string>en</string>
    <key>CFBundleDisplayName</key><string>__NAME__</string>
    <key>CFBundleName</key><string>__NAME__</string>
    <key>CFBundleExecutable</key><string>$(EXECUTABLE_NAME)</string>
    <key>CFBundleIdentifier</key><string>$(PRODUCT_BUNDLE_IDENTIFIER)</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$(MARKETING_VERSION)</string>
    <key>CFBundleVersion</key><string>$(CURRENT_PROJECT_VERSION)</string>
    <key>LSRequiresIPhoneOS</key><true/>
    <key>ITSAppUsesNonExemptEncryption</key><false/>
    <key>UILaunchScreen</key><dict/>
    <key>UIRequiredDeviceCapabilities</key><array><string>arm64</string></array>
    <key>UISupportedInterfaceOrientations</key>
    <array>
        <string>UIInterfaceOrientationPortrait</string>
        <string>UIInterfaceOrientationLandscapeLeft</string>
        <string>UIInterfaceOrientationLandscapeRight</string>
    </array>
    <key>UISupportedInterfaceOrientations~ipad</key>
    <array>
        <string>UIInterfaceOrientationPortrait</string>
        <string>UIInterfaceOrientationPortraitUpsideDown</string>
        <string>UIInterfaceOrientationLandscapeLeft</string>
        <string>UIInterfaceOrientationLandscapeRight</string>
    </array>
    <key>NSAppTransportSecurity</key>
    <dict><key>NSAllowsArbitraryLoadsInWebContent</key><true/></dict>
    <key>NSCameraUsageDescription</key><string>This app needs the camera when a page asks for it.</string>
    <key>NSMicrophoneUsageDescription</key><string>This app needs the microphone when a page asks for it.</string>
    <key>NSPhotoLibraryUsageDescription</key><string>This app needs your photos when you upload a file.</string>
    <key>NSLocationWhenInUseUsageDescription</key><string>This app needs your location when a page asks for it.</string>
</dict>
</plist>
""";

    private const string AppDelegate = """
import UIKit

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {
    var window: UIWindow?

    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        let window = UIWindow(frame: UIScreen.main.bounds)
        window.rootViewController = WebViewController()
        window.makeKeyAndVisible()
        self.window = window
        return true
    }
}
""";

    private const string WebViewController = """
import UIKit
import WebKit

struct AppSettings: Decodable {
    var url: String
    var themeColor: String
    var pullToRefresh: Bool
    var allowZoom: Bool
    var externalLinks: Bool
    var fullscreen: Bool
    var enableJavaScript: Bool

    static func load() -> AppSettings {
        if let u = Bundle.main.url(forResource: "config", withExtension: "json"),
           let d = try? Data(contentsOf: u),
           let s = try? JSONDecoder().decode(AppSettings.self, from: d) {
            return s
        }
        return AppSettings(url: "https://example.com", themeColor: "#0A84FF", pullToRefresh: true,
                           allowZoom: false, externalLinks: true, fullscreen: false, enableJavaScript: true)
    }
}

extension UIColor {
    convenience init(hex: String) {
        var h = hex.trimmingCharacters(in: .whitespacesAndNewlines).replacingOccurrences(of: "#", with: "")
        if h.count == 3 { h = h.map { "\($0)\($0)" }.joined() }
        var v: UInt64 = 0
        Scanner(string: h).scanHexInt64(&v)
        self.init(red: CGFloat((v >> 16) & 0xFF) / 255,
                  green: CGFloat((v >> 8) & 0xFF) / 255,
                  blue: CGFloat(v & 0xFF) / 255,
                  alpha: 1)
    }

    var luminance: CGFloat {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        getRed(&r, green: &g, blue: &b, alpha: &a)
        return 0.299 * r + 0.587 * g + 0.114 * b
    }
}

final class WebViewController: UIViewController, WKNavigationDelegate, WKUIDelegate {
    private let settings = AppSettings.load()
    private var webView: WKWebView!
    private let progress = UIProgressView(progressViewStyle: .bar)
    private let refresh = UIRefreshControl()
    private var progressObserver: NSKeyValueObservation?

    private var theme: UIColor { UIColor(hex: settings.themeColor) }
    override var prefersStatusBarHidden: Bool { settings.fullscreen }
    override var preferredStatusBarStyle: UIStatusBarStyle { theme.luminance < 0.6 ? .lightContent : .darkContent }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = theme

        let config = WKWebViewConfiguration()
        config.allowsInlineMediaPlayback = true
        config.mediaTypesRequiringUserActionForPlayback = []
        config.defaultWebpagePreferences.allowsContentJavaScript = settings.enableJavaScript
        if !settings.allowZoom {
            let js = "var m=document.querySelector('meta[name=viewport]');"
                   + "if(!m){m=document.createElement('meta');m.name='viewport';document.head.appendChild(m);}"
                   + "m.content=(m.content||'width=device-width, initial-scale=1')+', maximum-scale=1, user-scalable=no';"
            config.userContentController.addUserScript(
                WKUserScript(source: js, injectionTime: .atDocumentEnd, forMainFrameOnly: true))
        }

        webView = WKWebView(frame: .zero, configuration: config)
        webView.navigationDelegate = self
        webView.uiDelegate = self
        webView.allowsBackForwardNavigationGestures = true
        webView.isOpaque = false
        webView.backgroundColor = .clear
        webView.scrollView.backgroundColor = .clear
        webView.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(webView)

        let guide = view.safeAreaLayoutGuide
        if settings.fullscreen {
            NSLayoutConstraint.activate([
                webView.topAnchor.constraint(equalTo: view.topAnchor),
                webView.bottomAnchor.constraint(equalTo: view.bottomAnchor),
                webView.leadingAnchor.constraint(equalTo: view.leadingAnchor),
                webView.trailingAnchor.constraint(equalTo: view.trailingAnchor)
            ])
        } else {
            NSLayoutConstraint.activate([
                webView.topAnchor.constraint(equalTo: guide.topAnchor),
                webView.bottomAnchor.constraint(equalTo: guide.bottomAnchor),
                webView.leadingAnchor.constraint(equalTo: guide.leadingAnchor),
                webView.trailingAnchor.constraint(equalTo: guide.trailingAnchor)
            ])
        }

        progress.translatesAutoresizingMaskIntoConstraints = false
        progress.trackTintColor = .clear
        progress.progressTintColor = theme.luminance < 0.6 ? .white : .darkGray
        view.addSubview(progress)
        NSLayoutConstraint.activate([
            progress.topAnchor.constraint(equalTo: guide.topAnchor),
            progress.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            progress.trailingAnchor.constraint(equalTo: view.trailingAnchor)
        ])
        progressObserver = webView.observe(\.estimatedProgress, options: .new) { [weak self] wv, _ in
            self?.progress.setProgress(Float(wv.estimatedProgress), animated: true)
            self?.progress.isHidden = wv.estimatedProgress >= 1.0
        }

        if settings.pullToRefresh {
            refresh.addTarget(self, action: #selector(reload), for: .valueChanged)
            webView.scrollView.refreshControl = refresh
        }

        load()
    }

    private func load() {
        if let url = URL(string: settings.url) {
            webView.load(URLRequest(url: url))
        }
    }

    @objc private func reload() {
        if webView.url == nil || webView.url?.scheme == "about" { load() } else { webView.reload() }
    }

    private func isSameSite(_ host: String?) -> Bool {
        guard let host = host?.lowercased(), let home = URL(string: settings.url)?.host?.lowercased() else { return false }
        func base(_ h: String) -> String {
            let p = h.split(separator: ".")
            return p.count >= 2 ? p[p.count - 2] + "." + p[p.count - 1] : h
        }
        return base(host) == base(home)
    }

    // MARK: WKNavigationDelegate

    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction,
                 decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        guard let url = navigationAction.request.url else { decisionHandler(.allow); return }
        let scheme = url.scheme?.lowercased() ?? ""

        if scheme == "http" || scheme == "https" {
            if settings.externalLinks, navigationAction.navigationType == .linkActivated, !isSameSite(url.host) {
                UIApplication.shared.open(url)
                decisionHandler(.cancel)
            } else {
                decisionHandler(.allow)
            }
            return
        }
        if ["about", "blob", "data", "file", "javascript"].contains(scheme) {
            decisionHandler(.allow)
            return
        }
        UIApplication.shared.open(url) // tel:, mailto:, sms:, itms-apps: ...
        decisionHandler(.cancel)
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        refresh.endRefreshing()
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        refresh.endRefreshing()
    }

    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        refresh.endRefreshing()
        if (error as NSError).code == NSURLErrorCancelled { return }
        let html = "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'></head>"
            + "<body style='font-family:-apple-system;text-align:center;padding-top:26vh;color:#555'>"
            + "<h2>No connection · اتصال برقرار نیست</h2>"
            + "<p>Check your internet connection and try again.<br>اینترنت خود را بررسی کنید و دوباره تلاش کنید.</p>"
            + "<a style='font-size:18px' href='\(settings.url)'>Retry · تلاش دوباره</a></body></html>"
        webView.loadHTMLString(html, baseURL: nil)
    }

    // MARK: WKUIDelegate

    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                 for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        if navigationAction.targetFrame == nil { webView.load(navigationAction.request) }
        return nil
    }

    func webView(_ webView: WKWebView, runJavaScriptAlertPanelWithMessage message: String,
                 initiatedByFrame frame: WKFrameInfo, completionHandler: @escaping () -> Void) {
        let a = UIAlertController(title: nil, message: message, preferredStyle: .alert)
        a.addAction(UIAlertAction(title: "OK", style: .default) { _ in completionHandler() })
        present(a, animated: true)
    }

    func webView(_ webView: WKWebView, runJavaScriptConfirmPanelWithMessage message: String,
                 initiatedByFrame frame: WKFrameInfo, completionHandler: @escaping (Bool) -> Void) {
        let a = UIAlertController(title: nil, message: message, preferredStyle: .alert)
        a.addAction(UIAlertAction(title: "OK", style: .default) { _ in completionHandler(true) })
        a.addAction(UIAlertAction(title: "Cancel", style: .cancel) { _ in completionHandler(false) })
        present(a, animated: true)
    }
}
""";

    private const string Workflow = """
name: Build iOS app

on:
  workflow_dispatch:
  push:
    branches: [ main, master ]

env:
  BUNDLE_ID: __PKG__
  HAS_CERT: ${{ secrets.IOS_P12_BASE64 != '' }}
  HAS_ASC: ${{ secrets.ASC_KEY_ID != '' }}

jobs:
  build:
    runs-on: macos-15
    steps:
      - uses: actions/checkout@v4

      - name: Install XcodeGen
        run: brew install xcodegen

      - name: Generate Xcode project
        run: xcodegen generate

      # Unsigned build -> Template.ipa. The Windows tool re-signs it and installs it on the iPhone over USB.
      - name: Build unsigned Template.ipa
        run: |
          xcodebuild -project WebApp.xcodeproj -scheme WebApp -configuration Release -sdk iphoneos \
            -derivedDataPath build CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""
          rm -rf Payload && mkdir Payload
          cp -R build/Build/Products/Release-iphoneos/WebApp.app Payload/
          zip -qry Template.ipa Payload

      - uses: actions/upload-artifact@v4
        with:
          name: Template-ipa
          path: Template.ipa

      # ---- Everything below runs only when the signing secrets exist (App Store build) ----
      - name: Import certificate and provisioning profile
        if: env.HAS_CERT == 'true'
        env:
          P12_BASE64: ${{ secrets.IOS_P12_BASE64 }}
          P12_PASSWORD: ${{ secrets.IOS_P12_PASSWORD }}
          PROFILE_BASE64: ${{ secrets.IOS_PROFILE_BASE64 }}
        run: |
          KC="$RUNNER_TEMP/build.keychain-db"
          echo "$P12_BASE64" | base64 --decode > "$RUNNER_TEMP/cert.p12"
          security create-keychain -p ci-pass "$KC"
          security set-keychain-settings -lut 21600 "$KC"
          security unlock-keychain -p ci-pass "$KC"
          security import "$RUNNER_TEMP/cert.p12" -P "$P12_PASSWORD" -A -t cert -f pkcs12 -k "$KC"
          security set-key-partition-list -S apple-tool:,apple: -k ci-pass "$KC"
          security list-keychains -d user -s "$KC" $(security list-keychains -d user | tr -d '"')
          echo "$PROFILE_BASE64" | base64 --decode > "$RUNNER_TEMP/app.mobileprovision"
          security cms -D -i "$RUNNER_TEMP/app.mobileprovision" > "$RUNNER_TEMP/profile.plist"
          UUID=$(/usr/libexec/PlistBuddy -c "Print :UUID" "$RUNNER_TEMP/profile.plist")
          mkdir -p "$HOME/Library/MobileDevice/Provisioning Profiles"
          cp "$RUNNER_TEMP/app.mobileprovision" "$HOME/Library/MobileDevice/Provisioning Profiles/$UUID.mobileprovision"
          echo "PROFILE_NAME=$(/usr/libexec/PlistBuddy -c 'Print :Name' "$RUNNER_TEMP/profile.plist")" >> "$GITHUB_ENV"
          echo "TEAM_ID=$(/usr/libexec/PlistBuddy -c 'Print :TeamIdentifier:0' "$RUNNER_TEMP/profile.plist")" >> "$GITHUB_ENV"

      - name: Archive and export for the App Store
        if: env.HAS_CERT == 'true'
        run: |
          xcodebuild archive -project WebApp.xcodeproj -scheme WebApp -configuration Release \
            -destination "generic/platform=iOS" -archivePath "$RUNNER_TEMP/WebApp.xcarchive" \
            CODE_SIGN_STYLE=Manual DEVELOPMENT_TEAM="$TEAM_ID" \
            CODE_SIGN_IDENTITY="Apple Distribution" PROVISIONING_PROFILE_SPECIFIER="$PROFILE_NAME"
          cat > "$RUNNER_TEMP/export.plist" <<EOF
          <?xml version="1.0" encoding="UTF-8"?>
          <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
          <plist version="1.0"><dict>
            <key>method</key><string>app-store-connect</string>
            <key>teamID</key><string>$TEAM_ID</string>
            <key>signingStyle</key><string>manual</string>
            <key>provisioningProfiles</key>
            <dict><key>$BUNDLE_ID</key><string>$PROFILE_NAME</string></dict>
          </dict></plist>
          EOF
          xcodebuild -exportArchive -archivePath "$RUNNER_TEMP/WebApp.xcarchive" \
            -exportOptionsPlist "$RUNNER_TEMP/export.plist" -exportPath "$GITHUB_WORKSPACE/export"

      # The whole export folder is kept: Apple Transporter on Windows also needs AppStoreInfo.plist from it.
      - uses: actions/upload-artifact@v4
        if: env.HAS_CERT == 'true'
        with:
          name: AppStore-export
          path: export/

      - name: Upload to App Store Connect / TestFlight
        if: env.HAS_CERT == 'true' && env.HAS_ASC == 'true'
        continue-on-error: true
        env:
          ASC_KEY_ID: ${{ secrets.ASC_KEY_ID }}
          ASC_ISSUER_ID: ${{ secrets.ASC_ISSUER_ID }}
          ASC_KEY_P8_BASE64: ${{ secrets.ASC_KEY_P8_BASE64 }}
        run: |
          mkdir -p "$HOME/private_keys"
          echo "$ASC_KEY_P8_BASE64" | base64 --decode > "$HOME/private_keys/AuthKey_${ASC_KEY_ID}.p8"
          xcrun altool --upload-app -f export/WebApp.ipa -t ios --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
""";
}
