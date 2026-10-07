using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace AlexaDesktop
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Contains("--selftest")) return MainForm.SelfTest();

            using var mutex = new Mutex(true, @"Local\AlexaDesktop.Instance", out bool first);
            using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\AlexaDesktop.Show");
            if (!first)
            {
                // Already running: hand it the foreground and ask it to show itself.
                Native.AllowSetForegroundWindow(-1);
                showSignal.Set();
                return 0;
            }

            // Lets installer upgrades (and crash recovery) bring the app back, into the tray.
            Native.RegisterApplicationRestart("--background", 0);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(showSignal, startHidden: args.Contains("--background")));
            return 0;
        }
    }

    sealed class MainForm : Form
    {
        const string Home = "https://alexa.amazon.com/";
        const string SettingsKey = @"Software\AlexaDesktop";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "AlexaDesktop";
        // ponytail: fixed shortcuts; add a settings dialog if people report clashes.
        const int HotkeyToggle = 1, HotkeySpeak = 2;

        static readonly Regex AmazonHost = new Regex(
            @"(^|\.)amazon\.(com|ca|com\.mx|com\.br|co\.uk|de|fr|it|es|nl|se|pl|com\.tr|ae|sa|eg|in|co\.jp|com\.au|sg|com\.be|ie|co\.za)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlexaDesktop");

        readonly WebView2 web = new WebView2 { Dock = DockStyle.Fill };
        readonly NotifyIcon tray;
        bool startHidden, quitting;

        public MainForm(EventWaitHandle showSignal, bool startHidden)
        {
            this.startHidden = startHidden;
            Text = "Alexa";
            Icon = new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("icon.ico"));
            MinimumSize = new Size(420, 520);
            Size = new Size(1100, 820);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = web.DefaultBackgroundColor = SystemUsesDarkMode() ? Color.FromArgb(14, 18, 24) : Color.White;
            Controls.Add(web);
            LoadWindowBounds();

            tray = new NotifyIcon
            {
                Icon = new Icon(Icon, SystemInformation.SmallIconSize),
                Text = "Alexa",
                Visible = true,
                ContextMenuStrip = BuildTrayMenu(),
            };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };

            // Creating the handle now lets hotkeys and the second-instance signal work while hidden in the tray.
            var hwnd = Handle;
            const uint mods = Native.MOD_WIN | Native.MOD_ALT | Native.MOD_NOREPEAT;
            if (!Native.RegisterHotKey(hwnd, HotkeyToggle, mods, (uint)Keys.A) | !Native.RegisterHotKey(hwnd, HotkeySpeak, mods, (uint)Keys.V))
                tray.ShowBalloonTip(5000, "Alexa", "Another app already uses Win+Alt+A or Win+Alt+V, so that shortcut is off.", ToolTipIcon.Warning);

            ThreadPool.RegisterWaitForSingleObject(showSignal, (_, __) =>
            {
                try { BeginInvoke((Action)ShowWindow); } catch (InvalidOperationException) { } // closing down
            }, null, Timeout.Infinite, false);

            // Start once the message loop runs, so a startup error can close the window cleanly.
            BeginInvoke((Action)(() => _ = InitWebViewAsync()));
        }

        async Task InitWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(DataDir, "WebView2"));
                await web.EnsureCoreWebView2Async(env);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                if (MessageBox.Show("Alexa needs the Microsoft Edge WebView2 Runtime, which is missing on this PC.\n\nOpen the download page?",
                        "Alexa", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                    OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/");
                Quit();
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Alexa could not start its web view:\n\n" + ex.Message, "Alexa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Quit();
                return;
            }

            var core = web.CoreWebView2;
            core.Settings.IsStatusBarEnabled = false;
            core.PermissionRequested += OnPermissionRequested;
            core.NewWindowRequested += OnNewWindowRequested;
            core.NavigationStarting += OnNavigationStarting;
            core.Navigate(Home);
        }

        // Voice, location, notifications and paste are granted to Amazon without a prompt;
        // anything else gets WebView2's normal permission prompt.
        void OnPermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            if (IsAmazon(e.Uri) && (e.PermissionKind is CoreWebView2PermissionKind.Microphone
                    or CoreWebView2PermissionKind.Geolocation
                    or CoreWebView2PermissionKind.Notifications
                    or CoreWebView2PermissionKind.ClipboardRead))
                e.State = CoreWebView2PermissionState.Allow;
        }

        // Links that open a new tab go to the default browser, except Alexa itself and Amazon sign-in.
        void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (!IsWebUrl(e.Uri, out var uri)) return; // e.g. about:blank keeps WebView2's default popup
            e.Handled = true;
            if (IsAmazon(e.Uri) && (uri.Host.Equals("alexa.amazon.com", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.StartsWith("/ap/")))
                web.CoreWebView2.Navigate(e.Uri);
            else
                OpenExternal(e.Uri);
        }

        // Amazon pages (the app, sign-in, sign-out) stay in the window; any other site opens in the default browser.
        void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!IsWebUrl(e.Uri, out _) || IsAmazon(e.Uri)) return;
            e.Cancel = true;
            OpenExternal(e.Uri);
        }

        static bool IsWebUrl(string url, out Uri uri) =>
            Uri.TryCreate(url, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        static bool IsAmazon(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && AmazonHost.IsMatch(uri.Host);

        static void OpenExternal(string url)
        {
            if (IsWebUrl(url, out var uri)) Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }

        ContextMenuStrip BuildTrayMenu()
        {
            var autostart = new ToolStripMenuItem("Start with Windows") { Checked = AutoStart, CheckOnClick = true };
            autostart.CheckedChanged += (s, e) => AutoStart = autostart.Checked;
            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Open Alexa", null, (s, e) => ShowWindow()) { ShortcutKeyDisplayString = "Win+Alt+A" },
                new ToolStripMenuItem("Speak to Alexa", null, (s, e) => Speak()) { ShortcutKeyDisplayString = "Win+Alt+V" },
                new ToolStripMenuItem("Reload", null, (s, e) => web.CoreWebView2?.Navigate(Home)),
                new ToolStripSeparator(),
                autostart,
                new ToolStripSeparator(),
                new ToolStripMenuItem("Quit", null, (s, e) => Quit()),
            });
            return menu;
        }

        void ShowWindow()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            web.Focus();
        }

        void ToggleWindow()
        {
            if (Visible && WindowState != FormWindowState.Minimized && ActiveForm == this) { Hide(); return; }
            ShowWindow();
            _ = web.CoreWebView2?.ExecuteScriptAsync("document.querySelector('[placeholder=\"Ask Alexa\"]')?.focus()");
        }

        // Presses the web app's mic button. If Amazon renames it, this just opens the window.
        void Speak()
        {
            ShowWindow();
            _ = web.CoreWebView2?.ExecuteScriptAsync("document.querySelector('[aria-label=\"Speech to Text\"]')?.click()");
        }

        void Quit()
        {
            quitting = true;
            Close();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                if ((int)m.WParam == HotkeyToggle) ToggleWindow();
                else Speak();
            }
            base.WndProc(ref m);
        }

        // Started with Windows (--background): stay in the tray until summoned.
        protected override void SetVisibleCore(bool value)
        {
            if (startHidden) { startHidden = false; value = false; }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = SystemUsesDarkMode() ? 1 : 0;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveWindowBounds();
            if (!quitting && e.CloseReason == CloseReason.UserClosing)
            {
                // The close button hides to the tray, like other chat apps; Quit lives in the tray menu.
                e.Cancel = true;
                Hide();
                ShowTrayHintOnce();
                return;
            }
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        void ShowTrayHintOnce()
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
            if (key.GetValue("TrayHintShown") != null) return;
            key.SetValue("TrayHintShown", 1);
            tray.ShowBalloonTip(5000, "Alexa is still running", "Open it from the tray icon or with Win+Alt+A. Quit from the tray menu.", ToolTipIcon.Info);
        }

        void LoadWindowBounds()
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
            var parts = (key?.GetValue("WindowBounds") as string)?.Split(',');
            if (parts?.Length != 5 || !parts.All(p => int.TryParse(p, out _))) return;
            var n = parts.Select(int.Parse).ToArray();
            var rect = new Rectangle(n[0], n[1], n[2], n[3]);
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect))) return; // that monitor is gone
            StartPosition = FormStartPosition.Manual;
            Bounds = rect;
            if (n[4] == 1) WindowState = FormWindowState.Maximized;
        }

        void SaveWindowBounds()
        {
            var r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
            key.SetValue("WindowBounds", $"{r.X},{r.Y},{r.Width},{r.Height},{(WindowState == FormWindowState.Maximized ? 1 : 0)}");
        }

        static bool AutoStart
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(RunValue) != null;
            }
            set
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) key.SetValue(RunValue, $"\"{Application.ExecutablePath}\" --background");
                else key.DeleteValue(RunValue, false);
            }
        }

        static bool SystemUsesDarkMode()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }

        // Run with --selftest (CI does): checks which URLs get Amazon-only treatment.
        internal static int SelfTest()
        {
            var cases = new (string Url, bool Amazon)[]
            {
                ("https://alexa.amazon.com/", true),
                ("https://www.amazon.com/ap/signin?openid.return_to=x", true),
                ("https://www.amazon.co.uk/ap/signin", true),
                ("https://amazon.com.br/", true),
                ("http://alexa.amazon.com/", false),
                ("https://evilamazon.com/", false),
                ("https://amazon.com.evil.io/", false),
                ("https://alexa.amazon.com.attacker.net/", false),
                ("https://example.com/?u=amazon.com", false),
                ("not a url", false),
            };
            var failed = cases.Where(c => IsAmazon(c.Url) != c.Amazon).ToList();
            foreach (var c in failed) Console.Error.WriteLine($"FAIL {c.Url}: expected {c.Amazon}");
            return failed.Count == 0 ? 0 : 1;
        }
    }

    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const uint MOD_ALT = 0x1, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int dwProcessId);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterApplicationRestart(string commandLine, int flags);
    }
}
