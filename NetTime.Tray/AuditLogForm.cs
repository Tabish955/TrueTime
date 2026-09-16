using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NetTimeService.Models;

namespace NetTime.Tray;

public class AuditLogForm : Form
{
    private readonly ListView _lvHistory;
    private readonly List<SyncHistoryEntry> _history;

    public AuditLogForm(Icon? icon, List<SyncHistoryEntry>? history)
    {
        _history = history ?? new List<SyncHistoryEntry>();

        Text = "TrueTime Synchronization Audit Log";
        Size = new Size(620, 420);
        MinimumSize = new Size(580, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 250, 252);

        if (icon != null) Icon = icon;

        // Header
        var lblTitle = new Label
        {
            Text = "Time Synchronization Audit Log",
            Location = new Point(16, 14),
            AutoSize = true,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };
        Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Historical record of authoritative NTP voting, measured offsets, and clock adjustments.",
            Location = new Point(17, 36),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        Controls.Add(lblSubtitle);

        // List view
        _lvHistory = new ListView
        {
            Location = new Point(16, 62),
            Size = new Size(572, 260),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 8.5F)
        };
        _lvHistory.Columns.Add("Timestamp", 125);
        _lvHistory.Columns.Add("Authoritative Server", 145);
        _lvHistory.Columns.Add("Stratum", 60, HorizontalAlignment.Center);
        _lvHistory.Columns.Add("Offset", 70, HorizontalAlignment.Right);
        _lvHistory.Columns.Add("Latency", 65, HorizontalAlignment.Right);
        _lvHistory.Columns.Add("Status Summary", 100);
        Controls.Add(_lvHistory);

        PopulateList();

        // Bottom buttons
        var btnExport = new Button
        {
            Text = "📥 Export CSV...",
            Location = new Point(16, 335),
            Size = new Size(115, 30),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btnExport.FlatAppearance.BorderSize = 1;
        btnExport.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnExport.Click += (s, e) => ExportCsv();
        Controls.Add(btnExport);

        var btnClose = new Button
        {
            Text = "Close",
            Location = new Point(508, 335),
            Size = new Size(80, 30),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        btnClose.FlatAppearance.BorderSize = 0;
        Controls.Add(btnClose);

        AcceptButton = btnClose;
        CancelButton = btnClose;
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
                    lvi.ForeColor = Color.FromArgb(220, 38, 38);
                }
                else
                {
                    lvi.ForeColor = Color.FromArgb(30, 41, 59);
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
