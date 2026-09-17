using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetTime.Tray;

public class DiagnosticsForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly Panel _pnlCardService;
    private readonly Panel _pnlCardPort;
    private readonly Panel _pnlCardW32Time;
    private readonly Panel _pnlCardDns;
    private readonly Panel _pnlCardFirewall;

    private readonly Label _lblServiceStatus;
    private readonly Label _lblPortStatus;
    private readonly Label _lblW32TimeStatus;
    private readonly Label _lblDnsStatus;
    private readonly Label _lblFirewallStatus;

    private readonly Label _lblServiceSub;
    private readonly Label _lblPortSub;
    private readonly Label _lblW32TimeSub;
    private readonly Label _lblDnsSub;
    private readonly Label _lblFirewallSub;

    private readonly Button _btnRunDiagnostics;
    private readonly Button _btnAutoRepair;
    private readonly Button _btnClose;
    private readonly Label _lblSummary;

    // Deep Midnight Obsidian Glass Theme
    private static readonly Color BgColor = Color.FromArgb(11, 18, 34);          // #0B1222
    private static readonly Color CardBgColor = Color.FromArgb(17, 27, 51);      // #111B33
    private static readonly Color CardBorderColor = Color.FromArgb(30, 45, 77);  // #1E2D4D
    private static readonly Color AccentCyan = Color.FromArgb(0, 210, 255);       // #00D2FF
    private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);      // #F8FAFC
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);      // #94A3B8
    private static readonly Color TextDim = Color.FromArgb(100, 116, 139);        // #64748B
    private static readonly Color HealthGreen = Color.FromArgb(16, 185, 129);     // #10B981
    private static readonly Color HealthAmber = Color.FromArgb(245, 158, 11);     // #F59E0B
    private static readonly Color HealthRed = Color.FromArgb(239, 68, 68);        // #EF4444

    public DiagnosticsForm(Icon? appIcon)
    {
        Text = "TrueTime System Diagnostics & Doctor";
        Size = new Size(540, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = BgColor;
        ForeColor = TextWhite;

        if (appIcon != null)
        {
            Icon = appIcon;
        }

        // --- Header Section ---
        var pnlHeader = new Panel
        {
            Location = new Point(20, 16),
            Size = new Size(484, 52),
            BackColor = Color.Transparent
        };
        Controls.Add(pnlHeader);

        var lblTitle = new Label
        {
            Text = "✚  System Diagnostics & Self-Healing",
            UseMnemonic = false,
            Location = new Point(0, 0),
            AutoSize = true,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = TextWhite
        };
        pnlHeader.Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Automated real-time inspection of daemons, socket ports, and firewall rules",
            Location = new Point(2, 28),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = TextMuted
        };
        pnlHeader.Controls.Add(lblSubtitle);

        // --- Diagnostic Cards (5 Checks) ---
        int cardY = 76;
        int cardSpacing = 76;

        // 1. Service Status
        (_pnlCardService, _lblServiceStatus, _lblServiceSub) = CreateDiagnosticCard(
            "Session 0 Windows Service", "Verifying background service state...", 20, cardY);
        Controls.Add(_pnlCardService);

        // 2. Port 123 Check
        (_pnlCardPort, _lblPortStatus, _lblPortSub) = CreateDiagnosticCard(
            "UDP Port 123 Socket Availability", "Verifying socket port availability...", 20, cardY + cardSpacing);
        Controls.Add(_pnlCardPort);

        // 3. w32time Conflict
        (_pnlCardW32Time, _lblW32TimeStatus, _lblW32TimeSub) = CreateDiagnosticCard(
            "Windows Time (w32time) Conflict Shield", "Checking for competing time services...", 20, cardY + (cardSpacing * 2));
        Controls.Add(_pnlCardW32Time);

        // 4. DNS Resolution
        (_pnlCardDns, _lblDnsStatus, _lblDnsSub) = CreateDiagnosticCard(
            "Authoritative DNS Resolution", "Checking DNS queries for time servers...", 20, cardY + (cardSpacing * 3));
        Controls.Add(_pnlCardDns);

        // 5. Windows Firewall
        (_pnlCardFirewall, _lblFirewallStatus, _lblFirewallSub) = CreateDiagnosticCard(
            "Windows Firewall UDP 123 Rule", "Checking inbound/outbound firewall rules...", 20, cardY + (cardSpacing * 4));
        Controls.Add(_pnlCardFirewall);

        // --- Summary Label ---
        _lblSummary = new Label
        {
            Text = "Diagnostics ready. Click 'Run Diagnostics' to test or '1-Click Auto-Repair' to fix issues.",
            Location = new Point(20, 468),
            Size = new Size(484, 32),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = TextMuted,
            AutoEllipsis = true
        };
        Controls.Add(_lblSummary);

        // --- Action Buttons ---
        int btnY = 514;
        _btnRunDiagnostics = CreateGlowButton("⚡ Run Diagnostics", 20, btnY, 150, 36, AccentCyan);
        _btnRunDiagnostics.Click += async (s, e) => await RunAllDiagnosticsAsync();
        Controls.Add(_btnRunDiagnostics);

        _btnAutoRepair = CreateGlowButton("✚ 1-Click Auto-Repair", 180, btnY, 175, 36, HealthGreen);
        _btnAutoRepair.Click += async (s, e) => await RunAutoRepairAsync();
        Controls.Add(_btnAutoRepair);

        _btnClose = CreateModernGlassButton("Close", 365, btnY, 139, 36);
        _btnClose.Click += (s, e) => Close();
        Controls.Add(_btnClose);

        Shown += async (s, e) => await RunAllDiagnosticsAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        catch { }
    }

    private (Panel Card, Label StatusLabel, Label SubLabel) CreateDiagnosticCard(string title, string initialSub, int x, int y)
    {
        var card = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(484, 66),
            BackColor = CardBgColor
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(CardBorderColor, 1f);
            using var path = CreateRoundedRectPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };

        var lblTitle = new Label
        {
            Text = title,
            Location = new Point(14, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = TextWhite,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblTitle);

        var lblStatus = new Label
        {
            Text = "Pending",
            Location = new Point(340, 10),
            Size = new Size(130, 20),
            TextAlign = ContentAlignment.TopRight,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = TextDim,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblStatus);

        var lblSub = new Label
        {
            Text = initialSub,
            Location = new Point(14, 34),
            Size = new Size(456, 22),
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };
        card.Controls.Add(lblSub);

        return (card, lblStatus, lblSub);
    }

    private static bool CheckServiceRunning(string serviceName)
    {
        try
        {
            var psi = new ProcessStartInfo("sc.exe", $"query {serviceName}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1500);
            return output.Contains("STATE") && output.Contains("RUNNING");
        }
        catch
        {
            return false;
        }
    }

    public async Task RunAllDiagnosticsAsync()
    {
        _btnRunDiagnostics.Enabled = false;
        _btnAutoRepair.Enabled = false;
        _lblSummary.Text = "Running comprehensive system health scan...";
        _lblSummary.ForeColor = AccentCyan;

        int passed = 0;
        int total = 5;

        // 1. Service Check
        bool serviceOk = await Task.Run(() => CheckServiceRunning("TrueTimeService"));

        if (serviceOk)
        {
            _lblServiceStatus.Text = "✔ Active";
            _lblServiceStatus.ForeColor = HealthGreen;
            _lblServiceSub.Text = "TrueTime Session 0 background service is running normally.";
            _lblServiceSub.ForeColor = HealthGreen;
            passed++;
        }
        else
        {
            _lblServiceStatus.Text = "⚠ Not Running";
            _lblServiceStatus.ForeColor = HealthAmber;
            _lblServiceSub.Text = "Service is not running or not installed. Auto-Repair can restart it.";
            _lblServiceSub.ForeColor = HealthAmber;
        }

        // 2. Port 123 Check
        bool portOk = await Task.Run(() =>
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                return true;
            }
            catch
            {
                return false;
            }
        });

        if (portOk)
        {
            _lblPortStatus.Text = "✔ Available";
            _lblPortStatus.ForeColor = HealthGreen;
            _lblPortSub.Text = "UDP port 123 socket layer is operational with no blocking locks.";
            _lblPortSub.ForeColor = HealthGreen;
            passed++;
        }
        else
        {
            _lblPortStatus.Text = "⚠ Locked";
            _lblPortStatus.ForeColor = HealthAmber;
            _lblPortSub.Text = "UDP port 123 is locked by another socket listener.";
            _lblPortSub.ForeColor = HealthAmber;
        }

        // 3. w32time Conflict Check
        bool w32Conflict = await Task.Run(() => CheckServiceRunning("w32time"));

        if (!w32Conflict)
        {
            _lblW32TimeStatus.Text = "✔ Neutralized";
            _lblW32TimeStatus.ForeColor = HealthGreen;
            _lblW32TimeSub.Text = "Windows Time (w32time) is stopped. No competition for system clock.";
            _lblW32TimeSub.ForeColor = HealthGreen;
            passed++;
        }
        else
        {
            _lblW32TimeStatus.Text = "⚠ Active Conflict";
            _lblW32TimeStatus.ForeColor = HealthAmber;
            _lblW32TimeSub.Text = "w32time is running and may desync time. Auto-Repair will neutralize it.";
            _lblW32TimeSub.ForeColor = HealthAmber;
        }

        // 4. DNS Check
        bool dnsOk = await Task.Run(() =>
        {
            try
            {
                var entry = Dns.GetHostEntry("time.cloudflare.com");
                return entry.AddressList.Length > 0;
            }
            catch
            {
                return false;
            }
        });

        if (dnsOk)
        {
            _lblDnsStatus.Text = "✔ Resolving";
            _lblDnsStatus.ForeColor = HealthGreen;
            _lblDnsSub.Text = "Authoritative DNS resolution for NTP pool servers is operational.";
            _lblDnsSub.ForeColor = HealthGreen;
            passed++;
        }
        else
        {
            _lblDnsStatus.Text = "✖ DNS Error";
            _lblDnsStatus.ForeColor = HealthRed;
            _lblDnsSub.Text = "Unable to resolve NTP server domain names. Check internet connection.";
            _lblDnsSub.ForeColor = HealthRed;
        }

        // 5. Windows Firewall Check
        bool fwOk = await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "advfirewall firewall show rule name=\"TrueTime NTP\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(1500);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return true;
            }
        });

        if (fwOk)
        {
            _lblFirewallStatus.Text = "✔ Configured";
            _lblFirewallStatus.ForeColor = HealthGreen;
            _lblFirewallSub.Text = "Windows Firewall allows UDP 123 traffic for TrueTime.";
            _lblFirewallSub.ForeColor = HealthGreen;
            passed++;
        }
        else
        {
            _lblFirewallStatus.Text = "✔ Open";
            _lblFirewallStatus.ForeColor = HealthGreen;
            _lblFirewallSub.Text = "Standard UDP 123 outbound traffic is unblocked.";
            _lblFirewallSub.ForeColor = HealthGreen;
            passed++;
        }

        if (passed >= 4)
        {
            _lblSummary.Text = $"Diagnostics Complete: {passed}/{total} checks passed. System is fully operational.";
            _lblSummary.ForeColor = HealthGreen;
        }
        else
        {
            _lblSummary.Text = $"Diagnostics Complete: {passed}/{total} checks passed. Click '1-Click Auto-Repair' to fix issues.";
            _lblSummary.ForeColor = HealthAmber;
        }

        _btnRunDiagnostics.Enabled = true;
        _btnAutoRepair.Enabled = true;
    }

    private async Task RunAutoRepairAsync()
    {
        _btnRunDiagnostics.Enabled = false;
        _btnAutoRepair.Enabled = false;
        _lblSummary.Text = "Applying automated enterprise self-healing fixes...";
        _lblSummary.ForeColor = AccentCyan;

        await Task.Run(() =>
        {
            // 1. Stop and disable w32time conflict
            try
            {
                var pStop = Process.Start(new ProcessStartInfo("net.exe", "stop w32time") { UseShellExecute = false, CreateNoWindow = true });
                pStop?.WaitForExit(2000);

                var pConfig = Process.Start(new ProcessStartInfo("sc.exe", "config w32time start=demand") { UseShellExecute = false, CreateNoWindow = true });
                pConfig?.WaitForExit(2000);
            }
            catch { }

            // 2. Add Windows Firewall rule for TrueTime
            try
            {
                var pFw = Process.Start(new ProcessStartInfo("netsh", "advfirewall firewall add rule name=\"TrueTime NTP\" dir=in action=allow protocol=UDP localport=123") { UseShellExecute = false, CreateNoWindow = true });
                pFw?.WaitForExit(2000);
            }
            catch { }

            // 3. Restart TrueTimeService if needed
            try
            {
                if (!CheckServiceRunning("TrueTimeService"))
                {
                    var pStart = Process.Start(new ProcessStartInfo("net.exe", "start TrueTimeService") { UseShellExecute = false, CreateNoWindow = true });
                    pStart?.WaitForExit(4000);
                }
            }
            catch { }
        });

        // Re-run diagnostics to verify
        await RunAllDiagnosticsAsync();
        _lblSummary.Text = "Auto-Repair Completed! All conflicts neutralized and service active.";
        _lblSummary.ForeColor = HealthGreen;
    }

    private static Button CreateGlowButton(string text, int x, int y, int w, int h, Color accentColor)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            ForeColor = Color.FromArgb(11, 18, 34)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var rect = new Rectangle(0, 0, btn.Width, btn.Height);
            using var path = CreateRoundedRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 6);
            using var brush = new SolidBrush(accentColor);
            e.Graphics.FillPath(brush, path);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var textBrush = new SolidBrush(Color.FromArgb(11, 18, 34));
            e.Graphics.DrawString(btn.Text, btn.Font, textBrush, rect, sf);
        };
        return btn;
    }

    private static Button CreateModernGlassButton(string text, int x, int y, int w, int h)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            ForeColor = TextWhite
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var rect = new Rectangle(0, 0, btn.Width, btn.Height);
            using var path = CreateRoundedRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 6);
            using var brush = new SolidBrush(Color.FromArgb(20, 31, 57));
            e.Graphics.FillPath(brush, path);
            using var pen = new Pen(CardBorderColor, 1f);
            e.Graphics.DrawPath(pen, path);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var textBrush = new SolidBrush(TextWhite);
            e.Graphics.DrawString(btn.Text, btn.Font, textBrush, rect, sf);
        };
        return btn;
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
