using System.Diagnostics;
using WebToAppStudio.Core;

namespace WebToAppStudio.UI;

public sealed class MainForm : Form
{
    private static readonly Color SideBg = Color.FromArgb(15, 23, 42);
    private static readonly Color SideActive = Color.FromArgb(30, 41, 59);
    private static readonly Color PageBg = Color.FromArgb(245, 247, 251);
    private static readonly Color Accent = Color.FromArgb(10, 132, 255);

    private readonly AppConfig _c = AppConfig.LoadOrDefault();
    private bool _fa = true;
    private bool _built;
    private bool _busy;
    private int _page;
    private CancellationTokenSource? _cts;
    private string _lastApk = "";
    private string _lastIpa = "";

    // persistent chrome
    private readonly Panel _side = new() { Width = 220, BackColor = SideBg };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = PageBg };
    private readonly Panel _logPanel = new() { Dock = DockStyle.Bottom, Height = 200, BackColor = Color.FromArgb(17, 24, 39) };
    private readonly TextBox _log = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(17, 24, 39), ForeColor = Color.FromArgb(203, 213, 225),
        Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.No
    };
    private readonly ProgressBar _bar = new() { Dock = DockStyle.Top, Height = 5, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0 };
    private readonly Button _cancel = new() { Dock = DockStyle.Right, Width = 130, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(185, 28, 28), Enabled = false };

    // rebuilt on every language switch
    private readonly List<Panel> _pages = new();
    private readonly List<Button> _nav = new();
    private TextBox _tbName = null!, _tbUrl = null!, _tbPkg = null!, _tbVer = null!, _tbIcon = null!, _tbOut = null!;
    private TextBox _tbKs = null!, _tbAlias = null!, _tbKsPass = null!;
    private TextBox _tbTemplate = null!, _tbP12 = null!, _tbP12Pass = null!, _tbProv = null!, _tbZsign = null!, _tbIdev = null!;
    private NumericUpDown _nudCode = null!;
    private Button _btnColor = null!;
    private PictureBox _pic = null!;
    private CheckBox _cbJs = null!, _cbPull = null!, _cbZoom = null!, _cbExt = null!, _cbFull = null!, _cbAab = null!, _cbLic = null!;
    private ComboBox _cmbDevice = null!;

    public MainForm()
    {
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(1000, 700);
        Size = new Size(1180, 800);
        StartPosition = FormStartPosition.CenterScreen;

        _cancel.FlatAppearance.BorderSize = 0;
        _cancel.Click += (_, _) => _cts?.Cancel();
        _logPanel.Controls.Add(_log);
        _logPanel.Controls.Add(_cancel);
        _logPanel.Controls.Add(_bar);

        Controls.Add(_content);   // Fill
        Controls.Add(_logPanel);  // Bottom
        Controls.Add(_side);      // Left / Right (docked first, full height)

        FormClosing += (_, _) =>
        {
            _cts?.Cancel();
            if (_built) { ReadForm(); _c.Save(); }
        };

        BuildUi();
        Log("WebToApp Studio ready.");
    }

    // ===================================================================== helpers

    private string L(string fa, string en) => _fa ? fa : en;

    private void Log(string s)
    {
        if (IsDisposed || !IsHandleCreated && !_built) return;
        if (InvokeRequired) { BeginInvoke((Action)(() => Log(s))); return; }
        _log.AppendText(s + Environment.NewLine);
    }

    private void SetBusy(bool busy)
    {
        _bar.MarqueeAnimationSpeed = busy ? 30 : 0;
        _cancel.Enabled = busy;
    }

    private async Task RunJob(string title, Func<CancellationToken, Task> job)
    {
        if (_busy) { Log(L("یک عملیات دیگر در حال اجراست.", "Another task is already running.")); return; }
        ReadForm();
        _c.Save();
        _busy = true;
        _cts = new CancellationTokenSource();
        SetBusy(true);
        Log("=== " + title + " ===");
        try { await job(_cts.Token); }
        catch (OperationCanceledException) { Log(L("لغو شد.", "Cancelled.")); }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, _fa ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);
        }
        finally
        {
            _busy = false;
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static void Reveal(string path)
    {
        try
        {
            if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private void Rtl(Control c) { if (_fa) c.RightToLeft = RightToLeft.Yes; }

    private Label Lbl(string text, bool bold = false, float size = 10f)
    {
        var l = new Label
        {
            Text = text, AutoSize = true, Margin = new Padding(0, 9, 8, 0),
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            BackColor = Color.Transparent
        };
        Rtl(l);
        return l;
    }

    private TextBox Tb(string text, bool password = false)
    {
        var t = new TextBox { Text = text, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 5), UseSystemPasswordChar = password };
        return t;
    }

    private Button Btn(string text, EventHandler onClick, bool primary = false, int width = 0)
    {
        var b = new Button
        {
            Text = text, AutoSize = width == 0, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 4, 14, 4), Margin = new Padding(0, 4, 8, 4),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Color.FromArgb(30, 41, 59)
        };
        if (width > 0) b.Width = width;
        b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(203, 213, 225);
        b.Click += onClick;
        Rtl(b);
        return b;
    }

    private CheckBox Chk(string text, bool value)
    {
        var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 2), BackColor = Color.Transparent };
        Rtl(c);
        return c;
    }

    private Button BrowseFile(TextBox tb, string filter)
    {
        var b = Btn("…", (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (d.ShowDialog(this) == DialogResult.OK) tb.Text = d.FileName;
        }, false, 44);
        return b;
    }

    private Button BrowseFolder(TextBox tb)
    {
        var b = Btn("…", (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = tb.Text };
            if (d.ShowDialog(this) == DialogResult.OK) tb.Text = d.SelectedPath;
        }, false, 44);
        return b;
    }

    private (Panel host, TableLayoutPanel table) NewPage(string title)
    {
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28, 20, 28, 20), BackColor = PageBg, Visible = false };
        var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, BackColor = PageBg };
        if (_fa)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        }
        else
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        host.Controls.Add(t);
        _content.Controls.Add(host);
        _pages.Add(host);
        Wide(t, Lbl(title, true, 16f));
        return (host, t);
    }

    private int NextRow(TableLayoutPanel t)
    {
        int r = t.RowCount;
        t.RowCount = r + 1;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return r;
    }

    private void Row(TableLayoutPanel t, string label, Control input, Control? extra = null)
    {
        int r = NextRow(t);
        t.Controls.Add(Lbl(label), _fa ? 2 : 0, r);
        t.Controls.Add(input, 1, r);
        if (extra != null) t.Controls.Add(extra, _fa ? 0 : 2, r);
    }

    private void Wide(TableLayoutPanel t, Control c)
    {
        int r = NextRow(t);
        t.Controls.Add(c, 0, r);
        t.SetColumnSpan(c, 3);
        if (_fa) c.Anchor = AnchorStyles.Top | AnchorStyles.Right;
    }

    private void Section(TableLayoutPanel t, string text)
    {
        var l = Lbl(text, true, 11f);
        l.Margin = new Padding(0, 18, 0, 4);
        l.ForeColor = Accent;
        Wide(t, l);
    }

    private void Note(TableLayoutPanel t, string text)
    {
        var l = Lbl(text);
        l.MaximumSize = new Size(820, 0);
        l.ForeColor = Color.FromArgb(71, 85, 105);
        l.Margin = new Padding(0, 4, 0, 4);
        Wide(t, l);
    }

    private void Buttons(TableLayoutPanel t, params Button[] buttons)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = PageBg, Margin = new Padding(0, 8, 0, 0), MaximumSize = new Size(840, 0) };
        if (_fa) f.FlowDirection = FlowDirection.RightToLeft;
        f.Controls.AddRange(buttons);
        Wide(t, f);
    }

    // ===================================================================== UI construction

    private void BuildUi()
    {
        _built = false;
        SuspendLayout();
        foreach (var ctl in _content.Controls.Cast<Control>().ToList()) ctl.Dispose();
        foreach (var ctl in _side.Controls.Cast<Control>().ToList()) ctl.Dispose();
        _pages.Clear();
        _nav.Clear();

        Text = "WebToApp Studio — " + L("تبدیل سایت به اپ اندروید و iOS", "Website to Android & iOS app");
        _side.Dock = _fa ? DockStyle.Right : DockStyle.Left;
        _cancel.Text = L("لغو عملیات", "Cancel");

        BuildSidebar();
        BuildProjectPage();
        BuildAndroidPage();
        BuildIosPage();
        BuildPublishPage();

        ShowPage(_page);
        ResumeLayout(true);
        _built = true;
        UpdatePreview();
    }

    private void BuildSidebar()
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = SideBg, Padding = new Padding(0, 8, 0, 0) };
        string[] names =
        {
            L("پروژه و تنظیمات", "Project"),
            L("اندروید (APK)", "Android (APK)"),
            L("آیفون (IPA)", "iPhone (IPA)"),
            L("انتشار در استور", "Publish")
        };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var b = new Button
            {
                Text = names[i], Width = 220, Height = 50, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = SideBg,
                TextAlign = _fa ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 18, 0), Margin = new Padding(0), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => ShowPage(index);
            _nav.Add(b);
            flow.Controls.Add(b);
        }

        var title = new Label
        {
            Text = "WebToApp\nStudio", Dock = DockStyle.Top, Height = 90, ForeColor = Color.White,
            Font = new Font("Segoe UI", 17f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter
        };
        var lang = new Button
        {
            Text = _fa ? "English" : "فارسی", Dock = DockStyle.Bottom, Height = 44, FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(148, 163, 184), BackColor = SideBg, Cursor = Cursors.Hand
        };
        lang.FlatAppearance.BorderSize = 0;
        lang.Click += (_, _) => BeginInvoke((Action)(() => { ReadForm(); _fa = !_fa; BuildUi(); }));

        _side.Controls.Add(flow);
        _side.Controls.Add(title);
        _side.Controls.Add(lang);
    }

    private void ShowPage(int index)
    {
        _page = index;
        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].Visible = i == index;
            _nav[i].BackColor = i == index ? SideActive : SideBg;
        }
    }

    private void BuildProjectPage()
    {
        var (_, t) = NewPage(L("اطلاعات اپلیکیشن", "App details"));

        _tbName = Tb(_c.AppName);
        _tbUrl = Tb(_c.WebUrl);
        _tbPkg = Tb(_c.PackageId);
        _tbVer = Tb(_c.VersionName);
        _nudCode = new NumericUpDown { Minimum = 1, Maximum = 2000000000, Value = Math.Max(1, _c.VersionCode), Dock = DockStyle.Left, Width = 140, Margin = new Padding(0, 5, 0, 5) };
        _tbIcon = Tb(_c.IconPath);
        _tbOut = Tb(_c.OutputDir);

        Section(t, L("مشخصات", "Identity"));
        Row(t, L("نام اپلیکیشن", "App name"), _tbName);
        Row(t, L("آدرس سایت", "Website URL"), _tbUrl);
        Row(t, L("شناسه بسته (Package / Bundle ID)", "Package / Bundle ID"), _tbPkg);
        Row(t, L("نسخه", "Version"), _tbVer);
        Row(t, L("کد نسخه (Build)", "Build number"), _nudCode);

        Section(t, L("ظاهر", "Look"));
        _btnColor = Btn(_c.ThemeColor, (_, _) =>
        {
            using var d = new ColorDialog { Color = ColorTranslator.FromHtml(_c.ThemeColor), FullOpen = true };
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                _c.ThemeColor = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";
                StyleColorButton();
                UpdatePreview();
            }
        }, false, 140);
        StyleColorButton();
        Row(t, L("رنگ اصلی", "Theme colour"), _btnColor);

        _tbIcon.TextChanged += (_, _) => UpdatePreview();
        Row(t, L("آیکون (PNG مربع، ترجیحاً ۱۰۲۴px)", "Icon (square PNG, ideally 1024px)"), _tbIcon, BrowseFile(_tbIcon, "Images|*.png;*.jpg;*.jpeg;*.bmp"));
        _pic = new PictureBox { Size = new Size(96, 96), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 6, 0, 6), BackColor = Color.Transparent };
        Row(t, L("پیش‌نمایش", "Preview"), _pic);

        Section(t, L("رفتار اپلیکیشن", "Behaviour"));
        _cbJs = Chk(L("فعال بودن جاوااسکریپت", "Enable JavaScript"), _c.EnableJavaScript);
        _cbPull = Chk(L("کشیدن به پایین برای رفرش", "Pull to refresh"), _c.PullToRefresh);
        _cbZoom = Chk(L("اجازه زوم کردن صفحه", "Allow pinch zoom"), _c.AllowZoom);
        _cbExt = Chk(L("لینک‌های سایت‌های دیگر در مرورگر باز شوند", "Open other websites' links in the browser"), _c.ExternalLinksInBrowser);
        _cbFull = Chk(L("تمام‌صفحه (بدون نوار وضعیت)", "Fullscreen (hide status bar)"), _c.Fullscreen);
        foreach (var cb in new[] { _cbJs, _cbPull, _cbZoom, _cbExt, _cbFull }) Wide(t, cb);

        Section(t, L("خروجی", "Output"));
        Row(t, L("پوشه خروجی", "Output folder"), _tbOut, BrowseFolder(_tbOut));
        Buttons(t, Btn(L("باز کردن پوشه خروجی", "Open output folder"), (_, _) =>
        {
            ReadForm();
            Directory.CreateDirectory(_c.OutputDir);
            Reveal(_c.OutputDir);
        }));
    }

    private void StyleColorButton()
    {
        var col = ColorTranslator.FromHtml(_c.ThemeColor);
        _btnColor.Text = _c.ThemeColor;
        _btnColor.BackColor = col;
        _btnColor.ForeColor = (0.299 * col.R + 0.587 * col.G + 0.114 * col.B) > 150 ? Color.Black : Color.White;
    }

    private void BuildAndroidPage()
    {
        var (_, t) = NewPage(L("ساخت اپلیکیشن اندروید", "Build the Android app"));

        _tbKs = Tb(_c.KeystorePath);
        _tbAlias = Tb(_c.KeyAlias);
        _tbKsPass = Tb(_c.KeystorePassword, true);
        _cbAab = Chk(L("خروجی AAB برای Google Play هم ساخته شود", "Also build an AAB for Google Play"), _c.BuildAab);
        _cbLic = Chk(L("لایسنس‌های Android SDK را می‌پذیرم", "I accept the Android SDK licences"), false);

        var jdk = ToolLocator.FindJdkHome();
        var sdk = ToolLocator.FindAndroidSdk();
        Section(t, L("ابزارهای لازم", "Required tools"));
        Note(t, "JDK 17+ : " + (jdk ?? L("پیدا نشد — Android Studio را نصب کنید", "not found — install Android Studio")));
        Note(t, "Android SDK : " + (sdk ?? L("پیدا نشد — Android Studio را یک‌بار باز کنید", "not found — open Android Studio once")));
        Note(t, "Gradle : " + L("خودکار دانلود می‌شود (یک‌بار)", "downloaded automatically (once)"));

        Section(t, L("امضای اپلیکیشن", "App signing"));
        Row(t, L("فایل Keystore (اختیاری)", "Keystore file (optional)"), _tbKs, BrowseFile(_tbKs, "Keystore|*.jks;*.keystore|All|*.*"));
        Row(t, "Alias", _tbAlias);
        Row(t, L("رمز Keystore (خالی = خودکار)", "Keystore password (blank = auto)"), _tbKsPass);
        Note(t, L("اگر Keystore ندهید، یکی ساخته می‌شود. حتماً از آن و فایل رمزش نسخه پشتیبان بگیرید؛ بدون آن نمی‌توانید اپ را آپدیت کنید.",
                  "If you give no keystore one is created. Back it up together with its password file - without it you can never update the app."));
        Wide(t, _cbAab);
        Wide(t, _cbLic);

        Buttons(t,
            Btn(L("ساخت APK", "Build APK"), async (_, _) => await RunJob("Android", async ct =>
            {
                var r = await AndroidBuilder.BuildAsync(_c, _cbLic.Checked, Log, ct);
                _lastApk = r.ApkPath;
                Log(L("اپ اندروید ساخته شد: ", "Android app built: ") + r.ApkPath);
                if (r.AabPath != null) Log("AAB: " + r.AabPath);
                Reveal(r.ApkPath);
            }), true),
            Btn(L("نصب روی گوشی اندروید (USB)", "Install on Android phone (USB)"), async (_, _) => await RunJob("adb", async ct =>
            {
                var apk = File.Exists(_lastApk) ? _lastApk : Path.Combine(_c.OutputDir, $"{_c.SafeName}-{_c.VersionName}.apk");
                if (!File.Exists(apk)) { Log(L("ابتدا APK را بسازید.", "Build the APK first.")); return; }
                Log(L("USB debugging را در گوشی فعال کنید.", "Enable USB debugging on the phone."));
                bool ok = await AndroidBuilder.InstallViaAdbAsync(apk, Log, ct);
                Log(ok ? L("روی گوشی نصب شد.", "Installed on the phone.") : L("نصب ناموفق بود.", "Install failed."));
            })),
            Btn(L("بررسی دوباره ابزارها", "Re-check tools"), (_, _) => BeginInvoke((Action)(() => { ReadForm(); BuildUi(); }))));
    }

    private void BuildIosPage()
    {
        var (_, t) = NewPage(L("ساخت اپلیکیشن iOS", "Build the iOS app"));

        _tbTemplate = Tb(_c.TemplateIpaPath);
        _tbP12 = Tb(_c.P12Path);
        _tbP12Pass = Tb(_c.P12Password, true);
        _tbProv = Tb(_c.ProvisionPath);
        _tbZsign = Tb(_c.ZsignPath);
        _tbIdev = Tb(_c.IdeviceInstallerPath);
        _cmbDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 5) };

        Note(t, L("ویندوز نمی‌تواند کد Swift را کامپایل کند. روش کار: ۱) سورس iOS را خروجی بگیرید و یک‌بار در GitHub Actions (مک ابری) کامپایل کنید تا Template.ipa ساخته شود. ۲) این برنامه Template.ipa را با تنظیمات شما و گواهی خودتان امضا می‌کند و با کابل روی آیفون نصب می‌کند.",
                  "Windows cannot compile Swift. How it works: 1) export the iOS source and let GitHub Actions (a cloud Mac) compile it once into Template.ipa. 2) This app writes your settings into it, signs it with your certificate and installs it on the iPhone over the USB cable."));

        Section(t, L("مرحله ۱ — سورس iOS", "Step 1 — iOS source"));
        Buttons(t, Btn(L("خروجی سورس iOS (برای GitHub)", "Export iOS source (for GitHub)"), async (_, _) => await RunJob("iOS source", ct =>
        {
            var errors = _c.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            var dir = Path.Combine(_c.OutputDir, "ios-source");
            IosSourceExporter.Export(_c, dir);
            Log(L("سورس iOS آماده شد: ", "iOS source ready: ") + dir);
            Log(L("این پوشه را در یک مخزن GitHub بگذارید و Actions > Build iOS app > Run workflow را بزنید؛ سپس Template.ipa را از Artifacts دانلود کنید.",
                  "Put this folder in a GitHub repository, run Actions > Build iOS app > Run workflow, then download Template.ipa from Artifacts."));
            Reveal(dir);
            return Task.CompletedTask;
        }), true));

        Section(t, L("مرحله ۲ — امضا", "Step 2 — Signing"));
        Row(t, "Template.ipa", _tbTemplate, BrowseFile(_tbTemplate, "IPA|*.ipa"));
        Row(t, L("گواهی (.p12)", "Certificate (.p12)"), _tbP12, BrowseFile(_tbP12, "P12|*.p12"));
        Row(t, L("رمز گواهی", "Certificate password"), _tbP12Pass);
        Row(t, L("پروفایل (.mobileprovision)", "Profile (.mobileprovision)"), _tbProv, BrowseFile(_tbProv, "Profile|*.mobileprovision"));
        Row(t, L("مسیر zsign.exe (خالی = خودکار)", "zsign.exe path (blank = auto)"), _tbZsign, BrowseFile(_tbZsign, "Executable|*.exe"));
        Row(t, L("مسیر ideviceinstaller.exe (خالی = خودکار)", "ideviceinstaller.exe path (blank = auto)"), _tbIdev, BrowseFile(_tbIdev, "Executable|*.exe"));
        Note(t, "zsign : " + (ToolLocator.Resolve(_c.ZsignPath, "zsign") ?? L("پیدا نشد", "not found"))
              + "   |   ideviceinstaller : " + (ToolLocator.Resolve(_c.IdeviceInstallerPath, "ideviceinstaller") ?? L("پیدا نشد", "not found")));
        Buttons(t, Btn(L("ساخت IPA امضاشده", "Build signed IPA"), async (_, _) => await RunJob("iOS IPA", async ct =>
        {
            _lastIpa = await IosIpaBuilder.BuildSignedAsync(_c, Log, ct);
            Reveal(_lastIpa);
        }), true));

        Section(t, L("مرحله ۳ — نصب مستقیم روی آیفون با کابل", "Step 3 — Install on the iPhone via cable"));
        Row(t, L("دستگاه", "Device"), _cmbDevice);
        Buttons(t,
            Btn(L("پیدا کردن آیفون", "Detect iPhone"), async (_, _) => await RunJob("iPhone", async ct =>
            {
                var list = await DeviceInstaller.ListAsync(_c, Log, ct);
                _cmbDevice.Items.Clear();
                foreach (var d in list) _cmbDevice.Items.Add(d);
                if (_cmbDevice.Items.Count > 0) { _cmbDevice.SelectedIndex = 0; Log(L("آیفون پیدا شد.", "iPhone found.")); }
            })),
            Btn(L("نصب روی آیفون", "Install on iPhone"), async (_, _) => await RunJob("iPhone install", async ct =>
            {
                if (_cmbDevice.SelectedItem is not IosDevice dev) { Log(L("ابتدا «پیدا کردن آیفون» را بزنید.", "Click \"Detect iPhone\" first.")); return; }
                var ipa = File.Exists(_lastIpa) ? _lastIpa : Path.Combine(_c.OutputDir, $"{_c.SafeName}-{_c.VersionName}-signed.ipa");
                if (!File.Exists(ipa)) { Log(L("ابتدا IPA امضاشده را بسازید.", "Build the signed IPA first.")); return; }
                await DeviceInstaller.InstallAsync(_c, dev.Udid, ipa, Log, ct);
            }), true));
    }

    private void BuildPublishPage()
    {
        var (_, t) = NewPage(L("انتشار در استورها", "Publishing"));

        Section(t, "App Store (iOS)");
        Note(t, L(
            "۱) حساب Apple Developer (سالانه ۹۹ دلار) و ساخت App ID با همان Bundle ID.\n" +
            "۲) گواهی Apple Distribution (.p12) و پروفایل App Store (.mobileprovision) از developer.apple.com.\n" +
            "۳) پوشه ios-source را در GitHub بگذارید و در Settings > Secrets مقادیر IOS_P12_BASE64، IOS_P12_PASSWORD و IOS_PROFILE_BASE64 را (به‌صورت base64) اضافه کنید.\n" +
            "۴) Workflow را اجرا کنید؛ خروجی AppStore-export شامل IPA و AppStoreInfo.plist است.\n" +
            "۵) آپلود: یا خودکار (با افزودن ASC_KEY_ID، ASC_ISSUER_ID و ASC_KEY_P8_BASE64 به Secrets) یا با برنامه Apple Transporter (نسخه ویندوز).\n" +
            "۶) در App Store Connect اطلاعات، اسکرین‌شات‌ها و Privacy را کامل و برای Review ارسال کنید.\n" +
            "توجه: اپل اپ‌هایی را که فقط یک سایت ساده‌اند معمولاً رد می‌کند (Guideline 4.2)؛ قابلیت واقعی مثل اعلان، ورود، یا امکانات بومی اضافه کنید.",
            "1) Apple Developer account (99 USD/year) and an App ID with the same Bundle ID.\n" +
            "2) Apple Distribution certificate (.p12) and App Store provisioning profile from developer.apple.com.\n" +
            "3) Put the ios-source folder on GitHub and add the secrets IOS_P12_BASE64, IOS_P12_PASSWORD and IOS_PROFILE_BASE64 (base64 encoded).\n" +
            "4) Run the workflow; the AppStore-export artifact contains the IPA and AppStoreInfo.plist.\n" +
            "5) Upload: automatically (add ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8_BASE64 secrets) or with Apple Transporter for Windows.\n" +
            "6) In App Store Connect complete the listing, screenshots and privacy answers, then submit for review.\n" +
            "Note: Apple often rejects apps that are only a plain website (Guideline 4.2) - add real app value such as notifications, login or native features."));
        Buttons(t,
            Btn("App Store Connect", (_, _) => OpenUrl("https://appstoreconnect.apple.com"), true),
            Btn("Apple Developer", (_, _) => OpenUrl("https://developer.apple.com/account")));

        Section(t, "Google Play (Android)");
        Note(t, L(
            "۱) حساب Google Play Console (پرداخت یک‌باره ۲۵ دلار).\n" +
            "۲) در صفحه اندروید گزینه «خروجی AAB» را فعال کنید و فایل .aab را آپلود کنید.\n" +
            "۳) با Keystore ثابت امضا کنید و از آن نسخه پشتیبان نگه دارید.",
            "1) Google Play Console account (one-time 25 USD).\n" +
            "2) Tick \"Also build an AAB\" on the Android page and upload the .aab file.\n" +
            "3) Always sign with the same keystore and keep a backup of it."));
        Buttons(t,
            Btn("Google Play Console", (_, _) => OpenUrl("https://play.google.com/console"), true),
            Btn(L("باز کردن پوشه خروجی", "Open output folder"), (_, _) => { ReadForm(); Directory.CreateDirectory(_c.OutputDir); Reveal(_c.OutputDir); }));
    }

    // ===================================================================== form <-> config

    private void ReadForm()
    {
        if (!_built) return;
        _c.AppName = _tbName.Text.Trim();
        _c.WebUrl = _tbUrl.Text.Trim();
        _c.PackageId = _tbPkg.Text.Trim();
        _c.VersionName = _tbVer.Text.Trim();
        _c.VersionCode = (int)_nudCode.Value;
        _c.IconPath = _tbIcon.Text.Trim();
        _c.OutputDir = _tbOut.Text.Trim();
        _c.EnableJavaScript = _cbJs.Checked;
        _c.PullToRefresh = _cbPull.Checked;
        _c.AllowZoom = _cbZoom.Checked;
        _c.ExternalLinksInBrowser = _cbExt.Checked;
        _c.Fullscreen = _cbFull.Checked;

        _c.KeystorePath = _tbKs.Text.Trim();
        _c.KeyAlias = _tbAlias.Text.Trim();
        _c.KeystorePassword = _tbKsPass.Text;
        _c.BuildAab = _cbAab.Checked;

        _c.TemplateIpaPath = _tbTemplate.Text.Trim();
        _c.P12Path = _tbP12.Text.Trim();
        _c.P12Password = _tbP12Pass.Text;
        _c.ProvisionPath = _tbProv.Text.Trim();
        _c.ZsignPath = _tbZsign.Text.Trim();
        _c.IdeviceInstallerPath = _tbIdev.Text.Trim();
    }

    private void UpdatePreview()
    {
        if (!_built || _pic == null) return;
        try
        {
            _c.IconPath = _tbIcon.Text.Trim();
            _c.AppName = _tbName.Text.Trim();
            var old = _pic.Image;
            _pic.Image = IconService.Render(_c, 96, IconKind.Legacy);
            old?.Dispose();
        }
        catch { /* preview is cosmetic only */ }
    }
}
