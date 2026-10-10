using System.Diagnostics;

namespace UsbDesktop;

class LoginForm : Form
{
    readonly bool firstRun = !File.Exists(Auth.UserFile);
    readonly Label clock = new(), date = new(), name = new(), hint = new(), err = new(), tip = new();
    readonly TextBox pass = new();
    readonly Panel card = new(), avatar = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    Bitmap? bg;

    public LoginForm()
    {
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; DoubleBuffered = true; KeyPreview = true;
        clock.Font = new Font("Segoe UI Light", 60); clock.ForeColor = Color.White; clock.AutoSize = true; clock.BackColor = Color.Transparent; clock.Location = new Point(40, 30);
        date.Font = new Font("Segoe UI", 15); date.ForeColor = Color.FromArgb(225, 225, 245); date.AutoSize = true; date.BackColor = Color.Transparent; date.Location = new Point(48, 150);
        tip.Text = "Click or press any key to unlock"; tip.Font = new Font("Segoe UI", 12); tip.ForeColor = Color.White; tip.AutoSize = true; tip.BackColor = Color.Transparent;
        Controls.AddRange(new Control[] { clock, date, tip, card });

        card.Size = new Size(340, 360); card.BackColor = Color.FromArgb(40, 36, 96); card.Visible = firstRun;
        avatar.Size = new Size(104, 104); avatar.Location = new Point(118, 24); avatar.BackColor = card.BackColor;
        avatar.Paint += (_, e) => Theme.Avatar(e.Graphics, new Rectangle(4, 4, 96, 96));
        name.Text = Theme.Name; name.Font = new Font("Segoe UI", 17); name.ForeColor = Color.White; name.TextAlign = ContentAlignment.MiddleCenter; name.SetBounds(0, 134, 340, 36);
        hint.Text = firstRun ? "Create a password for this USB" : "Enter your password"; hint.Font = new Font("Segoe UI", 10); hint.ForeColor = Color.FromArgb(206, 203, 246); hint.TextAlign = ContentAlignment.MiddleCenter; hint.SetBounds(0, 172, 340, 24);
        pass.UseSystemPasswordChar = true; pass.Font = new Font("Segoe UI", 13); pass.SetBounds(30, 212, 240, 34); pass.BorderStyle = BorderStyle.FixedSingle; pass.BackColor = Color.FromArgb(60, 52, 137); pass.ForeColor = Color.White;
        var go = new Button { Text = "→", Font = new Font("Segoe UI", 13), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(127, 119, 221), ForeColor = Color.White, Bounds = new Rectangle(276, 212, 34, 34) };
        go.FlatAppearance.BorderSize = 0; go.Click += (_, _) => Submit();
        err.ForeColor = Color.FromArgb(240, 149, 149); err.Font = new Font("Segoe UI", 10); err.TextAlign = ContentAlignment.MiddleCenter; err.SetBounds(0, 254, 340, 24);
        var usb = new Label { Text = "Running from " + Path.GetPathRoot(Paths.Root), ForeColor = Color.FromArgb(175, 169, 236), Font = new Font("Segoe UI", 10), TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 300, 340, 24) };
        card.Controls.AddRange(new Control[] { avatar, name, hint, pass, go, err, usb });

