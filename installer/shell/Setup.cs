// DUN installer window. Compiled with the C# 5 compiler that ships with Windows (.NET Framework 4.x),
// so it runs on any Windows 10/11 without extra downloads. The actual install is done by the embedded
// Inno Setup engine (installer/DUN.iss), run silently; this window shows its progress.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Dun.Setup
{
    internal static class Install
    {
        // Inno Setup's uninstall key for AppId {8F3A6C2E-...} in installer/DUN.iss.
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3A6C2E-5B1D-4E7A-9C44-2D6E1B7F0A93}_is1";

        internal static string DefaultDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DUN"); }
        }

        // Where an earlier version is installed, and which version, or null.
        internal static string[] Existing()
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var key = root.OpenSubKey(UninstallKey))
                {
                    if (key == null) continue;
                    string dir = Convert.ToString(key.GetValue("InstallLocation"));
                    if (IsValid(dir) && Directory.Exists(dir)) return new[] { dir.TrimEnd('\\'), Convert.ToString(key.GetValue("DisplayVersion")) };
                }
            }
            return null;
        }

        internal static bool IsValid(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && path.IndexOf('"') < 0
                    && path.IndexOfAny(Path.GetInvalidPathChars()) < 0
                    && Path.GetFullPath(path).TrimEnd('\\').Length > Path.GetPathRoot(Path.GetFullPath(path)).TrimEnd('\\').Length;
            }
            catch { return false; }
        }

        // DUN keeps running in the tray; it has to stop before its files can be replaced.
        internal static void StopRunningApp()
        {
            foreach (var p in Process.GetProcessesByName("DUN"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
                p.Dispose();
            }
        }

        internal static async Task<int> Run(string dir, bool autostart, bool desktop, string log, Action<int> progress)
        {
            string work = Path.Combine(Path.GetTempPath(), "DUN.Setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string engine = Path.Combine(work, "DUN-Engine.exe");
            try
            {
                await Task.Run(delegate
                {
                    using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dun.Setup.Engine"))
                    using (var output = File.Create(engine))
                    {
                        if (input == null) throw new InvalidOperationException("安装文件不完整，请重新下载安装包。");
                        input.CopyTo(output);
                    }
                    StopRunningApp();
                });
                var tasks = string.Join(",", new[] { autostart ? "autostart" : null, desktop ? "desktopicon" : null }.Where(t => t != null));
                var args = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CURRENTUSER /DIR=\"" + dir.TrimEnd('\\') + "\" /TASKS=\"" + tasks + "\" /LOG=\"" + log + "\"";
                using (var process = Process.Start(new ProcessStartInfo(engine, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = work }))
                {
                    var exit = Task.Run(delegate { process.WaitForExit(); return process.ExitCode; });
                    while (!exit.IsCompleted) { ReadProgress(log, progress); await Task.Delay(150); }
                    ReadProgress(log, progress);
                    return await exit;
                }
            }
            finally
            {
                try { File.Delete(engine); Directory.Delete(work, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        static void ReadProgress(string log, Action<int> progress)
        {
            string text;
            try
            {
                using (var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fs, Encoding.UTF8, true)) text = reader.ReadToEnd();
            }
            catch (IOException) { return; }
            int at = text.LastIndexOf("DUN_PROGRESS|", StringComparison.Ordinal);
            if (at < 0) return;
            var digits = new string(text.Substring(at + 13).TakeWhile(char.IsDigit).ToArray());
            int value;
            if (int.TryParse(digits, out value)) progress(Math.Min(100, value));
        }
    }

    internal sealed class SetupWindow
    {
        internal readonly Window Window;
        readonly string[] existing = Install.Existing();
        string state = "ready", directory;
        bool installing;
        double shown, target;
        readonly DispatcherTimer smooth = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };

        static string Version { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }

        internal SetupWindow()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dun.Setup.Window")) Window = (Window)XamlReader.Load(s);
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dun.Setup.Icon"))
                if (s != null) Window.Icon = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Window.Width = Math.Min(752, SystemParameters.WorkArea.Width - 12);
            Window.Height = Math.Min(624, SystemParameters.WorkArea.Height - 12);
            directory = existing != null ? existing[0] : Install.DefaultDirectory;
            Text("VersionLabel", "DUN  /  v" + Version);

            Find<Button>("PrimaryButton").Click += async delegate { await Primary(); };
            Find<Button>("SecondaryButton").Click += delegate { if (state == "done") Window.Close(); else Ready(); };
            Find<Button>("CloseButton").Click += delegate { Window.Close(); };
            Find<Button>("ChangeDirectoryButton").Click += delegate { ChooseDirectory(); };
            var link = Find<TextBlock>("AuthorLink");
            link.MouseEnter += delegate { link.Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x66, 0x75)); };
            link.MouseLeave += delegate { link.Foreground = new SolidColorBrush(Color.FromRgb(0xAD, 0xB5, 0xC0)); };
            link.MouseLeftButtonUp += delegate { try { Process.Start("https://github.com/sandun2027"); } catch { } };
            Find<Grid>("TitleBar").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource is TextBlock || e.OriginalSource is Grid || e.OriginalSource is StackPanel) Window.DragMove();
            };
            Window.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape && !installing) Window.Close(); };
            Window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (installing) e.Cancel = true; };
            smooth.Tick += delegate { Step(); };
            Ready();
        }

        T Find<T>(string name) where T : FrameworkElement { return (T)Window.FindName(name); }
        void Text(string name, string value) { Find<TextBlock>(name).Text = value; }

        void Panel(string name)
        {
            foreach (var p in new[] { "ReadyPanel", "ProgressPanel", "MessagePanel" })
                Find<FrameworkElement>(p).Visibility = p == name ? Visibility.Visible : Visibility.Collapsed;
            Find<Button>("SecondaryButton").Visibility = Visibility.Collapsed;
            Find<Button>("PrimaryButton").IsEnabled = true;
        }

        void Ready()
        {
            state = "ready";
            Panel("ReadyPanel");
            Text("Headline", "复制过的，都晾在这根绳上。");
            Text("Subtitle", "文字、链接、图片、文件，随时找回，一键再用。");
            Text("DirectoryLabel", directory);
            Find<Button>("ChangeDirectoryButton").Visibility = existing == null ? Visibility.Visible : Visibility.Collapsed;
            if (existing != null)
                Text("ExistingLabel", "已安装" + (string.IsNullOrEmpty(existing[1]) ? "旧版本" : " v" + existing[1]) + "，将在原位置更新，剪贴板历史会保留。");
            Find<Button>("PrimaryButton").Content = existing == null ? "安装 DUN  →" : "更新到 v" + Version + "  →";
        }

        void Message(string headline, string subtitle, string title, string detail, string button)
        {
            Panel("MessagePanel");
            Text("Headline", headline);
            Text("Subtitle", subtitle);
            Text("MessageTitle", title);
            Text("MessageDetail", detail);
            Find<Button>("PrimaryButton").Content = button;
        }

        void ChooseDirectory()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择 DUN 的安装位置", SelectedPath = Directory.Exists(directory) ? directory : "" })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var picked = dlg.SelectedPath;
                // Install into a DUN folder inside what was picked, unless it already is one.
                directory = Path.GetFileName(picked.TrimEnd('\\')).Equals("DUN", StringComparison.OrdinalIgnoreCase) ? picked : Path.Combine(picked, "DUN");
                Text("DirectoryLabel", directory);
            }
        }

        async Task Primary()
        {
            if (state == "done") { Launch(); return; }
            if (installing) return;
            if (state == "error") { Ready(); return; }
            if (!Install.IsValid(directory))
            {
                state = "error";
                Message("换个位置试试。", "为 DUN 选一个文件夹。", "安装位置无效", "请选择一个具体的文件夹，不能直接使用磁盘根目录。", "返回");
                return;
            }

            installing = true;
            state = "installing";
            shown = target = 0;
            Panel("ProgressPanel");
            var primary = Find<Button>("PrimaryButton");
            primary.IsEnabled = false;
            primary.Content = "正在安装…";
            Text("Headline", existing == null ? "正在把绳子挂好。" : "正在更新 DUN。");
            Text("Subtitle", "只需要几秒钟。");
            smooth.Start();
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DUN", "InstallerLogs");
            Directory.CreateDirectory(logDir);
            var log = Path.Combine(logDir, "install-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            int code;
            try
            {
                code = await Install.Run(directory, Find<CheckBox>("AutostartBox").IsChecked == true, Find<CheckBox>("DesktopBox").IsChecked == true, log,
                    delegate(int p) { target = Math.Max(target, p); });
            }
            catch (Exception e)
            {
                installing = false;
                smooth.Stop();
                state = "error";
                Message("还差一点。", "处理后可以直接重试。", "安装没有完成", e.Message, "重试");
                return;
            }
            installing = false;
            if (code == 0)
            {
                target = 100;
                Done();
            }
            else
            {
                smooth.Stop();
                state = "error";
                Message("还差一点。", "处理后可以直接重试。", "安装没有完成（代码 " + code + "）",
                    "请确认安装位置可以写入，然后重试。安装日志：" + log, "重试");
            }
        }

        void Done()
        {
            smooth.Stop();
            state = "done";
            Message("挂好了。", "DUN 会在托盘里安静待命。", "DUN v" + Version + " 已安装",
                "按 Ctrl+Alt+V 打开键盘模式，或者把鼠标停在屏幕顶边。复制点什么试试看。", "启动 DUN  →");
            var secondary = Find<Button>("SecondaryButton");
            secondary.Content = "完成";
            secondary.Visibility = Visibility.Visible;
        }

        void Launch()
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(directory, "DUN.exe")) { UseShellExecute = true, WorkingDirectory = directory }); } catch { }
            Window.Close();
        }

        // Ease the bar toward the engine's reported progress; creep slowly while it's busy copying.
        void Step()
        {
            if (target < 95 && shown >= target) target = Math.Min(95, target + 0.15);
            shown += (target - shown) * 0.18;
            SetProgress(shown);
        }

        void SetProgress(double value)
        {
            int p = (int)Math.Round(value);
            Find<Border>("ProgressFill").Width = 632 * value / 100;
            Text("PercentLabel", p + "%");
            Text("ProgressLabel", p < 8 ? "正在准备安装文件" : p < 90 ? "正在复制 DUN" : "正在收尾");
        }

        // --shot out.png ready|installing|done: render a state to PNG (for the README).
        internal void Shot(string path, string scene)
        {
            if (scene == "installing")
            {
                Panel("ProgressPanel");
                Text("Headline", "正在把绳子挂好。");
                Text("Subtitle", "只需要几秒钟。");
                SetProgress(64);
                Find<Button>("PrimaryButton").Content = "正在安装…";
                Find<Button>("PrimaryButton").IsEnabled = false;
            }
            else if (scene == "done") Done();
            Window.WindowStartupLocation = WindowStartupLocation.Manual;
            Window.Left = -20000;
            Window.Top = -20000;
            Window.ShowActivated = false;
            Window.Show();
            Window.Dispatcher.BeginInvoke(new Action(delegate
            {
                var rtb = new RenderTargetBitmap((int)(Window.ActualWidth * 2), (int)(Window.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
                rtb.Render(Window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(path)) encoder.Save(fs);
                Window.Close();
            }), DispatcherPriority.ApplicationIdle);
        }
    }

    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool created;
            using (new Mutex(true, @"Local\DUN.Setup", out created))
            {
                if (!created) return 1;
                var app = new Application();
                var window = new SetupWindow();
                if (args.Length >= 2 && args[0] == "--shot")
                {
                    window.Shot(Path.GetFullPath(args[1]), args.Length > 2 ? args[2] : "ready");
                    app.Run();
                    return 0;
                }
                app.Run(window.Window);
                return 0;
            }
        }
    }
}
