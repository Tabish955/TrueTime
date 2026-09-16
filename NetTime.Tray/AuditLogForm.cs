using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NetTimeService.Models;

namespace NetTime.Tray;

public class AuditLogForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly ListView _lvHistory;
    private readonly List<SyncHistoryEntry> _history;

    public AuditLogForm(Icon? icon, List<SyncHistoryEntry>? history)
    {
        _history = history ?? new List<SyncHistoryEntry>();

        Text = "TrueTime Synchronization Audit Log";
        Size = new Size(640, 440);
        MinimumSize = new Size(600, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(11, 18, 34); // #0B1222
        ForeColor = Color.FromArgb(248, 250, 252);

        if (icon != null) Icon = icon;

        // Header
        var lblTitle = new Label
        {
            Text = "Time Synchronization Audit Log",
            Location = new Point(16, 14),
            AutoSize = true,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252)
        };
        Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Historical record of authoritative NTP voting, measured offsets, and clock adjustments.",
            Location = new Point(17, 38),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        Controls.Add(lblSubtitle);

        // List view
        _lvHistory = new ListView
        {
            Location = new Point(16, 66),
            Size = new Size(592, 275),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(17, 27, 51), // #111B33
            ForeColor = Color.FromArgb(241, 245, 249),
            Font = new Font("Segoe UI", 8.5F)
        };
        _lvHistory.Columns.Add("Timestamp", 130);
        _lvHistory.Columns.Add("Authoritative Server", 155);
        _lvHistory.Columns.Add("Stratum", 65, HorizontalAlignment.Center);
        _lvHistory.Columns.Add("Offset", 75, HorizontalAlignment.Right);
        _lvHistory.Columns.Add("Latency", 65, HorizontalAlignment.Right);
        _lvHistory.Columns.Add("Status Summary", 100);
        Controls.Add(_lvHistory);

        PopulateList();

        // Bottom buttons
        var btnExport = new Button
        {
            Text = "📥 Export CSV...",
            Location = new Point(16, 354),
            Size = new Size(120, 32),
            BackColor = Color.FromArgb(21, 32, 56),
            ForeColor = Color.FromArgb(248, 250, 252),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btnExport.FlatAppearance.BorderSize = 1;
        btnExport.FlatAppearance.BorderColor = Color.FromArgb(45, 60, 95);
        btnExport.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 45, 80);
        btnExport.Click += (s, e) => ExportCsv();
        Controls.Add(btnExport);

        var btnClose = new Button
        {
            Text = "Close",
            Location = new Point(528, 354),
            Size = new Size(80, 32),
            BackColor = Color.FromArgb(14, 165, 233),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK,
            UseVisualStyleBackColor = false
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(2, 132, 199);
        Controls.Add(btnClose);

        AcceptButton = btnClose;
        CancelButton = btnClose;
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

    private void PopulateList()
    {
        _lvHistory.BeginUpdate();
        _lvHistory.Items.Clear();

        if (_history.Count == 0)
        {
            var empty = new ListViewItem(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            empty.SubItems.Add("No recorded events");
            empty.SubItems.Add("—");
            empty.SubItems.Add("0.0 ms");
            empty.SubItems.Add("—");
            empty.SubItems.Add("Awaiting initial sync");
            empty.ForeColor = Color.FromArgb(148, 163, 184);
            _lvHistory.Items.Add(empty);
        }
        else
        {
            foreach (var h in _history)
            {
                var lvi = new ListViewItem(h.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
                lvi.SubItems.Add(h.Server);
                lvi.SubItems.Add($"Stratum {h.Stratum}");
                lvi.SubItems.Add($"{h.OffsetMs:+0.0;-0.0;0.0} ms");
                lvi.SubItems.Add($"{h.RoundTripMs:F1} ms");
                lvi.SubItems.Add(h.Success ? "Synchronized" : "Timeout");

                if (!h.Success)
                {
                    lvi.ForeColor = Color.FromArgb(239, 68, 68);
                }
                else
                {
                    lvi.ForeColor = Color.FromArgb(241, 245, 249);
                }

                _lvHistory.Items.Add(lvi);
            }
        }

        _lvHistory.EndUpdate();
    }

    private void ExportCsv()
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Export Synchronization Audit Log",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"TrueTime_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Server,Stratum,OffsetMs,RoundTripMs,Success,Summary");
                foreach (var h in _history)
                {
                    sb.AppendLine($"\"{h.Timestamp:u}\",\"{h.Server}\",{h.Stratum},{h.OffsetMs},{h.RoundTripMs},{h.Success},\"{h.Summary.Replace("\"", "\"\"")}\"");
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Audit log successfully exported to:\n" + sfd.FileName, "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not export audit log: " + ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
