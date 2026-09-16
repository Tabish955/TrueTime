using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NetTime.Tray;

public class AboutForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public AboutForm(Icon? appIcon)
    {
        Text = "About TrueTime Professional";
        Size = new Size(460, 240);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(11, 18, 34); // #0B1222
        ForeColor = Color.FromArgb(248, 250, 252);

        if (appIcon != null)
        {
            Icon = appIcon;
        }

        var logoImg = LogoProvider.GetAppLogo() ?? appIcon?.ToBitmap();
        var picIcon = new PictureBox
        {
            Location = new Point(24, 24),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Image = logoImg
        };
        Controls.Add(picIcon);

        var lblTitle = new Label
        {
            Text = "TrueTime Professional",
            Location = new Point(88, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252)
        };
        Controls.Add(lblTitle);

        var lblVersion = new Label
        {
            Text = "Version 1.0.0 (Win-x64 Enterprise)",
            Location = new Point(88, 48),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 210, 255)
        };
        Controls.Add(lblVersion);

        var lblDesc = new Label
        {
            Text = "Authoritative Multi-Server NTP Time Synchronization Suite.\nEnsures microsecond clock alignment across Windows systems with concurrent SNTP voting and automated failover.",
            Location = new Point(88, 76),
            Size = new Size(340, 54),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        Controls.Add(lblDesc);

        var btnOk = new Button
        {
            Text = "Close",
            Size = new Size(88, 32),
            Location = new Point(340, 148),
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(14, 165, 233),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.FlatAppearance.MouseOverBackColor = Color.FromArgb(2, 132, 199);
        btnOk.FlatAppearance.MouseDownBackColor = Color.FromArgb(3, 105, 161);
        Controls.Add(btnOk);

        AcceptButton = btnOk;
        CancelButton = btnOk;
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
}