        var off = new Button { Text = "⏻", Font = new Font("Segoe UI", 14), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(83, 74, 183), Size = new Size(44, 44) };
        off.FlatAppearance.BorderSize = 0; off.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(off);
        Resize += (_, _) =>
        {
            card.Location = new Point((ClientSize.Width - card.Width) / 2, (ClientSize.Height - card.Height) / 2);
            off.Location = new Point(ClientSize.Width - 64, ClientSize.Height - 64);
            tip.Location = new Point((ClientSize.Width - tip.Width) / 2, ClientSize.Height - 90);
            Invalidate();
        };
        AcceptButton = go;
        Click += (_, _) => Reveal();
        KeyDown += (_, e) => { if (!card.Visible) { Reveal(); e.SuppressKeyPress = true; } };
        timer.Tick += (_, _) => Tick(); timer.Start(); Tick();
        tip.Visible = !firstRun;
        Shown += (_, _) => { if (firstRun) pass.Focus(); };
    }

    void Reveal() { if (card.Visible) return; card.Visible = true; tip.Visible = false; pass.Focus(); }
    void Tick() { clock.Text = DateTime.Now.ToString("HH:mm"); date.Text = DateTime.Now.ToString("dddd, MMMM d"); }

    void Submit()
    {
        if (pass.Text.Length < 1) { err.Text = "Enter a password"; return; }
        if (firstRun) { File.WriteAllText(Auth.UserFile, Auth.Hash(pass.Text)); DialogResult = DialogResult.OK; return; }
        if (File.ReadAllText(Auth.UserFile).Trim() == Auth.Hash(pass.Text)) DialogResult = DialogResult.OK;
        else { err.Text = "Wrong password. Try again."; pass.SelectAll(); }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (bg == null || bg.Size != ClientSize) { bg?.Dispose(); bg = Theme.LockBg(ClientSize); }
        e.Graphics.DrawImage(bg, 0, 0);
    }
}

class NotepadForm : Form
{
    readonly TextBox tb = new() { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 12), AcceptsTab = true, WordWrap = false, BorderStyle = BorderStyle.None };
    string? path; bool dirty;

    public NotepadForm(string? p = null)
    {
        Size = new Size(720, 500); StartPosition = FormStartPosition.CenterScreen;
        var ms = new MenuStrip(); var f = new ToolStripMenuItem("File");
        f.DropDownItems.Add(new ToolStripMenuItem("New", null, (_, _) => { if (Confirm()) { tb.Text = ""; path = null; dirty = false; Title(); } }, Keys.Control | Keys.N));
        f.DropDownItems.Add(new ToolStripMenuItem("Open...", null, (_, _) =>
        {
            if (!Confirm()) return;
            using var d = new OpenFileDialog { InitialDirectory = Paths.Desktop };
            if (d.ShowDialog() == DialogResult.OK) Open(d.FileName);
        }, Keys.Control | Keys.O));
        f.DropDownItems.Add(new ToolStripMenuItem("Save", null, (_, _) => Save(false), Keys.Control | Keys.S));
        f.DropDownItems.Add(new ToolStripMenuItem("Save as...", null, (_, _) => Save(true)));
        ms.Items.Add(f);
        Controls.Add(tb); Controls.Add(ms); MainMenuStrip = ms;
        tb.TextChanged += (_, _) => { dirty = true; Title(); };
        FormClosing += (_, e) => { if (!Confirm()) e.Cancel = true; };
        if (p != null) Open(p);
        Title();
    }

    void Title() => Text = (path == null ? "Untitled" : Path.GetFileName(path)) + (dirty ? "*" : "") + " - Notepad";
    void Open(string p) { try { tb.Text = File.ReadAllText(p); path = p; dirty = false; Title(); } catch (Exception ex) { MessageBox.Show(ex.Message); } }

    bool Save(bool asNew)
    {
        if (path == null || asNew)
        {
            using var d = new SaveFileDialog { InitialDirectory = Paths.Desktop, DefaultExt = "txt", FileName = path == null ? "Untitled.txt" : Path.GetFileName(path) };
            if (d.ShowDialog() != DialogResult.OK) return false;
            path = d.FileName;
        }
        try { File.WriteAllText(path, tb.Text); dirty = false; Title(); DesktopForm.I?.Reload(); return true; }
        catch (Exception ex) { MessageBox.Show(ex.Message); return false; }
    }

    bool Confirm()
    {
        if (!dirty) return true;
        var r = MessageBox.Show("Save changes?", "Notepad", MessageBoxButtons.YesNoCancel);
        return r == DialogResult.No || (r == DialogResult.Yes && Save(false));
    }
}

