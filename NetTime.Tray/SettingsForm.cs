using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using NetTimeService.Models;

namespace NetTime.Tray;

public class SettingsForm : Form
{
    private static readonly string[] DefaultServers = new[]
    {
        "time.cloudflare.com",
        "time.google.com",
        "time.facebook.com",
        "time.apple.com",
        "time.windows.com",
        "pool.ntp.org",
        "time.nist.gov"
    };

    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "TrueTimeTray";

    private readonly CheckedListBox _clbServers;
    private readonly TextBox _txtNewServer;
    private readonly Button _btnAdd;
    private readonly Button _btnEdit;
    private readonly Button _btnRemove;
    private readonly Button _btnUp;
    private readonly Button _btnDown;
    private readonly Button _btnTest;
    private readonly Button _btnDefaults;
    private readonly Label _lblTestResult;

    private readonly ComboBox _cmbInterval;
    private readonly ComboBox _cmbThreshold;

    private readonly CheckBox _chkStartWithWindows;
    private readonly CheckBox _chkMinimizeOnClose;
    private readonly CheckBox _chkShowBalloons;
    private readonly CheckBox _chkEnableLocalNtpServer;

    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public bool MinimizeOnClose => _chkMinimizeOnClose.Checked;
    public bool ShowBalloons => _chkShowBalloons.Checked;

