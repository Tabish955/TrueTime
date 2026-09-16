using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTimeService.Models;

namespace NetTime.Tray;

public class MainForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _statusPollTimer;

    // Header Controls
    private readonly PictureBox _picAppLogo;
    private readonly Label _lblHeaderTitle;
    private readonly Label _lblHeaderSubtitle;
    private readonly Panel _pnlHealthBadge;
    private readonly Label _lblHealthBadgeMain;
    private readonly Label _lblHealthBadgeSub;

    // Dual Dashboard Cards
    private readonly Panel _pnlClockCard;
    private readonly Label _lblClockTime;
    private readonly Label _lblClockDateUtc;

    private readonly Panel _pnlSyncCard;
    private readonly Label _lblSyncOffset;
    private readonly Label _lblSyncNextAndServer;

    // Server Table
    private readonly Label _lblTableTitle;
    private readonly Label _lblTableSubtitle;
    private readonly Panel _pnlServerListContainer;
    private readonly Panel _pnlServerListHeader;
    private readonly ServerListControl _serverList;

    // Bottom Action Bar Controls
    private readonly Label _lblStatusNote;
    private readonly Label _lblServiceStatus;
    private readonly Button _btnSyncNow;
    private readonly Button _btnAuditLog;
    private readonly Button _btnSettings;
    private readonly Button _btnAbout;

    // State Tracking
    private TimeSyncSnapshot? _lastSnapshot;
    private DateTime _lastAttemptTime = DateTime.MinValue;
    private int _pollIntervalMinutes = 15;
    private bool _minimizeOnClose = true;
    private bool _showBalloons = true;
    private bool _allowExit = false;
    private bool _isUpdating = false;
    private int _consecutiveFailures = 0;

    // Palette: Deep Midnight Obsidian Glass
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

    public MainForm(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;

        // Double buffering for smooth flicker-free rendering
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;

        // --- Window Settings ---
        Text = "TrueTime Professional v1.0.0";
        Size = new Size(584, 615);
        MinimumSize = new Size(584, 615);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = BgColor;
        ForeColor = TextWhite;
        Icon = notifyIcon.Icon;

        // ==========================================
        // 1. TOP HEADER & HEALTH BADGE
        // ==========================================
        var pnlHeader = new Panel
        {
            Location = new Point(18, 12),
            Size = new Size(532, 42),
            BackColor = Color.Transparent
        };
        Controls.Add(pnlHeader);

        // Neon Logo PictureBox
        _picAppLogo = new PictureBox
        {
            Location = new Point(0, 2),
            Size = new Size(38, 38),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LogoProvider.GetAppLogo(),
            BackColor = Color.Transparent
        };
        pnlHeader.Controls.Add(_picAppLogo);

        // Header Title (Dual-colored custom paint)
        _lblHeaderTitle = new Label
        {
            Location = new Point(44, 1),
            Size = new Size(330, 24),
            BackColor = Color.Transparent
        };
        _lblHeaderTitle.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var fontBold = new Font("Segoe UI", 13.5F, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, "TrueTime ", fontBold, new Point(0, 0), Color.White);
            int offset = TextRenderer.MeasureText(e.Graphics, "TrueTime ", fontBold).Width - 8;
            TextRenderer.DrawText(e.Graphics, "Professional", fontBold, new Point(offset, 0), AccentCyan);
        };
        pnlHeader.Controls.Add(_lblHeaderTitle);

        _lblHeaderSubtitle = new Label
        {
            Text = "Precision Multi-Server Network Time Synchronization",
            Location = new Point(45, 24),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = TextMuted,
            BackColor = Color.Transparent
        };
        pnlHeader.Controls.Add(_lblHeaderSubtitle);

        // Right-side Health Badge Container
        _pnlHealthBadge = new Panel
        {
            Location = new Point(394, 2),
            Size = new Size(138, 38),
            BackColor = Color.FromArgb(4, 47, 46) // Deep emerald glass
        };
        _pnlHealthBadge.Paint += DrawHealthBadgeContainer;
        pnlHeader.Controls.Add(_pnlHealthBadge);

        _lblHealthBadgeMain = new Label
        {
            Text = "✔ In Sync",
            Location = new Point(6, 4),
            Size = new Size(126, 16),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = HealthGreen,
            BackColor = Color.Transparent
        };
        _pnlHealthBadge.Controls.Add(_lblHealthBadgeMain);

        _lblHealthBadgeSub = new Label
        {
            Text = "Last sync: Initializing...",
            Location = new Point(4, 20),
            Size = new Size(130, 14),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 7F, FontStyle.Regular),
            ForeColor = Color.FromArgb(110, 231, 183),
            BackColor = Color.Transparent
        };
        _pnlHealthBadge.Controls.Add(_lblHealthBadgeSub);

        // ==========================================
        // 2. DUAL BALANCED DASHBOARD CARDS
        // ==========================================
        int cardY = 60;
        int cardW = 260;
        int cardH = 88;

        // --- Card 1: System Time ---
        _pnlClockCard = CreateModernGlassCard(18, cardY, cardW, cardH);
        Controls.Add(_pnlClockCard);

        var lblClockHeader = new Label
        {
            Text = "🕒  Current System Time",
            Location = new Point(14, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = TextMuted,
            BackColor = Color.Transparent
        };
        _pnlClockCard.Controls.Add(lblClockHeader);

        _lblClockTime = new Label
        {
            Text = DateTime.Now.ToString("hh:mm:ss tt"),
            Location = new Point(12, 28),
            AutoSize = true,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = TextWhite,
            BackColor = Color.Transparent
        };
        _pnlClockCard.Controls.Add(_lblClockTime);

        string tzName = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) 
            ? TimeZoneInfo.Local.DaylightName 
            : TimeZoneInfo.Local.StandardName;
        string tzAbbr = new string(tzName.Split(' ').Where(w => w.Length > 0).Select(w => w[0]).ToArray());
        if (tzAbbr.Length > 4) tzAbbr = "Local";

        _lblClockDateUtc = new Label
        {
            Text = $"{DateTime.Now:ddd, dd MMM yyyy}  ({tzAbbr})",
            Location = new Point(14, 62),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = TextDim,
            BackColor = Color.Transparent
        };
        _pnlClockCard.Controls.Add(_lblClockDateUtc);

        // --- Card 2: Precision & Accuracy ---
        _pnlSyncCard = CreateModernGlassCard(290, cardY, cardW, cardH);
        Controls.Add(_pnlSyncCard);

        var lblSyncHeader = new Label
        {
            Text = "🎯  Time Accuracy",
            Location = new Point(14, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = TextMuted,
            BackColor = Color.Transparent
        };
        _pnlSyncCard.Controls.Add(lblSyncHeader);

        _lblSyncOffset = new Label
        {
            Text = "Synchronizing...",
            Location = new Point(12, 28),
            AutoSize = true,
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = HealthGreen,
            BackColor = Color.Transparent
        };
        _pnlSyncCard.Controls.Add(_lblSyncOffset);

        _lblSyncNextAndServer = new Label
        {
            Text = "vs. NTP pool average",
            Location = new Point(14, 62),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = TextDim,
            BackColor = Color.Transparent
        };
        _pnlSyncCard.Controls.Add(_lblSyncNextAndServer);

        // ==========================================
        // 3. NTP SERVER POOL TABLE SECTION
        // ==========================================
        int tableTopY = 156;
        var pnlTableBar = new Panel
        {
            Location = new Point(18, tableTopY),
            Size = new Size(532, 22),
            BackColor = Color.Transparent
        };
        Controls.Add(pnlTableBar);

        _lblTableTitle = new Label
        {
            Text = "🖧  NTP Server Pool",
            Location = new Point(0, 0),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = TextWhite
        };
        pnlTableBar.Controls.Add(_lblTableTitle);

        _lblTableSubtitle = new Label
        {
            Text = "7 authoritative servers • 15m polling interval",
            Location = new Point(270, 2),
            Size = new Size(262, 18),
            TextAlign = ContentAlignment.TopRight,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = TextDim
        };
        pnlTableBar.Controls.Add(_lblTableSubtitle);

        // Server List Container
        int listY = 182;
        int listW = 532;
        int listH = 278;

        _pnlServerListContainer = new Panel
        {
            Location = new Point(18, listY),
            Size = new Size(listW, listH),
            BackColor = Color.FromArgb(13, 21, 39)
        };
        _pnlServerListContainer.Paint += (s, e) =>
        {
            using var pen = new Pen(CardBorderColor, 1f);
            using var path = CreateRoundedRectPath(new Rectangle(0, 0, _pnlServerListContainer.Width - 1, _pnlServerListContainer.Height - 1), 6);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };
        Controls.Add(_pnlServerListContainer);

        // Header inside list container
        _pnlServerListHeader = new Panel
        {
            Location = new Point(1, 1),
            Size = new Size(listW - 2, 26),
            BackColor = Color.FromArgb(17, 27, 51)
        };
        _pnlServerListHeader.Paint += (s, e) =>
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var font = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var brush = new SolidBrush(TextDim);
            e.Graphics.DrawString("Server", font, brush, 16, 6);
            e.Graphics.DrawString("Latency", font, brush, 350, 6);
            e.Graphics.DrawString("Status", font, brush, 452, 6);

            using var pen = new Pen(Color.FromArgb(24, 37, 66), 1f);
            e.Graphics.DrawLine(pen, 0, 25, _pnlServerListHeader.Width, 25);
        };
        _pnlServerListContainer.Controls.Add(_pnlServerListHeader);

        // Custom Owner-Drawn Server List
        _serverList = new ServerListControl
        {
            Location = new Point(1, 28),
            Size = new Size(listW - 2, listH - 30),
            BackColor = Color.FromArgb(13, 21, 39)
        };
        _pnlServerListContainer.Controls.Add(_serverList);

        // Pre-populate with default servers so the table displays immediately with brand logos
        var initialServers = new List<ServerSyncDetail>
        {
            new ServerSyncDetail { Server = "time.cloudflare.com", Success = true, RoundTripMs = 12.4, OffsetMs = 0.2, Stratum = 1 },
            new ServerSyncDetail { Server = "time.google.com", Success = true, RoundTripMs = 18.2, OffsetMs = -0.5, Stratum = 1 },
            new ServerSyncDetail { Server = "time.facebook.com", Success = true, RoundTripMs = 24.1, OffsetMs = 0.8, Stratum = 1 },
            new ServerSyncDetail { Server = "time.apple.com", Success = true, RoundTripMs = 28.5, OffsetMs = -1.2, Stratum = 1 },
            new ServerSyncDetail { Server = "time.windows.com", Success = true, RoundTripMs = 32.0, OffsetMs = 1.4, Stratum = 1 },
            new ServerSyncDetail { Server = "pool.ntp.org", Success = true, RoundTripMs = 35.6, OffsetMs = 0.1, Stratum = 2 },
            new ServerSyncDetail { Server = "time.nist.gov", Success = true, RoundTripMs = 45.2, OffsetMs = -0.9, Stratum = 1 }
        };
        _serverList.SetServers(initialServers, "time.cloudflare.com", false);

        // ==========================================
        // 4. ACTION BAR & FOOTER
        // ==========================================
        int actionY = 470;
        int btnH = 36;

        // 1. Sync Button (Vibrant Cyan Gradient Pill)
        _btnSyncNow = new Button
        {
            Text = "🔄  Sync",
            Location = new Point(18, actionY),
            Size = new Size(124, btnH),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        _btnSyncNow.FlatAppearance.BorderSize = 0;
        _btnSyncNow.Paint += DrawCyanGlowButton;
        _btnSyncNow.Click += async (s, e) => await TriggerSyncNowAsync();
        Controls.Add(_btnSyncNow);

        // 2. Audit Log Button
        _btnAuditLog = CreateModernGlassButton("📄  Log", 152, actionY, 120, btnH);
        _btnAuditLog.Click += (s, e) => OpenAuditLog();
        Controls.Add(_btnAuditLog);

        // 3. Settings Button
        _btnSettings = CreateModernGlassButton("⚙  Settings", 282, actionY, 126, btnH);
        _btnSettings.Click += (s, e) => OpenSettings();
        Controls.Add(_btnSettings);

        // 4. About Button
        _btnAbout = CreateModernGlassButton("ℹ  About", 418, actionY, 132, btnH);
        _btnAbout.Click += (s, e) => OpenAbout();
        Controls.Add(_btnAbout);

        // Footer status labels
        int footerY = 516;
        _lblStatusNote = new Label
        {
            Text = "Initializing background synchronization service...",
            Location = new Point(20, footerY),
            Size = new Size(530, 16),
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = TextMuted,
            AutoEllipsis = true
        };
        Controls.Add(_lblStatusNote);

        _lblServiceStatus = new Label
        {
            Text = "● Service: Active  •  LAN NTP: Active",
            Location = new Point(20, footerY + 18),
            Size = new Size(530, 16),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = HealthGreen
        };
        Controls.Add(_lblServiceStatus);

        // ==========================================
        // 5. TIMERS & EVENT LISTENERS
        // ==========================================
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();

        _statusPollTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _statusPollTimer.Tick += async (s, e) => await RefreshStatusAsync();

        try
        {
            NetworkChange.NetworkAvailabilityChanged += (s, ev) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => { _ = RefreshStatusAsync(); }));
                }
            };
        }
        catch { }

        _statusPollTimer.Start();
        _ = RefreshStatusAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Enable Windows 10 / 11 Native Immersive Dark Mode for Title Bar
        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                int darkMode = 1;
                int res = DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                if (res != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref darkMode, sizeof(int));
                }
            }
        }
        catch { }
    }

    private static Panel CreateModernGlassCard(int x, int y, int width, int height)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = CardBgColor
        };
        panel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectPath(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8);
            using var pen = new Pen(CardBorderColor, 1.2f);
            e.Graphics.DrawPath(pen, path);
        };
        return panel;
    }

    private static Button CreateModernGlassButton(string text, int x, int y, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.FromArgb(19, 31, 56),
            ForeColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(34, 51, 86);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 45, 79);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(14, 23, 43);
        return btn;
    }

    private void DrawCyanGlowButton(object? sender, PaintEventArgs e)
    {
        var btn = (Button)sender!;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, btn.Width, btn.Height);

        using var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 210, 255), Color.FromArgb(2, 132, 199), LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, rect);

        TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, rect, Color.FromArgb(10, 25, 47),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void DrawHealthBadgeContainer(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, _pnlHealthBadge.Width - 1, _pnlHealthBadge.Height - 1);
        using var path = CreateRoundedRectPath(rect, 8);
        using var brush = new SolidBrush(_pnlHealthBadge.BackColor);
        e.Graphics.FillPath(brush, path);

        Color borderColor = _pnlHealthBadge.BackColor == Color.FromArgb(63, 18, 18) ? HealthRed : HealthGreen;
        using var pen = new Pen(borderColor, 1.2f);
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        _lblClockTime.Text = DateTime.Now.ToString("hh:mm:ss tt");

        string tzName = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) 
            ? TimeZoneInfo.Local.DaylightName 
            : TimeZoneInfo.Local.StandardName;
        string tzAbbr = new string(tzName.Split(' ').Where(w => w.Length > 0).Select(w => w[0]).ToArray());
        if (tzAbbr.Length > 4) tzAbbr = "Local";

        _lblClockDateUtc.Text = $"{DateTime.Now:ddd, dd MMM yyyy}  ({tzAbbr})";

        bool networkAvail = NetworkInterface.GetIsNetworkAvailable();
        bool isNoInternet = !networkAvail || (_lastSnapshot != null && (!_lastSnapshot.NetworkOnline || (!_lastSnapshot.InSync && string.IsNullOrEmpty(_lastSnapshot.SelectedServer))));

        if (isNoInternet)
        {
            DateTime nextAttempt = _lastSnapshot != null && _lastSnapshot.NextSyncTime != DateTime.MinValue
                ? _lastSnapshot.NextSyncTime
                : (_lastAttemptTime != DateTime.MinValue ? _lastAttemptTime.AddSeconds(60) : DateTime.Now.AddSeconds(60));

            var remaining = nextAttempt - DateTime.Now;
            if (remaining > TimeSpan.Zero)
            {
                string timeStr = remaining.TotalMinutes >= 1
                    ? $"{(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s"
                    : $"{remaining.Seconds}s";

                _lblSyncNextAndServer.Text = $"No internet. Trying again in {timeStr}...";
                _lblStatusNote.Text = $"No internet connection detected. Trying again in {timeStr}...";
            }
            else
            {
                _lblSyncNextAndServer.Text = "No internet. Retrying connection now...";
                _lblStatusNote.Text = "No internet. Retrying connection now...";
                if (!_isUpdating)
                {
                    _ = RefreshStatusAsync();
                }
            }
        }
        else if (_lastAttemptTime != DateTime.MinValue)
        {
            DateTime nextAttempt = _lastSnapshot != null && _lastSnapshot.NextSyncTime != DateTime.MinValue
                ? _lastSnapshot.NextSyncTime
                : _lastAttemptTime.AddMinutes(_pollIntervalMinutes > 0 ? _pollIntervalMinutes : 15);

            var remaining = nextAttempt - DateTime.Now;
            string serverPart = !string.IsNullOrEmpty(_lastSnapshot?.SelectedServer)
                ? $"  •  {_lastSnapshot.SelectedServer}"
                : "";

            if (remaining > TimeSpan.Zero)
            {
                string timeStr = remaining.TotalHours >= 1
                    ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m {remaining.Seconds:D2}s"
                    : $"{remaining.Minutes}m {remaining.Seconds:D2}s";

                _lblSyncNextAndServer.Text = $"Next check: in {timeStr}{serverPart}";
            }
            else
            {
                _lblSyncNextAndServer.Text = $"Next check: Synchronizing...{serverPart}";
            }
        }
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            var snapshot = await PipeClient.GetStatusAsync();
            if (snapshot != null)
            {
                _consecutiveFailures = 0;
                UpdateUI(snapshot);
            }
            else
            {
                if (_isUpdating) return;

                _consecutiveFailures++;
                if (_consecutiveFailures >= 3)
                {
                    SetHealthBadge(false, "✖ Offline", "Service unreachable");
                    _lblStatusNote.Text = "TrueTime Service is offline or not reachable via Named Pipe.";
                    _lblStatusNote.ForeColor = HealthRed;
                    _lblServiceStatus.Text = "● Service: Offline";
                    _lblServiceStatus.ForeColor = HealthRed;
                    _lblSyncOffset.Text = "Service Offline";
                    _lblSyncOffset.ForeColor = HealthRed;
                }
            }
        }
        catch { }
    }

    public async Task TriggerSyncNowAsync()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        _btnSyncNow.Enabled = false;
        _btnSyncNow.Text = "⚡ Syncing...";
        SetHealthBadge(true, "⚡ Syncing...", "Querying NTP pool...");
        _lblStatusNote.Text = "Querying authoritative NTP servers concurrently over UDP 123...";
        _lblStatusNote.ForeColor = AccentCyan;

        try
        {
            var snapshot = await PipeClient.TriggerSyncAsync();
            if (snapshot != null)
            {
                _consecutiveFailures = 0;
                UpdateUI(snapshot);

                if (_showBalloons && Math.Abs(snapshot.OffsetMs) > snapshot.ThresholdMilliseconds)
                {
                    _notifyIcon.ShowBalloonTip(3000, "TrueTime", $"Clock adjusted by {snapshot.OffsetMs:+0.0;-0.0;0.0} ms via {snapshot.SelectedServer}.", ToolTipIcon.Info);
                }
            }
        }
        finally
        {
            _isUpdating = false;
            _btnSyncNow.Enabled = true;
            _btnSyncNow.Text = "🔄  Sync";
        }
    }

    private void UpdateUI(TimeSyncSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _lastAttemptTime = snapshot.LastAttemptTime != DateTime.MinValue
            ? snapshot.LastAttemptTime
            : (snapshot.LastSyncTime != DateTime.MinValue ? snapshot.LastSyncTime : _lastAttemptTime == DateTime.MinValue ? DateTime.Now : _lastAttemptTime);
        _pollIntervalMinutes = snapshot.PollIntervalMinutes > 0 ? snapshot.PollIntervalMinutes : 15;

        bool networkAvail = NetworkInterface.GetIsNetworkAvailable();
        bool isNoInternet = !networkAvail || !snapshot.NetworkOnline || (!snapshot.InSync && string.IsNullOrEmpty(snapshot.SelectedServer));

        if (isNoInternet)
        {
            _lblSyncOffset.Text = "No Internet Connection";
            _lblSyncOffset.ForeColor = HealthRed;

            DateTime nextAttempt = snapshot.NextSyncTime != DateTime.MinValue
                ? snapshot.NextSyncTime
                : _lastAttemptTime.AddSeconds(snapshot.RetryIntervalSeconds > 0 ? snapshot.RetryIntervalSeconds : 60);
            var remaining = nextAttempt - DateTime.Now;
            string timeStr = remaining > TimeSpan.Zero
                ? (remaining.TotalMinutes >= 1 ? $"{(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s" : $"{remaining.Seconds}s")
                : "0s";

            SetHealthBadge(false, "✖ No Internet", $"Retry in {timeStr}");

            _lblSyncNextAndServer.Text = remaining > TimeSpan.Zero
                ? $"No internet. Trying again in {timeStr}..."
                : "No internet. Retrying connection now...";
            _lblStatusNote.Text = remaining > TimeSpan.Zero
                ? $"No internet connection. Retrying authoritative servers in {timeStr}..."
                : "No internet. Retrying connection now...";
            _lblStatusNote.ForeColor = HealthRed;
        }
        else
        {
            string offsetText = $"{snapshot.OffsetMs:+0.0;-0.0;0.0} ms";
            string metricsText = "";
            if (snapshot.PoolJitterMs > 0 || Math.Abs(snapshot.CrystalPpm) > 0.01)
            {
                metricsText = $"  |  Jitter: {snapshot.PoolJitterMs:F1}ms  |  Drift: {snapshot.CrystalPpm:+0.0;-0.0;0.0} PPM";
            }

            if (snapshot.InSync && !string.IsNullOrEmpty(snapshot.SelectedServer) && Math.Abs(snapshot.OffsetMs) <= snapshot.ThresholdMilliseconds)
            {
                _lblSyncOffset.Text = $"Accurate ({offsetText})";
                _lblSyncOffset.ForeColor = HealthGreen;
                string syncTimeStr = snapshot.LastSyncTime != DateTime.MinValue ? snapshot.LastSyncTime.ToString("h:mm:ss tt") : "Just now";
                SetHealthBadge(true, "✔ In Sync", $"Last sync: {syncTimeStr}");
                _lblStatusNote.Text = $"System clock accurate within {snapshot.ThresholdMilliseconds} ms via {snapshot.SelectedServer}.{metricsText}";
                _lblStatusNote.ForeColor = HealthGreen;
            }
            else
            {
                _lblSyncOffset.Text = $"Drift: {offsetText}";
                _lblSyncOffset.ForeColor = HealthAmber;
                SetHealthBadge(false, "● Drift Detected", $"Offset: {offsetText}");
                _lblStatusNote.Text = $"Time drift of {offsetText} detected. Adjusting clock...{metricsText}";
                _lblStatusNote.ForeColor = HealthAmber;
            }
        }

        string lanNtpStatus = snapshot.LocalNtpServerRunning ? "  •  LAN NTP: Active" : "";
        _lblServiceStatus.Text = $"● Service: Active{lanNtpStatus}";
        _lblServiceStatus.ForeColor = HealthGreen;

        if (snapshot.ConfiguredServers != null && snapshot.ConfiguredServers.Count > 0)
        {
            _lblTableSubtitle.Text = $"{snapshot.ConfiguredServers.Count} servers • {snapshot.PollIntervalMinutes}m polling interval";
        }

        // Update Server List Control with brand logos
        _serverList.SetServers(snapshot.Servers, snapshot.SelectedServer, isNoInternet);
    }

    private void SetHealthBadge(bool ok, string mainText, string subText)
    {
        _lblHealthBadgeMain.Text = mainText;
        _lblHealthBadgeSub.Text = subText;

        if (mainText.Contains("Syncing"))
        {
            _pnlHealthBadge.BackColor = Color.FromArgb(12, 35, 64);
            _lblHealthBadgeMain.ForeColor = AccentCyan;
            _lblHealthBadgeSub.ForeColor = Color.FromArgb(186, 230, 253);
        }
        else if (ok)
        {
            _pnlHealthBadge.BackColor = Color.FromArgb(4, 47, 46);
            _lblHealthBadgeMain.ForeColor = HealthGreen;
            _lblHealthBadgeSub.ForeColor = Color.FromArgb(110, 231, 183);
        }
        else if (mainText.Contains("Drift"))
        {
            _pnlHealthBadge.BackColor = Color.FromArgb(59, 35, 8);
            _lblHealthBadgeMain.ForeColor = HealthAmber;
            _lblHealthBadgeSub.ForeColor = Color.FromArgb(253, 230, 138);
        }
        else
        {
            _pnlHealthBadge.BackColor = Color.FromArgb(63, 18, 18);
            _lblHealthBadgeMain.ForeColor = HealthRed;
            _lblHealthBadgeSub.ForeColor = Color.FromArgb(254, 202, 202);
        }
        _pnlHealthBadge.Invalidate();
    }

    public void OpenSettings()
    {
        using var form = new SettingsForm(Icon, _lastSnapshot, _minimizeOnClose, _showBalloons);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _minimizeOnClose = form.MinimizeOnClose;
            _showBalloons = form.ShowBalloons;
            _ = RefreshStatusAsync();
        }
    }

    public void OpenAuditLog()
    {
        using var form = new AuditLogForm(Icon, _lastSnapshot?.History);
        form.ShowDialog(this);
    }

    public void OpenAbout()
    {
        using var form = new AboutForm(Icon);
        form.ShowDialog(this);
    }

    private void HandleClose()
    {
        if (_minimizeOnClose)
        {
            Hide();
        }
        else
        {
            Close();
        }
    }

    public void AllowExit()
    {
        _allowExit = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Program.WM_SHOW_TRUETIME)
        {
            Program.ShowDashboard(this);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit && e.CloseReason == CloseReason.UserClosing && _minimizeOnClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
}

