using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTimeService.Models;

namespace NetTime.Tray;

public class MainForm : Form
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _statusPollTimer;

    // Header Controls
    private readonly Label _lblHeaderTitle;
    private readonly Label _lblHeaderSubtitle;
    private readonly Label _lblHealthBadge;

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
    private readonly ListView _lvServers;

    // Bottom Action Bar Controls
    private readonly Label _lblStatusNote;
    private readonly Label _lblServiceStatus;
    private readonly Button _btnSyncNow;
    private readonly Button _btnAuditLog;
    private readonly Button _btnSettings;
    private readonly Button _btnAbout;
    private readonly Button _btnClose;

    // State Tracking
    private TimeSyncSnapshot? _lastSnapshot;
    private DateTime _lastAttemptTime = DateTime.MinValue;
    private int _pollIntervalMinutes = 15;
    private bool _minimizeOnClose = true;
    private bool _showBalloons = true;
    private bool _allowExit = false;
    private bool _isUpdating = false;
    private int _consecutiveFailures = 0;

    public MainForm(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;

        // --- Window Settings ---
        Text = "TrueTime Professional";
        Size = new Size(592, 495);
        MinimumSize = new Size(592, 495);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 250, 252); // Soft modern slate-50
        Icon = notifyIcon.Icon;

        // ==========================================
        // 1. TOP HEADER & HEALTH BADGE
        // ==========================================
        var pnlHeader = new Panel
        {
            Location = new Point(16, 12),
            Size = new Size(544, 40),
            BackColor = Color.Transparent
        };
        Controls.Add(pnlHeader);

        _lblHeaderTitle = new Label
        {
            Text = "TrueTime Professional",
            Location = new Point(0, 0),
            AutoSize = true,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42) // Slate-900
        };
        pnlHeader.Controls.Add(_lblHeaderTitle);

        _lblHeaderSubtitle = new Label
        {
            Text = "Precision Multi-Server Network Time Synchronization",
            Location = new Point(1, 23),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139) // Slate-500
        };
        pnlHeader.Controls.Add(_lblHeaderSubtitle);

        _lblHealthBadge = new Label
        {
            Text = "● Connecting...",
            Location = new Point(416, 6),
            Size = new Size(128, 26),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = Color.FromArgb(241, 245, 249)
        };
        _lblHealthBadge.Paint += DrawHealthBadgePill;
        pnlHeader.Controls.Add(_lblHealthBadge);

        // ==========================================
        // 2. DUAL BALANCED DASHBOARD CARDS (Side-by-Side)
        // ==========================================
        int cardY = 56;
        int cardW = 266;
        int cardH = 88;

        // --- Card 1: Local Clock Card ---
        _pnlClockCard = CreateModernCard(16, cardY, cardW, cardH);
        Controls.Add(_pnlClockCard);

        var lblClockHeader = new Label
        {
            Text = "LOCAL SYSTEM TIME",
            Location = new Point(12, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            UseMnemonic = false
        };
        _pnlClockCard.Controls.Add(lblClockHeader);

        _lblClockTime = new Label
        {
            Text = DateTime.Now.ToString("hh:mm:ss tt"),
            Location = new Point(11, 26),
            AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            UseMnemonic = false
        };
        _pnlClockCard.Controls.Add(_lblClockTime);

        _lblClockDateUtc = new Label
        {
            Text = $"{DateTime.Now:ddd, dd MMM yyyy}  •  {DateTime.UtcNow:HH:mm:ss} UTC",
            Location = new Point(12, 58),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            UseMnemonic = false
        };
        _pnlClockCard.Controls.Add(_lblClockDateUtc);

        // --- Card 2: Precision & Health Card ---
        _pnlSyncCard = CreateModernCard(294, cardY, cardW, cardH);
        Controls.Add(_pnlSyncCard);

        var lblSyncHeader = new Label
        {
            Text = "CLOCK ACCURACY & STATUS",
            Location = new Point(12, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            UseMnemonic = false
        };
        _pnlSyncCard.Controls.Add(lblSyncHeader);

        _lblSyncOffset = new Label
        {
            Text = "Status: Initializing...",
            Location = new Point(11, 28),
            AutoSize = true,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            UseMnemonic = false
        };
        _pnlSyncCard.Controls.Add(_lblSyncOffset);

        _lblSyncNextAndServer = new Label
        {
            Text = "Next check: Calculating...",
            Location = new Point(12, 58),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            UseMnemonic = false
        };
        _pnlSyncCard.Controls.Add(_lblSyncNextAndServer);

        // ==========================================
        // 3. NTP SERVER POOL & LIVE METRICS TABLE
        // ==========================================
        int tableTopY = 152;
        var pnlTableBar = new Panel
        {
            Location = new Point(16, tableTopY),
            Size = new Size(544, 22),
            BackColor = Color.Transparent
        };
        Controls.Add(pnlTableBar);

        _lblTableTitle = new Label
        {
            Text = "NTP Server Pool",
            Location = new Point(0, 2),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        pnlTableBar.Controls.Add(_lblTableTitle);

        _lblTableSubtitle = new Label
        {
            Text = "Concurrent SNTP queries  •  Double-click server to configure",
            Location = new Point(180, 3),
            Size = new Size(364, 18),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        pnlTableBar.Controls.Add(_lblTableSubtitle);

        _lvServers = new ListView
        {
            Location = new Point(16, tableTopY + 22),
            Size = new Size(544, 178),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false, // Clean whitespace rows instead of harsh spreadsheet grid lines
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9F)
        };
        _lvServers.Columns.Add("Server Hostname", 175, HorizontalAlignment.Left);
        _lvServers.Columns.Add("Stratum", 60, HorizontalAlignment.Center);
        _lvServers.Columns.Add("Status", 75, HorizontalAlignment.Left);
        _lvServers.Columns.Add("Round Trip", 75, HorizontalAlignment.Right);
        _lvServers.Columns.Add("Offset", 75, HorizontalAlignment.Right);
        _lvServers.Columns.Add("Response Note", 84, HorizontalAlignment.Left);
        _lvServers.DoubleClick += (s, e) => OpenSettings();
        Controls.Add(_lvServers);

        PopulateServersFromSharedConfigIfEmpty();

        // ==========================================
        // 4. BOTTOM ACTION & STATUS BAR
        // ==========================================
        int bottomY = 360;
        var pnlBottom = new Panel
        {
            Location = new Point(16, bottomY),
            Size = new Size(544, 88),
            BackColor = Color.Transparent
        };
        Controls.Add(pnlBottom);

        _lblStatusNote = new Label
        {
            Text = "Ready. Time synchronized automatically with authoritative global servers.",
            Location = new Point(0, 4),
            Size = new Size(544, 18),
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        pnlBottom.Controls.Add(_lblStatusNote);

        // Service indicator label on left
        _lblServiceStatus = new Label
        {
            Text = "● Service: Running",
            Location = new Point(0, 35),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        pnlBottom.Controls.Add(_lblServiceStatus);

        // Cohesive Modern Action Buttons
        int btnY = 28;
        int btnH = 32;

        _btnSyncNow = CreateModernPrimaryButton("⚡ Sync", 154, btnY, 86, btnH);
        _btnSyncNow.Click += async (s, e) => await TriggerSyncNowAsync();
        pnlBottom.Controls.Add(_btnSyncNow);

        _btnAuditLog = CreateModernSecondaryButton("📊 Log", 246, btnY, 70, btnH);
        _btnAuditLog.Click += (s, e) => OpenAuditLog();
        pnlBottom.Controls.Add(_btnAuditLog);

        _btnSettings = CreateModernSecondaryButton("Settings...", 322, btnY, 82, btnH);
        _btnSettings.Click += (s, e) => OpenSettings();
        pnlBottom.Controls.Add(_btnSettings);

        _btnAbout = CreateModernSecondaryButton("About", 410, btnY, 62, btnH);
        _btnAbout.Click += (s, e) => OpenAbout();
        pnlBottom.Controls.Add(_btnAbout);

        _btnClose = CreateModernSecondaryButton("Close", 478, btnY, 66, btnH);
        _btnClose.Click += (s, e) => HandleClose();
        pnlBottom.Controls.Add(_btnClose);

        // --- Timers ---
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();

        _statusPollTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _statusPollTimer.Tick += async (s, e) => await RefreshStatusAsync();
        _statusPollTimer.Start();

        _ = RefreshStatusAsync();
    }

    private static Panel CreateModernCard(int x, int y, int width, int height)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.White
        };
        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    private static Button CreateModernPrimaryButton(string text, int x, int y, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.FromArgb(37, 99, 235), // Royal Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
        return btn;
    }

    private static Button CreateModernSecondaryButton(string text, int x, int y, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85), // Slate-700
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225); // Slate-300
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);
        return btn;
    }

    private Color _healthBadgeBorderColor = Color.FromArgb(203, 213, 225);

    private void DrawHealthBadgePill(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var rect = new Rectangle(0, 0, _lblHealthBadge.Width - 1, _lblHealthBadge.Height - 1);
        int radius = 12;

        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();

        using var brush = new SolidBrush(_lblHealthBadge.BackColor);
        e.Graphics.FillPath(brush, path);

        using var pen = new Pen(_healthBadgeBorderColor, 1f);
        e.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(e.Graphics, _lblHealthBadge.Text, _lblHealthBadge.Font, rect, _lblHealthBadge.ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        _lblClockTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
        _lblClockDateUtc.Text = $"{DateTime.Now:ddd, dd MMM yyyy}  •  {DateTime.UtcNow:HH:mm:ss} UTC";

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
                    SetHealthBadge("● Offline", Color.FromArgb(185, 28, 28), Color.FromArgb(254, 242, 242), Color.FromArgb(254, 202, 202));
                    _lblStatusNote.Text = "TrueTime Service is offline or not reachable via Named Pipe.";
                    _lblStatusNote.ForeColor = Color.FromArgb(185, 28, 28);
                    _lblServiceStatus.Text = "● Service: Offline";
                    _lblServiceStatus.ForeColor = Color.FromArgb(185, 28, 28);
                    _lblSyncOffset.Text = "Service Offline";
                    _lblSyncOffset.ForeColor = Color.FromArgb(185, 28, 28);
                    PopulateServersFromSharedConfigIfEmpty();
                }
            }
        }
        catch
        {
            // Suppress background poll glitches
        }
    }

    public async Task TriggerSyncNowAsync()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        _btnSyncNow.Enabled = false;
        _btnSyncNow.Text = "Syncing...";
        SetHealthBadge("⚡ Syncing...", Color.FromArgb(37, 99, 235), Color.FromArgb(239, 246, 255), Color.FromArgb(191, 219, 254));
        _lblStatusNote.Text = "Querying authoritative NTP servers concurrently over UDP 123...";
        _lblStatusNote.ForeColor = Color.FromArgb(37, 99, 235);

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
            _btnSyncNow.Text = "⚡ Sync";
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
            _lblSyncOffset.ForeColor = Color.FromArgb(220, 38, 38); // Red-600
            SetHealthBadge("● No Internet", Color.FromArgb(220, 38, 38), Color.FromArgb(254, 242, 242), Color.FromArgb(254, 202, 202));

            DateTime nextAttempt = snapshot.NextSyncTime != DateTime.MinValue
                ? snapshot.NextSyncTime
                : _lastAttemptTime.AddSeconds(snapshot.RetryIntervalSeconds > 0 ? snapshot.RetryIntervalSeconds : 60);
            var remaining = nextAttempt - DateTime.Now;
            string timeStr = remaining > TimeSpan.Zero
                ? (remaining.TotalMinutes >= 1 ? $"{(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s" : $"{remaining.Seconds}s")
                : "0s";

            _lblSyncNextAndServer.Text = remaining > TimeSpan.Zero
                ? $"No internet. Trying again in {timeStr}..."
                : "No internet. Retrying connection now...";
            _lblStatusNote.Text = remaining > TimeSpan.Zero
                ? $"No internet connection. Retrying authoritative servers in {timeStr}..."
                : "No internet. Retrying connection now...";
            _lblStatusNote.ForeColor = Color.FromArgb(220, 38, 38);
        }
        else
        {
            // Update Offset display in Sync Card
            string offsetText = $"{snapshot.OffsetMs:+0.0;-0.0;0.0} ms";
            string metricsText = "";
            if (snapshot.PoolJitterMs > 0 || Math.Abs(snapshot.CrystalPpm) > 0.01)
            {
                metricsText = $"  |  Jitter: {snapshot.PoolJitterMs:F1}ms  |  Drift: {snapshot.CrystalPpm:+0.0;-0.0;0.0} PPM";
            }

            if (snapshot.InSync && !string.IsNullOrEmpty(snapshot.SelectedServer) && Math.Abs(snapshot.OffsetMs) <= snapshot.ThresholdMilliseconds)
            {
                _lblSyncOffset.Text = $"Accurate ({offsetText})";
                _lblSyncOffset.ForeColor = Color.FromArgb(5, 150, 105); // Emerald-600
                SetHealthBadge("● In Sync", Color.FromArgb(5, 150, 105), Color.FromArgb(236, 253, 245), Color.FromArgb(167, 243, 208));
                _lblStatusNote.Text = $"System clock accurate within {snapshot.ThresholdMilliseconds} ms via {snapshot.SelectedServer}.{metricsText}";
                _lblStatusNote.ForeColor = Color.FromArgb(5, 150, 105);
            }
            else
            {
                _lblSyncOffset.Text = $"Drift: {offsetText}";
                _lblSyncOffset.ForeColor = Color.FromArgb(217, 119, 6); // Amber-600
                SetHealthBadge("● Drift Detected", Color.FromArgb(217, 119, 6), Color.FromArgb(255, 251, 235), Color.FromArgb(253, 230, 138));
                _lblStatusNote.Text = $"Time drift of {offsetText} detected. Adjusting clock...{metricsText}";
                _lblStatusNote.ForeColor = Color.FromArgb(217, 119, 6);
            }
        }

        string lanNtpStatus = snapshot.LocalNtpServerRunning ? "  •  LAN NTP: Active" : "";
        _lblServiceStatus.Text = $"● Service: Active{lanNtpStatus}";
        _lblServiceStatus.ForeColor = Color.FromArgb(5, 150, 105);

        // Server Table Population
        _lvServers.BeginUpdate();
        _lvServers.Items.Clear();

        if (snapshot.Servers != null)
        {
            foreach (var s in snapshot.Servers)
            {
                var lvi = new ListViewItem(s.Server);
                bool isFastest = string.Equals(s.Server, snapshot.SelectedServer, StringComparison.OrdinalIgnoreCase);

                if (s.Success)
                {
                    lvi.SubItems.Add(s.Stratum > 0 ? $"Stratum {s.Stratum}" : "—");
                    lvi.SubItems.Add(isFastest ? "★ Active" : "Online");
                    lvi.SubItems.Add($"{s.RoundTripMs:F1} ms");
                    lvi.SubItems.Add($"{s.OffsetMs:+0.0;-0.0;0.0} ms");
                    lvi.SubItems.Add(isFastest ? "Fastest source" : "Responding");

                    if (isFastest)
                    {
                        lvi.Font = new Font(_lvServers.Font, FontStyle.Bold);
                        lvi.ForeColor = Color.FromArgb(5, 150, 105); // Emerald-600
                    }
                    else
                    {
                        lvi.ForeColor = Color.FromArgb(30, 41, 59);
                    }
                }
                else
                {
                    lvi.SubItems.Add("—");
                    lvi.SubItems.Add(s.Enabled ? (isNoInternet ? "Offline" : "Timeout") : "Disabled");
                    lvi.SubItems.Add("—");
                    lvi.SubItems.Add("—");
                    lvi.SubItems.Add(s.Error ?? (s.Enabled ? (isNoInternet ? "No Internet connection" : "Timed out") : "Disabled"));
                    lvi.ForeColor = isNoInternet && s.Enabled ? Color.FromArgb(220, 38, 38) : Color.FromArgb(148, 163, 184);
                }

                _lvServers.Items.Add(lvi);
            }
        }

        _lvServers.EndUpdate();
    }

    private void SetHealthBadge(string text, Color foreColor, Color backColor, Color borderColor)
    {
        _lblHealthBadge.Text = text;
        _lblHealthBadge.ForeColor = foreColor;
        _lblHealthBadge.BackColor = backColor;
        _healthBadgeBorderColor = borderColor;
        _lblHealthBadge.Invalidate();
    }

    private void PopulateServersFromSharedConfigIfEmpty()
    {
        if (_lvServers.Items.Count > 0) return;

        try
        {
            string path = SettingsForm.GetSharedConfigPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var config = System.Text.Json.JsonSerializer.Deserialize<AppConfigPayload>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (config?.Servers != null && config.Servers.Count > 0)
                {
                    _lvServers.BeginUpdate();
                    _lvServers.Items.Clear();
                    foreach (var s in config.Servers)
                    {
                        var lvi = new ListViewItem(s.Hostname);
                        lvi.SubItems.Add("—");
                        lvi.SubItems.Add(s.Enabled ? "Configured" : "Disabled");
                        lvi.SubItems.Add("—");
                        lvi.SubItems.Add("—");
                        lvi.SubItems.Add(s.Enabled ? "Ready to sync" : "Disabled");
                        lvi.ForeColor = Color.FromArgb(100, 116, 139);
                        _lvServers.Items.Add(lvi);
                    }
                    _lvServers.EndUpdate();
                }
            }
        }
        catch { }
    }

    public void OpenAuditLog()
    {
        using var form = new AuditLogForm(Icon, _lastSnapshot?.History);
        form.ShowDialog(this);
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
        if (!_allowExit && _minimizeOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
