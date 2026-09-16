using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NetTime.Tray;

internal static class Program
{
    private static Mutex? _mutex;

    public const int HWND_BROADCAST = 0xffff;
    public const uint MSGFLT_ADD = 1;
    public static readonly uint WM_SHOW_TRUETIME = RegisterWindowMessage("TrueTime_ShowDashboard_Message_V1");

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--screenshot")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var scNotifyIcon = new NotifyIcon { Icon = CreateClockIcon() };
            var scMainForm = new MainForm(scNotifyIcon);
            scMainForm.Show();
            Application.DoEvents();
            using (var bmp = new Bitmap(scMainForm.Width, scMainForm.Height))
            {
                scMainForm.DrawToBitmap(bmp, new Rectangle(0, 0, scMainForm.Width, scMainForm.Height));
                string outPath = args.Length > 1 ? args[1] : "screenshot_main.png";
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            scMainForm.Close();
            return;
        }

        if (args.Length > 0 && args[0] == "--screenshot-settings")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var scNotifyIcon = new NotifyIcon { Icon = CreateClockIcon() };
            var scSettings = new SettingsForm(scNotifyIcon.Icon, null, true, true);
            scSettings.Show();
            Application.DoEvents();
            using (var bmp = new Bitmap(scSettings.Width, scSettings.Height))
            {
                scSettings.DrawToBitmap(bmp, new Rectangle(0, 0, scSettings.Width, scSettings.Height));
                string outPath = args.Length > 1 ? args[1] : "screenshot_settings.png";
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            scSettings.Close();
            return;
        }

        const string mutexName = "TrueTimeTrayApp_SingleInstanceMutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running; signal it to restore and show its dashboard
            PostMessage((IntPtr)HWND_BROADCAST, WM_SHOW_TRUETIME, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        // Allow the show message through UIPI in case instances run at different privilege levels
        try { ChangeWindowMessageFilter(WM_SHOW_TRUETIME, MSGFLT_ADD); } catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        string altIconPath = Path.Combine(AppContext.BaseDirectory, "tray.ico");
        Icon appIcon;
        if (File.Exists(iconPath))
        {
            appIcon = new Icon(iconPath);
        }
        else if (File.Exists(altIconPath))
        {
            appIcon = new Icon(altIconPath);
        }
        else
        {
            appIcon = CreateClockIcon();
        }

        using var notifyIcon = new NotifyIcon();
        notifyIcon.Text = "TrueTime Professional";
        notifyIcon.Icon = appIcon;
        notifyIcon.Visible = true;

        var mainForm = new MainForm(notifyIcon);

        var contextMenu = new ContextMenuStrip();
        var itemOpen = new ToolStripMenuItem("Open TrueTime", null, (s, e) => ShowDashboard(mainForm))
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };
        var itemSync = new ToolStripMenuItem("Update Now", null, async (s, e) =>
        {
            await mainForm.TriggerSyncNowAsync();
        });
        var itemSettings = new ToolStripMenuItem("Settings...", null, (s, e) =>
        {
            ShowDashboard(mainForm);
            mainForm.OpenSettings();
        });
        var itemAbout = new ToolStripMenuItem("About", null, (s, e) =>
        {
            mainForm.OpenAbout();
        });
        var itemExit = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            notifyIcon.Visible = false;
            mainForm.AllowExit();
            Application.Exit();
        });

        contextMenu.Items.Add(itemOpen);
        contextMenu.Items.Add(itemSync);
        contextMenu.Items.Add(itemSettings);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemAbout);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemExit);

        notifyIcon.ContextMenuStrip = contextMenu;
        notifyIcon.DoubleClick += (s, e) => ShowDashboard(mainForm);

        bool startMinimized = args.Length > 0 && args[0].Equals("--minimized", StringComparison.OrdinalIgnoreCase);

        // Show dashboard initially unless explicitly started with --minimized
        if (!startMinimized)
        {
            ShowDashboard(mainForm);
        }

        Application.Run();
    }

    public static void ShowDashboard(MainForm form)
    {
        try
        {
            if (form.InvokeRequired)
            {
                form.Invoke(new Action(() => ShowDashboard(form)));
                return;
            }

            if (!form.Visible)
            {
                form.Show();
            }
            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }
            form.ShowInTaskbar = true;
            form.BringToFront();
            form.Activate();
            SetForegroundWindow(form.Handle);
        }
        catch { }
    }

    private static Icon CreateClockIcon()
    {
        // Dynamically create a 32x32 crisp clock icon so no external file dependency is needed
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Clock body circle
            using var brushBg = new SolidBrush(Color.FromArgb(37, 99, 235));
            g.FillEllipse(brushBg, 2, 2, 27, 27);

            using var penBorder = new Pen(Color.White, 2.5f);
            g.DrawEllipse(penBorder, 2, 2, 27, 27);

            // Clock hands
            using var penHand = new Pen(Color.White, 2f);
            penHand.StartCap = LineCap.Round;
            penHand.EndCap = LineCap.Round;

            // Center to 12 o'clock (hour hand)
            g.DrawLine(penHand, 15.5f, 15.5f, 15.5f, 7.5f);
            // Center to 3 o'clock (minute hand)
            g.DrawLine(penHand, 15.5f, 15.5f, 22.5f, 15.5f);

            // Center pin
            using var brushPin = new SolidBrush(Color.White);
            g.FillEllipse(brushPin, 14, 14, 3.5f, 3.5f);
        }

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