class SettingsForm : Form
{
    readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    readonly ListBox nav = new() { Dock = DockStyle.Left, Width = 170, BackColor = Color.FromArgb(30, 30, 48), ForeColor = Color.White, BorderStyle = BorderStyle.None, ItemHeight = 44, Font = new Font("Segoe UI", 12) };

    public SettingsForm()
    {
        Text = "Settings"; Size = new Size(840, 560); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        nav.Items.AddRange(new object[] { "  Personalize", "  Profile", "  Lock screen", "  Display", "  Security", "  About" });
        nav.SelectedIndexChanged += (_, _) => Go(nav.SelectedIndex);
        Controls.Add(content); Controls.Add(nav);
        nav.SelectedIndex = 0;
    }

    Label T(string s, int x, int y, float sz = 10, bool bold = false)
    {
        var l = new Label { Text = s, Left = x, Top = y, AutoSize = true, Font = new Font("Segoe UI", sz, bold ? FontStyle.Bold : FontStyle.Regular) };
        content.Controls.Add(l); return l;
    }
    Button Bt(string s, int x, int y, int w, Action a)
    {
        var b = new Button { Text = s, Left = x, Top = y, Width = w, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White };
        b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => a(); content.Controls.Add(b); return b;
    }
    static string? Pick()
    {
        using var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        return d.ShowDialog() == DialogResult.OK ? d.FileName : null;
    }
    static void Apply() => DesktopForm.I?.ApplyTheme();