    public static string GetSharedConfigPath()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string dir = Path.Combine(programData, "TrueTime");
        if (!Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }
        return Path.Combine(dir, "settings.json");
    }

    public SettingsForm(Icon? appIcon, TimeSyncSnapshot? snapshot, bool minimizeOnClose, bool showBalloons)
    {
        Text = "TrueTime Configuration";
        Size = new Size(510, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 250, 252); // Modern slate-50

        if (appIcon != null)
        {
            Icon = appIcon;
        }

        // --- 1. Time Servers Group ---
        var grpServers = new GroupBox
        {
            Text = "NTP Server Pool (Customizable)",
            Location = new Point(14, 12),
            Size = new Size(466, 235),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            BackColor = Color.White
        };
        Controls.Add(grpServers);

        _clbServers = new CheckedListBox
        {
            Location = new Point(14, 24),
            Size = new Size(330, 130),
            CheckOnClick = true,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _clbServers.DoubleClick += (s, e) => EditSelectedServer();
        grpServers.Controls.Add(_clbServers);

        // Buttons column
        int btnX = 354;
        int btnW = 98;
        int btnH = 25;

        _btnEdit = CreateModernSecondaryButton("Edit...", btnX, 24, btnW, btnH);
        _btnEdit.Click += (s, e) => EditSelectedServer();
        grpServers.Controls.Add(_btnEdit);

        _btnRemove = CreateModernSecondaryButton("Remove", btnX, 52, btnW, btnH);
        _btnRemove.Click += BtnRemove_Click;
        grpServers.Controls.Add(_btnRemove);

        _btnUp = CreateModernSecondaryButton("▲ Up", btnX, 80, 46, btnH);
        _btnUp.Click += (s, e) => MoveSelectedServerUp();
        grpServers.Controls.Add(_btnUp);

        _btnDown = CreateModernSecondaryButton("▼ Dn", btnX + 52, 80, 46, btnH);
        _btnDown.Click += (s, e) => MoveSelectedServerDown();
        grpServers.Controls.Add(_btnDown);

        _btnTest = CreateModernSecondaryButton("Test Ping", btnX, 108, btnW, btnH);
        _btnTest.Click += async (s, e) => await TestSelectedServerAsync();
        grpServers.Controls.Add(_btnTest);

        _btnDefaults = CreateModernSecondaryButton("Defaults", btnX, 136, btnW, btnH);
        _btnDefaults.Click += (s, e) => ResetToDefaults();
        grpServers.Controls.Add(_btnDefaults);

        // Add server row
        var lblAdd = new Label
        {
            Text = "New Server:",
            Location = new Point(14, 166),
            AutoSize = true
        };
        grpServers.Controls.Add(lblAdd);

        _txtNewServer = new TextBox
        {
            Location = new Point(94, 163),
            Size = new Size(250, 23),
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtNewServer.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddServer();
            }
        };
        grpServers.Controls.Add(_txtNewServer);

        _btnAdd = CreateModernSecondaryButton("+ Add", btnX, 162, btnW, btnH);
        _btnAdd.Click += (s, e) => AddServer();
        grpServers.Controls.Add(_btnAdd);

        _lblTestResult = new Label
        {
            Text = "Select a server and click 'Test Ping' or double-click to edit.",
            Location = new Point(14, 198),
            Size = new Size(438, 26),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        grpServers.Controls.Add(_lblTestResult);

        // --- 2. Sync Frequency & Drift Group ---
        var grpSync = new GroupBox
        {
            Text = "Synchronization Schedule && Sensitivity",
            Location = new Point(14, 255),
            Size = new Size(466, 95),
            BackColor = Color.White
        };
        Controls.Add(grpSync);

        var lblInterval = new Label
        {
            Text = "Poll Interval:",
            Location = new Point(14, 28),
            AutoSize = true
        };
        grpSync.Controls.Add(lblInterval);

        _cmbInterval = new ComboBox
        {
            Location = new Point(106, 25),
            Size = new Size(130, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbInterval.Items.AddRange(new object[]
        {
            "5 minutes",
            "15 minutes",
            "30 minutes",
            "1 hour",
            "6 hours",
            "24 hours"
        });
        grpSync.Controls.Add(_cmbInterval);

        var lblThreshold = new Label
        {
            Text = "Adjust clock if drift exceeds:",
            Location = new Point(14, 60),
            AutoSize = true
        };
        grpSync.Controls.Add(lblThreshold);

        _cmbThreshold = new ComboBox
        {
            Location = new Point(195, 57),
            Size = new Size(125, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbThreshold.Items.AddRange(new object[]
        {
            "50 ms",
            "100 ms",
            "250 ms",
            "500 ms",
            "1000 ms",
            "2000 ms"
        });
        grpSync.Controls.Add(_cmbThreshold);

        // --- 3. Options Group ---
        var grpOptions = new GroupBox
        {
            Text = "Preferences && Advanced Features",
            Location = new Point(14, 358),
            Size = new Size(466, 122),
            BackColor = Color.White
        };
        Controls.Add(grpOptions);

        _chkStartWithWindows = new CheckBox
        {
            Text = "Start TrueTime Tray with Windows",
            Location = new Point(16, 20),
            AutoSize = true
        };
        grpOptions.Controls.Add(_chkStartWithWindows);

        _chkMinimizeOnClose = new CheckBox
        {
            Text = "Minimize to notification area (tray) on close",
            Location = new Point(16, 44),
            AutoSize = true,
            Checked = minimizeOnClose
        };
        grpOptions.Controls.Add(_chkMinimizeOnClose);

        _chkShowBalloons = new CheckBox
        {
            Text = "Show notification balloon when clock is adjusted",
            Location = new Point(16, 68),
            AutoSize = true,
            Checked = showBalloons
        };
        grpOptions.Controls.Add(_chkShowBalloons);

        _chkEnableLocalNtpServer = new CheckBox
        {
            Text = "Enable Local LAN NTP Server (Broadcast Stratum-2 time on UDP 123)",
            Location = new Point(16, 92),
            AutoSize = true
        };
        grpOptions.Controls.Add(_chkEnableLocalNtpServer);

        // --- Bottom Action Buttons (OK acts as Apply & Save, Cancel discards) ---
        _btnOk = CreateModernPrimaryButton("OK", 286, 492, 94, 32);
        _btnOk.Click += async (s, e) =>
        {
            _btnOk.Enabled = false;
            _btnOk.Text = "Saving...";
            try
            {
                await SaveSettingsAsync();
            }
            catch { }
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_btnOk);

        _btnCancel = CreateModernSecondaryButton("Cancel", 388, 492, 92, 32);
        _btnCancel.DialogResult = DialogResult.Cancel;
        _btnCancel.Click += (s, e) => Close();
        Controls.Add(_btnCancel);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        // Initialize values from snapshot, shared disk store, and registry
        LoadInitialValues(snapshot);
    }

    private void LoadInitialValues(TimeSyncSnapshot? snapshot)
    {
        // Check Windows Run key
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            _chkStartWithWindows.Checked = key?.GetValue(RunValueName) != null;
        }
        catch
        {
            _chkStartWithWindows.Checked = false;
        }

        // Servers: try snapshot first, then fallback to shared ProgramData config
        var serverList = snapshot?.ConfiguredServers;
        int intervalMins = snapshot?.PollIntervalMinutes ?? 15;
        double threshold = snapshot?.ThresholdMilliseconds ?? 500;
        bool enableLocalNtp = snapshot?.LocalNtpServerRunning ?? false;

        if (serverList == null || serverList.Count == 0)
        {
            string sharedPath = GetSharedConfigPath();
            if (File.Exists(sharedPath))
            {
                try
                {
                    string json = File.ReadAllText(sharedPath);
                    var saved = JsonSerializer.Deserialize<AppConfigPayload>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (saved != null)
                    {
                        enableLocalNtp = saved.EnableLocalNtpServer;
                        if (saved.Servers != null && saved.Servers.Count > 0)
                        {
                            serverList = saved.Servers;
                            intervalMins = saved.PollIntervalMinutes > 0 ? saved.PollIntervalMinutes : 15;
                            threshold = saved.ThresholdMilliseconds > 0 ? saved.ThresholdMilliseconds : 500;
                        }
                    }
                }
                catch { }
            }
        }

        _chkEnableLocalNtpServer.Checked = enableLocalNtp;

        if (serverList != null && serverList.Count > 0)
        {
            _clbServers.Items.Clear();
            foreach (var s in serverList)
            {
                int idx = _clbServers.Items.Add(s.Hostname);
                _clbServers.SetItemChecked(idx, s.Enabled);
            }
        }
        else
        {
            ResetToDefaults();
        }

        // Interval
        _cmbInterval.SelectedIndex = intervalMins switch
        {
            <= 5 => 0,
            <= 15 => 1,
            <= 30 => 2,
            <= 60 => 3,
            <= 360 => 4,
            _ => 5
        };

        // Threshold
        _cmbThreshold.SelectedIndex = threshold switch
        {
            <= 50 => 0,
            <= 100 => 1,
            <= 250 => 2,
            <= 500 => 3,
            <= 1000 => 4,
            _ => 5
        };
    }

    private void AddServer()
    {
        string host = _txtNewServer.Text.Trim();
        if (string.IsNullOrEmpty(host)) return;

        for (int i = 0; i < _clbServers.Items.Count; i++)
        {
            if (string.Equals(_clbServers.Items[i]?.ToString(), host, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Server '{host}' is already in the list.", "Server Exists", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        int idx = _clbServers.Items.Add(host);
        _clbServers.SetItemChecked(idx, true);
        _txtNewServer.Clear();
        _lblTestResult.Text = $"Added '{host}' to pool.";
        _lblTestResult.ForeColor = Color.FromArgb(16, 120, 60);
    }

    private void EditSelectedServer()
    {
        int idx = _clbServers.SelectedIndex;
        if (idx < 0)
        {
            MessageBox.Show("Please select a server from the list to edit.", "Edit Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string currentHost = _clbServers.Items[idx]?.ToString() ?? "";
        bool isChecked = _clbServers.GetItemChecked(idx);

        string? updated = PromptDialog.Show("Edit Server", "Enter the NTP server hostname or IP address:", currentHost, this);
        if (!string.IsNullOrWhiteSpace(updated) && !string.Equals(updated, currentHost, StringComparison.OrdinalIgnoreCase))
        {
            _clbServers.Items[idx] = updated;
            _clbServers.SetItemChecked(idx, isChecked);
            _lblTestResult.Text = $"Updated server to: {updated}";
            _lblTestResult.ForeColor = Color.FromArgb(16, 120, 60);
        }
    }

    private void MoveSelectedServerUp()
    {
        int idx = _clbServers.SelectedIndex;
        if (idx <= 0) return;

        var item = _clbServers.Items[idx];
        bool isChecked = _clbServers.GetItemChecked(idx);

        _clbServers.Items.RemoveAt(idx);
        _clbServers.Items.Insert(idx - 1, item);
        _clbServers.SetItemChecked(idx - 1, isChecked);
        _clbServers.SelectedIndex = idx - 1;
    }

    private void MoveSelectedServerDown()
    {
        int idx = _clbServers.SelectedIndex;
        if (idx < 0 || idx >= _clbServers.Items.Count - 1) return;

        var item = _clbServers.Items[idx];
        bool isChecked = _clbServers.GetItemChecked(idx);

        _clbServers.Items.RemoveAt(idx);
        _clbServers.Items.Insert(idx + 1, item);
        _clbServers.SetItemChecked(idx + 1, isChecked);
        _clbServers.SelectedIndex = idx + 1;
    }

    private void BtnRemove_Click(object? sender, EventArgs e)
    {
        int idx = _clbServers.SelectedIndex;
        if (idx >= 0)
        {
            string? host = _clbServers.Items[idx]?.ToString();
            _clbServers.Items.RemoveAt(idx);
            _lblTestResult.Text = $"Removed '{host}' from pool.";
            _lblTestResult.ForeColor = Color.FromArgb(180, 40, 40);
        }
    }

    private void ResetToDefaults()
    {
        _clbServers.Items.Clear();
        foreach (var server in DefaultServers)
        {
            int idx = _clbServers.Items.Add(server);
            _clbServers.SetItemChecked(idx, true);
        }
        _lblTestResult.Text = "Restored 7 authoritative default servers.";
        _lblTestResult.ForeColor = Color.FromArgb(16, 120, 60);
    }

    private async Task TestSelectedServerAsync()
    {
        int idx = _clbServers.SelectedIndex;
        if (idx < 0)
        {
            _lblTestResult.Text = "Please select a server from the list first.";
            _lblTestResult.ForeColor = Color.FromArgb(180, 40, 40);
            return;
        }

        string? host = _clbServers.Items[idx]?.ToString();
        if (string.IsNullOrWhiteSpace(host)) return;

        _btnTest.Enabled = false;
        _lblTestResult.Text = $"Pinging {host} over UDP 123...";
        _lblTestResult.ForeColor = Color.FromArgb(30, 41, 59);

        try
        {
            var result = await PipeClient.TestServerAsync(host);
            if (result != null && result.Success)
            {
                _lblTestResult.Text = $"Success! Latency: {result.RoundTripMs:F1} ms, Offset: {result.OffsetMs:+0.0;-0.0;0.0} ms";
                _lblTestResult.ForeColor = Color.FromArgb(16, 120, 60);
            }
            else
            {
                _lblTestResult.Text = $"Failed: {result?.Error ?? "Request timed out"}";
                _lblTestResult.ForeColor = Color.FromArgb(180, 40, 40);
            }
        }
        catch (Exception ex)
        {
            _lblTestResult.Text = $"Error: {ex.Message}";
            _lblTestResult.ForeColor = Color.FromArgb(180, 40, 40);
        }
        finally
        {
            _btnTest.Enabled = true;
        }
    }

    private async Task SaveSettingsAsync()
    {
        // 1. Windows Run Key
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key != null)
            {
                if (_chkStartWithWindows.Checked)
                {
                    string exePath = Application.ExecutablePath;
                    key.SetValue(RunValueName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }
        catch
        {
            // Ignore registry write error if restricted
        }

        // 2. Prepare AppConfigPayload
        var servers = new List<ServerEntry>();
        for (int i = 0; i < _clbServers.Items.Count; i++)
        {
            string? host = _clbServers.Items[i]?.ToString();
            if (!string.IsNullOrWhiteSpace(host))
            {
                servers.Add(new ServerEntry
                {
                    Hostname = host,
                    Enabled = _clbServers.GetItemChecked(i)
                });
            }
        }

        int interval = _cmbInterval.SelectedIndex switch
        {
            0 => 5,
            1 => 15,
            2 => 30,
            3 => 60,
            4 => 360,
            _ => 1440
        };

        double threshold = _cmbThreshold.SelectedIndex switch
        {
            0 => 50,
            1 => 100,
            2 => 250,
            3 => 500,
            4 => 1000,
            _ => 2000
        };

        var payload = new AppConfigPayload
        {
            Servers = servers,
            PollIntervalMinutes = interval,
            ThresholdMilliseconds = threshold,
            EnableLocalNtpServer = _chkEnableLocalNtpServer.Checked,
            LocalNtpPort = 123
        };

        // 3. Persist directly to shared store (%ProgramData%\TrueTime\settings.json)
        try
        {
            string sharedPath = GetSharedConfigPath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(payload, options);
            File.WriteAllText(sharedPath, json);
        }
        catch { }

        // 4. Send over Named Pipe to TrueTimeService
        try
        {
            await PipeClient.SaveConfigAsync(payload);
        }
        catch { }
    }
    private static Button CreateModernSecondaryButton(string text, int x, int y, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);
        return btn;
    }

    private static Button CreateModernPrimaryButton(string text, int x, int y, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.FromArgb(37, 99, 235),
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
}

internal static class PromptDialog
{
    public static string? Show(string title, string prompt, string initialValue, Form parent)
    {
        using var form = new Form
        {
            Text = title,
            Size = new Size(380, 160),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var lbl = new Label
        {
            Text = prompt,
            Location = new Point(16, 16),
            Size = new Size(330, 20)
        };
        form.Controls.Add(lbl);

        var txt = new TextBox
        {
            Text = initialValue,
            Location = new Point(16, 42),
            Size = new Size(330, 23),
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(txt);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(180, 80),
            Size = new Size(80, 26),
            DialogResult = DialogResult.OK,
            UseVisualStyleBackColor = true
        };
        form.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(266, 80),
            Size = new Size(80, 26),
            DialogResult = DialogResult.Cancel,
            UseVisualStyleBackColor = true
        };
        form.Controls.Add(btnCancel);

        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog(parent) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
