using GHelper.Peripherals;
using GHelper.Peripherals.Mouse;
using System.Globalization;
using System.Reflection;

namespace GHelper
{
    /// <summary>
    /// Mouse-only build. The tray icon is the sole entry point: clicking it
    /// opens the settings window of a connected ASUS mouse directly.
    /// </summary>
    static class Program
    {
        public static NotifyIcon trayIcon;

        // Main entry point for the application
        public static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.WriteLine("Unhandled: " + e.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (s, e) => { Logger.WriteLine("Unobserved: " + e.Exception); e.SetObserved(); };

            string language = AppConfig.GetString("language");
            try
            {
                if (language != null && language.Length > 0)
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                else
                {
                    var culture = CultureInfo.CurrentUICulture;
                    if (culture.ToString() == "kr") culture = CultureInfo.GetCultureInfo("ko");
                    Thread.CurrentThread.CurrentUICulture = culture;
                }
            }
            catch
            {
                Logger.WriteLine("Unknown Language: " + language);
            }

            Logger.WriteLine("----------------------");
            Logger.WriteLine("Mouse-only build launched: " + AppConfig.GetModel() + " :" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + CultureInfo.CurrentUICulture);

            Application.EnableVisualStyles();

            trayIcon = new NotifyIcon
            {
                Text = "ASUS Mouse",
                Icon = Properties.Resources.standard,
                Visible = true,
                ContextMenuStrip = BuildTrayMenu()
            };

            var trayRetry = new System.Windows.Forms.Timer { Interval = 5000 };
            trayRetry.Tick += (_, _) => { trayRetry.Dispose(); trayIcon.Visible = false; trayIcon.Visible = true; };
            trayRetry.Start();

            trayIcon.MouseClick += TrayIcon_MouseClick;

            Task.Run(() => PeripheralsProvider.DetectAllAsusMice());
            PeripheralsProvider.RegisterForDeviceEvents();

            Application.ApplicationExit += OnExit;
            Application.Run();
        }

        private static ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();
            var open = menu.Items.Add("Open mouse settings");
            open.Click += (_, _) => TrayIcon_MouseClick(null, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            menu.Items.Add(new ToolStripSeparator());
            var exit = menu.Items.Add("Exit");
            exit.Click += (_, _) => Application.Exit();
            return menu;
        }

        static void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var mice = PeripheralsProvider.SnapshotMice();
            if (mice.Count == 0)
            {
                trayIcon.ShowBalloonTip(2000, "ASUS Mouse", "No ASUS mouse detected", ToolTipIcon.Info);
                return;
            }

            // If exactly one mouse is connected, open it directly. If several,
            // show a small menu so the user can pick which one to configure.
            if (mice.Count == 1)
            {
                OpenMouseSettings(mice[0]);
                return;
            }

            var menu = new ContextMenuStrip();
            foreach (var mouse in mice)
            {
                var item = menu.Items.Add(mouse.GetDisplayName());
                item.Click += (_, _) => OpenMouseSettings(mouse);
            }
            menu.Show(Cursor.Position);
        }

        private static void OpenMouseSettings(AsusMouse mouse)
        {
            try
            {
                var form = new AsusMouseSettings(mouse);
                form.Show();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Open mouse settings failed: " + ex.Message);
            }
        }

        static void OnExit(object sender, EventArgs e)
        {
            if (trayIcon is not null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            PeripheralsProvider.UnregisterForDeviceEvents();
        }
    }
}