    void Go(int i)
    {
        foreach (Control c in content.Controls.Cast<Control>().ToList()) c.Dispose();
        content.Controls.Clear();
        switch (i)
        {
            case 0:
                T("Personalize", 24, 18, 18, true); T("Wallpaper", 24, 64, 11, true);
                int x = 24;
                foreach (var w in Theme.Walls)
                {
                    var n = w.Name;
                    var pb = new PictureBox { Left = x, Top = 96, Width = 112, Height = 66, SizeMode = PictureBoxSizeMode.StretchImage, Image = Theme.Wall(new Size(224, 132), n), Cursor = Cursors.Hand };
                    pb.Click += (_, _) => { Cfg.Set("wall", n); Apply(); };
                    content.Controls.Add(pb); T(n, x, 166); x += 122;
                }
                Bt("Choose your own picture...", 24, 210, 230, () =>
                {
                    var p = Pick(); if (p == null) return;
                    File.Copy(p, Path.Combine(Paths.Data, "wall.img"), true); Cfg.Set("wall", "custom"); Apply();
                });
                T("Accent colour", 24, 270, 11, true);
                int ax = 24;
                foreach (var h in new[] { "#7F77DD", "#378ADD", "#1D9E75", "#D4537E", "#EF9F27", "#E24B4A", "#639922", "#888780" })
                {
                    var hex = h;
                    var b = new Button { Left = ax, Top = 304, Width = 38, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = ColorTranslator.FromHtml(hex) };
                    b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => { Cfg.Set("accent", hex); Apply(); };
                    content.Controls.Add(b); ax += 46;
                }
                break;
            case 1:
                T("Profile", 24, 18, 18, true);
                var pv = new Panel { Left = 24, Top = 70, Width = 112, Height = 112 };
                pv.Paint += (_, e) => Theme.Avatar(e.Graphics, new Rectangle(4, 4, 100, 100));
                content.Controls.Add(pv);
                Bt("Change picture...", 160, 76, 180, () =>
                {
                    var p = Pick(); if (p == null) return;
                    File.Copy(p, Theme.AvatarPath, true); pv.Invalidate(); Apply();
                });
                Bt("Remove picture", 160, 122, 180, () => { try { File.Delete(Theme.AvatarPath); } catch { } pv.Invalidate(); Apply(); });
                T("Display name", 24, 210, 11, true);
                var tb = new TextBox { Left = 24, Top = 240, Width = 260, Text = Theme.Name }; content.Controls.Add(tb);
                Bt("Save name", 300, 238, 110, () => { Cfg.Set("name", tb.Text.Trim()); pv.Invalidate(); Apply(); });
                T("Your picture and name show on the login and lock screen.", 24, 290, 9);
                break;
            case 2:
                T("Lock screen", 24, 18, 18, true);
                var lp = new PictureBox { Left = 24, Top = 70, Width = 340, Height = 190, SizeMode = PictureBoxSizeMode.Zoom, Image = Theme.LockBg(new Size(680, 380)) };
                content.Controls.Add(lp);
                Bt("Choose picture...", 24, 275, 170, () =>
                {
                    var p = Pick(); if (p == null) return;
                    File.Copy(p, Theme.LockPath, true); lp.Image = Theme.LockBg(new Size(680, 380));
                });
                Bt("Use wallpaper", 204, 275, 160, () => { try { File.Delete(Theme.LockPath); } catch { } lp.Image = Theme.LockBg(new Size(680, 380)); });
                break;
            case 3:
                T("Display", 24, 18, 18, true); T("Icon size", 24, 70, 11, true);
                int xx = 24;
                foreach (var (n, s) in new[] { ("Small", "32"), ("Medium", "48"), ("Large", "64") })
                {
                    var size = s;
                    var rb = new RadioButton { Text = n, Left = xx, Top = 100, AutoSize = true, Checked = Cfg.Get("icon", "48") == size };
                    rb.CheckedChanged += (_, _) => { if (rb.Checked) { Cfg.Set("icon", size); Apply(); } };
                    content.Controls.Add(rb); xx += 110;
                }
                var cb = new CheckBox { Text = "Show clock and storage widget", Left = 24, Top = 150, AutoSize = true, Checked = Cfg.Get("widget", "1") == "1" };
                cb.CheckedChanged += (_, _) => { Cfg.Set("widget", cb.Checked ? "1" : "0"); Apply(); };
                content.Controls.Add(cb);
                Bt("Screen resolution and scaling...", 24, 200, 270, () => Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }));
                break;
            case 4:
                T("Security", 24, 18, 18, true); T("Change password", 24, 70, 11, true);
                T("Current password", 24, 108); var o = new TextBox { Left = 24, Top = 132, Width = 240, UseSystemPasswordChar = true }; content.Controls.Add(o);
                T("New password", 24, 170); var nw = new TextBox { Left = 24, Top = 194, Width = 240, UseSystemPasswordChar = true }; content.Controls.Add(nw);
                Bt("Update", 24, 236, 120, () =>
                {
                    if (nw.Text.Length < 1) return;
                    if (File.Exists(Auth.UserFile) && File.ReadAllText(Auth.UserFile).Trim() != Auth.Hash(o.Text)) { MessageBox.Show("Current password is wrong."); return; }
                    File.WriteAllText(Auth.UserFile, Auth.Hash(nw.Text)); o.Clear(); nw.Clear(); MessageBox.Show("Password updated.");
                });
                break;
            default:
                T("About", 24, 18, 18, true);
                T("USB OS 1.1", 24, 70, 12, true);
                T("Running from " + Paths.Root, 24, 104);
                try { var d = new DriveInfo(Path.GetPathRoot(Paths.Root)!); T($"Storage: {d.AvailableFreeSpace / 1073741824} GB free of {d.TotalSize / 1073741824} GB", 24, 130); } catch { }
                Bt("Open USB folder", 24, 180, 170, () => Process.Start(new ProcessStartInfo(Paths.Root) { UseShellExecute = true }));
                Bt("Open error log", 204, 180, 170, () => { var f = Path.Combine(Paths.Data, "error.log"); if (File.Exists(f)) Process.Start(new ProcessStartInfo(f) { UseShellExecute = true }); else MessageBox.Show("No errors logged."); });
                break;
        }
    }
}
