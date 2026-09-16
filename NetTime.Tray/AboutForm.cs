using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NetTime.Tray;

public class AboutForm : Form
{
    public AboutForm(Icon? appIcon)
    {
        Text = "About TrueTime Professional";
        Size = new Size(420, 210);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 250, 252); // Slate-50

        if (appIcon != null)
        {
            Icon = appIcon;
        }

        var picIcon = new PictureBox
        {
            Location = new Point(24, 24),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Image = appIcon?.ToBitmap()
        };
        Controls.Add(picIcon);

        var lblTitle = new Label
        {
            Text = "TrueTime Professional",
            Location = new Point(88, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };
        Controls.Add(lblTitle);

        var lblVersion = new Label
        {
            Text = "Version 1.0.0 (Win-x64 Enterprise)",
            Location = new Point(88, 46),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        Controls.Add(lblVersion);

        var lblDesc = new Label
        {
            Text = "Authoritative Multi-Server Time Synchronization Suite.\nEnsures precision clock alignment across Windows systems with concurrent SNTP voting and automated failover.",
            Location = new Point(88, 72),
            Size = new Size(300, 50),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85)
        };
        Controls.Add(lblDesc);

        var btnOk = new Button
        {
            Text = "OK",
            Size = new Size(85, 30),
            Location = new Point(300, 130),
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
        btnOk.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
        Controls.Add(btnOk);

        AcceptButton = btnOk;
        CancelButton = btnOk;
    }
}