/// <summary>
/// Custom owner-drawn server pool list with server brand logos, 2-line title/hostname, latency, and status dots.
/// </summary>
public class ServerListControl : UserControl
{
    private readonly List<ServerSyncDetail> _servers = new();
    private string? _selectedServer;
    private bool _isNoInternet;

    public ServerListControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        AutoScroll = true;
    }

    public void SetServers(List<ServerSyncDetail>? servers, string? selectedServer, bool isNoInternet)
    {
        _servers.Clear();
        if (servers != null)
        {
            _servers.AddRange(servers);
        }
        _selectedServer = selectedServer;
        _isNoInternet = isNoInternet;

        int rowH = 35;
        int totalH = _servers.Count * rowH;
        AutoScrollMinSize = totalH > Height ? new Size(0, totalH) : Size.Empty;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int rowH = 35;
        int startY = AutoScrollPosition.Y;

        using var fontBrand = new Font("Segoe UI", 9F, FontStyle.Bold);
        using var fontHost = new Font("Segoe UI", 7F, FontStyle.Regular);
        using var fontMetrics = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        using var fontStatus = new Font("Segoe UI", 8F, FontStyle.Bold);

        using var brushWhite = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var brushMuted = new SolidBrush(Color.FromArgb(148, 163, 184));
        using var brushGreen = new SolidBrush(Color.FromArgb(16, 185, 129));
        using var brushCyan = new SolidBrush(Color.FromArgb(0, 210, 255));
        using var brushRed = new SolidBrush(Color.FromArgb(239, 68, 68));
        using var brushDisabled = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var penSeparator = new Pen(Color.FromArgb(21, 32, 57), 1f);

        for (int i = 0; i < _servers.Count; i++)
        {
            var s = _servers[i];
            int y = startY + (i * rowH);
            if (y + rowH < 0 || y > Height) continue;

            bool isFastest = string.Equals(s.Server, _selectedServer, StringComparison.OrdinalIgnoreCase);

            // Subtle row background highlight for fastest active server
            if (isFastest)
            {
                using var activeBrush = new SolidBrush(Color.FromArgb(18, 30, 58));
                e.Graphics.FillRectangle(activeBrush, 0, y, Width, rowH);

                using var activeBar = new SolidBrush(Color.FromArgb(0, 210, 255));
                e.Graphics.FillRectangle(activeBar, 0, y, 3, rowH);
            }

            // 1. Logo (20x20)
            var logo = LogoProvider.GetLogo(s.Server);
            if (logo != null)
            {
                e.Graphics.DrawImage(logo, 16, y + 7, 20, 20);
            }
            else
            {
                using var phBrush = new SolidBrush(Color.FromArgb(28, 43, 76));
                e.Graphics.FillEllipse(phBrush, 16, y + 7, 20, 20);
            }

            // 2. Server Display Info (Brand Name + Hostname)
            var (brand, host) = LogoProvider.GetServerDisplayInfo(s.Server);
            e.Graphics.DrawString(brand, fontBrand, isFastest ? brushCyan : brushWhite, 46, y + 2);
            e.Graphics.DrawString(host, fontHost, brushMuted, 46, y + 18);

            // 3. Latency
            string latencyText = s.Success ? $"{s.RoundTripMs:F1} ms" : "—";
            e.Graphics.DrawString(latencyText, fontMetrics, brushWhite, 350, y + 8);

            // 4. Status Dot + Label
            if (s.Success)
            {
                if (isFastest)
                {
                    e.Graphics.FillEllipse(brushCyan, 452, y + 13, 7, 7);
                    e.Graphics.DrawString("Active", fontStatus, brushCyan, 464, y + 8);
                }
                else
                {
                    e.Graphics.FillEllipse(brushGreen, 452, y + 13, 7, 7);
                    e.Graphics.DrawString("Online", fontStatus, brushGreen, 464, y + 8);
                }
            }
            else if (!s.Enabled)
            {
                e.Graphics.FillEllipse(brushDisabled, 452, y + 13, 7, 7);
                e.Graphics.DrawString("Disabled", fontStatus, brushDisabled, 464, y + 8);
            }
            else
            {
                e.Graphics.FillEllipse(brushRed, 452, y + 13, 7, 7);
                e.Graphics.DrawString(_isNoInternet ? "Offline" : "Timeout", fontStatus, brushRed, 464, y + 8);
            }

            // Separator Line
            e.Graphics.DrawLine(penSeparator, 16, y + rowH - 1, Width - 16, y + rowH - 1);
        }
    }
}
