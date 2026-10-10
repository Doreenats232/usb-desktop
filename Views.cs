using System.Data;
using System.Diagnostics;
using System.Globalization;
namespace UsbDesktop;

static class Views
{
    static List<string> clip = new(); static bool cutMode;
    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)));
        foreach (var x in Directory.GetDirectories(s)) CopyDir(x, Path.Combine(d, Path.GetFileName(x)));
    }

    public static Control Notepad(DesktopForm sh, string? path)
    {
        var p = new Panel();
        var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 11), AcceptsTab = true, BorderStyle = BorderStyle.None };
        var ms = new MenuStrip { Dock = DockStyle.Top }; var file = new ToolStripMenuItem("File");
        string? cur = path; if (path != null && File.Exists(path)) tb.Text = File.ReadAllText(path);
        void Save(bool asNew)
        {
            if (asNew || cur == null) { using var d = new SaveFileDialog { InitialDirectory = Paths.Desktop, FileName = "Untitled.txt" }; if (d.ShowDialog() != DialogResult.OK) return; cur = d.FileName; }
            File.WriteAllText(cur, tb.Text); sh.Toast("💾 Saved " + Path.GetFileName(cur)); sh.Reload();
        }
        file.DropDownItems.Add("New", null, (_, _) => { cur = null; tb.Clear(); });
        file.DropDownItems.Add("Open…", null, (_, _) => { using var d = new OpenFileDialog { InitialDirectory = Paths.Desktop }; if (d.ShowDialog() == DialogResult.OK) { cur = d.FileName; tb.Text = File.ReadAllText(cur); } });
        file.DropDownItems.Add("Save  (Ctrl+S)", null, (_, _) => Save(false));
        file.DropDownItems.Add("Save as…", null, (_, _) => Save(true));
        ms.Items.Add(file); p.Controls.Add(tb); p.Controls.Add(ms);
        tb.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.S) { Save(false); e.SuppressKeyPress = true; } };
        return p;
    }

    public static Control Calc()
    {
        var p = new Panel { BackColor = Color.FromArgb(32, 32, 44) };
        var d = new Label { Dock = DockStyle.Top, Height = 90, ForeColor = Color.White, Font = new Font("Segoe UI", 26), TextAlign = ContentAlignment.MiddleRight, Text = "0", Padding = new Padding(8) };
        var g = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 4, Padding = new Padding(4) };
        for (int i = 0; i < 4; i++) g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int i = 0; i < 5; i++) g.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        string[] k = { "C", "⌫", "(", ")", "7", "8", "9", "÷", "4", "5", "6", "×", "1", "2", "3", "−", "0", ".", "=", "+" };
        string e = "";
        foreach (var key in k)
        {
            var b = new Button { Text = key, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI", 15), Margin = new Padding(3), Cursor = Cursors.Hand };
            b.BackColor = key == "=" ? Theme.Accent : "÷×−+".Contains(key) ? Color.FromArgb(70, 70, 100) : Color.FromArgb(52, 52, 68); b.FlatAppearance.BorderSize = 0;
            var kk = key;
            b.Click += (_, _) =>
            {
                if (kk == "C") e = ""; else if (kk == "⌫") { if (e.Length > 0) e = e[..^1]; }
                else if (kk == "=")
                {
                    try { var x = new DataTable().Compute(e.Replace("×", "*").Replace("÷", "/").Replace("−", "-"), null); e = Convert.ToString(x, CultureInfo.InvariantCulture) ?? ""; }
                    catch { e = ""; d.Text = "Error"; return; }
                }
                else e += kk;
                d.Text = e == "" ? "0" : e;
            };
            g.Controls.Add(b);
        }
        p.Controls.Add(g); p.Controls.Add(d); return p;
    }

    public static Control Term()
    {
        var p = new Panel { BackColor = Color.FromArgb(12, 12, 20) };
        var o = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 20), ForeColor = Color.FromArgb(158, 225, 203), Font = new Font("Consolas", 11), BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Text = "USB Desktop terminal. Type a command and press Enter.\r\n" };
        var i = new TextBox { Dock = DockStyle.Bottom, BackColor = Color.FromArgb(24, 24, 36), ForeColor = Color.White, Font = new Font("Consolas", 12), BorderStyle = BorderStyle.FixedSingle };
        p.Controls.Add(o); p.Controls.Add(i);
        i.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return; e.SuppressKeyPress = true; var c = i.Text; i.Clear(); o.AppendText("> " + c + "\r\n");
            Task.Run(() =>
            {
                string r;
                try
                {
                    using var ps = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + c) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Paths.Root })!;
                    r = ps.StandardOutput.ReadToEnd() + ps.StandardError.ReadToEnd();
                }
                catch (Exception ex) { r = ex.Message; }
                o.BeginInvoke(new Action(() => o.AppendText(r.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n")));
            });
        };
        return p;
    }

    public static Control PaintApp(DesktopForm sh)
    {
        var p = new Panel(); var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(240, 240, 245), Padding = new Padding(6) };
        var bmp = new Bitmap(1600, 1000); using (var g0 = Graphics.FromImage(bmp)) g0.Clear(Color.White);
        var pb = new PictureBox { Dock = DockStyle.Fill, Image = bmp, SizeMode = PictureBoxSizeMode.Normal, BackColor = Color.White, Cursor = Cursors.Cross };
        Color col = Color.Black; int size = 4; Point? last = null;
        foreach (var c in new[] { Color.Black, Color.Red, Color.Orange, Color.Green, Color.Blue, Color.Purple, Color.White })
        {
            var cc = c; var b = new Button { Size = new Size(28, 28), BackColor = c, FlatStyle = FlatStyle.Flat }; b.Click += (_, _) => col = cc; tools.Controls.Add(b);
        }
        Button T(string t, Action a) { var b = new Button { Text = t, AutoSize = true, Height = 28 }; b.Click += (_, _) => a(); tools.Controls.Add(b); return b; }
        T("Thinner", () => size = Math.Max(1, size - 2)); T("Thicker", () => size += 2);
        T("Clear", () => { using var g = Graphics.FromImage(bmp); g.Clear(Color.White); pb.Invalidate(); });
        T("Save", () => { var f = U.Unique(Paths.Desktop, "Painting.png"); bmp.Save(f, System.Drawing.Imaging.ImageFormat.Png); sh.Reload(); sh.Toast("🎨 Saved " + Path.GetFileName(f)); });
        pb.MouseDown += (_, e) => last = e.Location; pb.MouseUp += (_, _) => last = null;
        pb.MouseMove += (_, e) =>
        {
            if (last == null || e.Button != MouseButtons.Left) return;
            using var g = Graphics.FromImage(bmp); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(col, size) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(pen, last.Value, e.Location); last = e.Location; pb.Invalidate();
        };
        p.Controls.Add(pb); p.Controls.Add(tools); return p;
    }

    public static Control BinView(DesktopForm sh)
    {
        var p = new Panel(); var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(240, 240, 245), Padding = new Padding(6) };
        var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, Font = new Font("Segoe UI", 10) };
        lv.Columns.Add("Name", 280); lv.Columns.Add("Deleted from", 260);
        void Fill()
        {
            lv.Items.Clear();
            foreach (var d in Bin.Items()) { var o = File.Exists(d + ".orig") ? File.ReadAllText(d + ".orig") : ""; lv.Items.Add(new ListViewItem(new[] { Bin.Name(d), Path.GetDirectoryName(o) ?? "" }) { Tag = d }); }
        }
        Button B(string t, Action a) { var b = new Button { Text = t, AutoSize = true, Height = 30 }; b.Click += (_, _) => a(); top.Controls.Add(b); return b; }
        B("↩ Restore", () => { foreach (ListViewItem it in lv.SelectedItems) try { Bin.Restore((string)it.Tag!); } catch { } Fill(); sh.Reload(); });
        B("🗑 Empty Recycle Bin", () => { if (MessageBox.Show("Delete everything permanently?", "Recycle Bin", MessageBoxButtons.YesNo) == DialogResult.Yes) { Bin.Empty(); Fill(); } });
        p.Controls.Add(lv); p.Controls.Add(top); Fill(); return p;
    }

    public static Control Files(DesktopForm sh, string start)
    {
        var p = new Panel(); var top = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(240, 240, 245) };
        var path = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Segoe UI", 10) };
        var up = new Button { Text = "↑", Width = 40, Dock = DockStyle.Left }; var nf = new Button { Text = "＋ Folder", Width = 90, Dock = DockStyle.Right }; var sb = new TextBox { Dock = DockStyle.Right, Width = 150, Font = new Font("Segoe UI", 10), PlaceholderText = "Search" };
        top.Controls.Add(path); top.Controls.Add(up); top.Controls.Add(nf); top.Controls.Add(sb);
        var il = new ImageList { ImageSize = new Size(40, 40), ColorDepth = ColorDepth.Depth32Bit };
        var lv = new ListView { Dock = DockStyle.Fill, View = View.LargeIcon, LargeImageList = il, LabelEdit = true, Font = new Font("Segoe UI", 9.5f) };
        string cur = start, filter = "";
        void Go(string d)
        {
            try
            {
                var items = Directory.GetFileSystemEntries(d).OrderByDescending(Directory.Exists).ThenBy(x => x).ToList(); cur = d; path.Text = d; lv.Items.Clear();
                foreach (var e in items.Where(x => filter == "" || Path.GetFileName(x).Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    var key = Directory.Exists(e) ? "dir" : Path.GetExtension(e).ToLower(); if (!il.Images.ContainsKey(key)) il.Images.Add(key, U.IconFor(e));
                    lv.Items.Add(new ListViewItem(Path.GetFileName(e), key) { Tag = e });
                }
            }
            catch (Exception ex) { sh.Toast(ex.Message); }
        }
        sb.TextChanged += (_, _) => { filter = sb.Text; Go(cur); };
        up.Click += (_, _) => { var pr = Directory.GetParent(cur); if (pr != null) Go(pr.FullName); };
        nf.Click += (_, _) => { Directory.CreateDirectory(U.Unique(cur, "New folder")); Go(cur); sh.Reload(); };
        lv.ItemActivate += (_, _) => { if (lv.SelectedItems.Count > 0) { var t = (string)lv.SelectedItems[0].Tag!; if (Directory.Exists(t)) Go(t); else sh.OpenFile(t); } };
        lv.AfterLabelEdit += (_, e) =>
        {
            var it = lv.Items[e.Item]; var old = (string)it.Tag!; if (string.IsNullOrWhiteSpace(e.Label)) { e.CancelEdit = true; return; }
            try { var n = Path.Combine(Path.GetDirectoryName(old)!, e.Label); if (Directory.Exists(old)) Directory.Move(old, n); else File.Move(old, n); it.Tag = n; sh.Reload(); } catch { e.CancelEdit = true; }
        };
        var m = new ContextMenuStrip();
        m.Items.Add("Open", null, (_, _) => { if (lv.SelectedItems.Count > 0) { var t = (string)lv.SelectedItems[0].Tag!; if (Directory.Exists(t)) Go(t); else sh.OpenFile(t); } });
        m.Items.Add("Rename", null, (_, _) => { if (lv.SelectedItems.Count > 0) lv.SelectedItems[0].BeginEdit(); });
        m.Items.Add("Delete", null, (_, _) => { foreach (ListViewItem it in lv.SelectedItems) try { Bin.Trash((string)it.Tag!); } catch { } Go(cur); sh.Reload(); sh.Toast("Moved to Recycle Bin"); });
        void Sel(bool c) { clip = lv.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToList(); cutMode = c; sh.Toast((c ? "✂ Cut " : "📋 Copied ") + clip.Count + " item(s)"); }
        void Paste()
        {
            foreach (var s in clip) { try { var d = U.Unique(cur, Path.GetFileName(s)); if (Directory.Exists(s)) { if (cutMode) Directory.Move(s, d); else CopyDir(s, d); } else if (cutMode) File.Move(s, d); else File.Copy(s, d); } catch (Exception ex) { sh.Toast(ex.Message); } }
            if (cutMode) clip.Clear(); Go(cur); sh.Reload();
        }
        m.Items.Add("Copy", null, (_, _) => Sel(false)); m.Items.Add("Cut", null, (_, _) => Sel(true)); m.Items.Add("Paste", null, (_, _) => Paste());
        m.Items.Add("🗜  Compress to zip", null, (_, _) =>
        {
            foreach (ListViewItem it in lv.SelectedItems)
                try { var t = (string)it.Tag!; var z = U.Unique(cur, Path.GetFileNameWithoutExtension(t) + ".zip"); if (Directory.Exists(t)) System.IO.Compression.ZipFile.CreateFromDirectory(t, z); else { using var za = System.IO.Compression.ZipFile.Open(z, System.IO.Compression.ZipArchiveMode.Create); System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(za, t, Path.GetFileName(t)); } }
                catch (Exception ex) { sh.Toast(ex.Message); }
            Go(cur);
        });
        m.Items.Add("📦  Extract here", null, (_, _) =>
        {
            foreach (ListViewItem it in lv.SelectedItems) { var t = (string)it.Tag!; if (t.EndsWith(".zip")) try { System.IO.Compression.ZipFile.ExtractToDirectory(t, U.Unique(cur, Path.GetFileNameWithoutExtension(t))); } catch (Exception ex) { sh.Toast(ex.Message); } }
            Go(cur);
        });
        m.Items.Add("Refresh", null, (_, _) => Go(cur)); lv.ContextMenuStrip = m;
        lv.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.C) Sel(false); else if (e.Control && e.KeyCode == Keys.X) Sel(true); else if (e.Control && e.KeyCode == Keys.V) Paste(); };
        p.Controls.Add(lv); p.Controls.Add(top); Go(start); return p;
    }

    public static Control Settings(DesktopForm sh, int start)
    {
        var root = new Panel(); var nav = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.FromArgb(243, 243, 248) };
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
        root.Controls.Add(page); root.Controls.Add(nav);
        string[] names = { "🎨  Personalization", "🖥  Display", "👤  Accounts", "🔒  Lock screen", "🛡  Security", "🛍  Store", "ℹ  About & updates" };
        Action<int> show = _ => { };
        for (int i = 0; i < names.Length; i++)
        {
            int k = i; var b = new Button { Text = names[i], Bounds = new Rectangle(8, 14 + i * 46, 184, 40), FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Emoji", 10.5f), BackColor = nav.BackColor };
            b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => show(k); nav.Controls.Add(b);
        }
        Label L(string t, int x, int y, float sz = 10, bool bold = false) { var l = new Label { Text = t, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", sz, bold ? FontStyle.Bold : FontStyle.Regular) }; page.Controls.Add(l); return l; }
        Button B(string t, int x, int y, int w, Action a) { var b = new Button { Text = t, Bounds = new Rectangle(x, y, w, 34), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => a(); page.Controls.Add(b); return b; }
        TextBox T(int x, int y, int w, bool pw = false) { var t = new TextBox { Bounds = new Rectangle(x, y, w, 28), UseSystemPasswordChar = pw, Font = new Font("Segoe UI", 10.5f) }; page.Controls.Add(t); return t; }
        string? Pick() { using var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" }; return d.ShowDialog() == DialogResult.OK ? d.FileName : null; }

        show = k =>
        {
            page.Controls.Clear();
            switch (k)
            {
                case 0:
                    L("Personalization", 24, 18, 18, true); L("Choose a background", 24, 62, 11);
                    for (int i = 0; i < Theme.P.Length; i++)
                    {
                        int idx = i; bool on = Cfg.Get("wallimg") == "" && Theme.Idx == i;
                        var pb = new PictureBox { Bounds = new Rectangle(24 + i % 3 * 200, 96 + i / 3 * 130, 184, 104), Image = Theme.Preset(new Size(184, 104), i), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand, BorderStyle = on ? BorderStyle.Fixed3D : BorderStyle.None };
                        pb.Click += (_, _) => { Cfg.Set("wp", idx.ToString()); Cfg.Set("wallimg", ""); sh.ApplyLook(); show(0); };
                        page.Controls.Add(pb); L(Theme.P[i].N + (on ? "  ✓" : ""), 24 + i % 3 * 200, 202 + i / 3 * 130, 9);
                    }
                    B("🖼  Use my own picture…", 24, 360, 220, () => { var f = Pick(); if (f != null) { Cfg.Set("wallimg", U.SaveImg(f, "wallpaper")); sh.ApplyLook(); show(0); } });
                    break;
                case 1:
                    L("Display", 24, 18, 18, true); var scr = Screen.PrimaryScreen!.Bounds; L($"Resolution: {scr.Width} × {scr.Height}", 24, 62, 11);
                    L("Desktop icon size", 24, 104, 11, true);
                    int x = 24; foreach (var (n, v) in new[] { ("Small", 40), ("Medium", 52), ("Large", 68) }) { var vv = v; B(n + (Cfg.Int("icon", 52) == v ? " ✓" : ""), x, 134, 100, () => { Cfg.Set("icon", vv.ToString()); sh.Reload(); show(1); }); x += 110; }
                    var c1 = new CheckBox { Text = "Show desktop icons", Checked = Cfg.Get("icons", "1") == "1", Location = new Point(24, 190), AutoSize = true, Font = new Font("Segoe UI", 10.5f) };
                    c1.CheckedChanged += (_, _) => { Cfg.Set("icons", c1.Checked ? "1" : "0"); sh.Reload(); };
                    var c2 = new CheckBox { Text = "24-hour clock", Checked = Cfg.Get("h24", "1") == "1", Location = new Point(24, 224), AutoSize = true, Font = new Font("Segoe UI", 10.5f) };
                    c2.CheckedChanged += (_, _) => Cfg.Set("h24", c2.Checked ? "1" : "0");
                    var c3 = new CheckBox { Text = "Show clock widget", Checked = Cfg.Get("wclock", "1") == "1", Location = new Point(24, 258), AutoSize = true, Font = new Font("Segoe UI", 10.5f) };
                    c3.CheckedChanged += (_, _) => { Cfg.Set("wclock", c3.Checked ? "1" : "0"); sh.ApplyWidgets(); };
                    var c4 = new CheckBox { Text = "Show sticky note", Checked = Cfg.Get("wnote", "1") == "1", Location = new Point(24, 292), AutoSize = true, Font = new Font("Segoe UI", 10.5f) };
                    c4.CheckedChanged += (_, _) => { Cfg.Set("wnote", c4.Checked ? "1" : "0"); sh.ApplyWidgets(); };
                    page.Controls.Add(c1); page.Controls.Add(c2); page.Controls.Add(c3); page.Controls.Add(c4); break;
                case 2:
                    L("Accounts", 24, 18, 18, true);
                    page.Controls.Add(new PictureBox { Bounds = new Rectangle(24, 62, 120, 120), Image = U.Avatar(120), SizeMode = PictureBoxSizeMode.Zoom });
                    B("📷  Change picture…", 164, 100, 180, () => { var f = Pick(); if (f != null && U.SetAvatar(f)) { sh.ApplyLook(); show(2); } });
                    L("Display name", 24, 204, 10, true); var tn = T(24, 230, 240); tn.Text = Cfg.Get("name", Environment.UserName);
                    B("Save name", 280, 227, 110, () => { if (tn.Text.Trim() != "") { Cfg.Set("name", tn.Text.Trim()); sh.ApplyLook(); sh.Toast("Name saved"); } });
                    L("Change password", 24, 290, 10, true); L("Current", 24, 320, 9); var po = T(24, 342, 240, true); L("New", 24, 376, 9); var pn = T(24, 398, 240, true);
                    B("Change password", 24, 436, 160, () =>
                    {
                        var uf = Path.Combine(Paths.Data, "user.txt");
                        if (!U.Check(po.Text, File.ReadAllText(uf).Trim())) { sh.Toast("Current password is wrong"); return; }
                        if (pn.Text.Length < 1) { sh.Toast("Type a new password"); return; }
                        File.WriteAllText(uf, U.MakeHash(pn.Text)); po.Clear(); pn.Clear(); sh.Toast("🔑 Password changed");
                    });
                    break;
                case 3:
                    L("Lock screen", 24, 18, 18, true); L("This picture shows on the sign-in screen", 24, 62, 11);
                    page.Controls.Add(new PictureBox { Bounds = new Rectangle(24, 96, 360, 200), Image = Theme.Lock(new Size(360, 200)), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle });
                    B("🖼  Choose picture…", 24, 312, 180, () => { var f = Pick(); if (f != null) { Cfg.Set("lockimg", U.SaveImg(f, "lock")); show(3); } });
                    B("Use my wallpaper", 214, 312, 170, () => { Cfg.Set("lockimg", ""); show(3); }); break;
                case 4:
                    L("Security", 24, 18, 18, true); L("Auto-lock after being idle (minutes, 0 = off)", 24, 66, 11);
                    var ni = new NumericUpDown { Bounds = new Rectangle(24, 96, 90, 28), Minimum = 0, Maximum = 240, Value = Math.Clamp(Cfg.Int("idle", 10), 0, 240), Font = new Font("Segoe UI", 10.5f) };
                    ni.ValueChanged += (_, _) => Cfg.Set("idle", ((int)ni.Value).ToString()); page.Controls.Add(ni);
                    var cu = new CheckBox { Text = "Close the desktop when the USB is removed", Checked = Cfg.Get("usbwatch", "1") == "1", Location = new Point(24, 148), AutoSize = true, Font = new Font("Segoe UI", 10.5f) };
                    cu.CheckedChanged += (_, _) => Cfg.Set("usbwatch", cu.Checked ? "1" : "0"); page.Controls.Add(cu);
                    L("Passwords are salted and hashed (PBKDF2). Change yours in Accounts.", 24, 196, 10); break;
                case 5:
                    L("MLD Store", 24, 18, 18, true); L("Store website address", 24, 66, 11); var su = T(24, 96, 420); su.Text = Cfg.Get("store_url");
                    B("Save", 460, 93, 90, () => { Cfg.Set("store_url", su.Text.Trim()); sh.Toast("Store address saved"); });
                    L("Apps you download in the store go to your apps folder and show in the Start menu.", 24, 140, 10); break;
                default:
                    L("About", 24, 18, 18, true);
                    var root2 = Path.GetPathRoot(Paths.Root)!; string free = ""; try { var di = new DriveInfo(root2); free = $"{di.AvailableFreeSpace / 1073741824} GB free of {di.TotalSize / 1073741824} GB"; } catch { }
                    L("USB Desktop  v2.0", 24, 66, 12, true); L("Computer: " + Cfg.Get("name", Environment.UserName) + "'s USB PC", 24, 100, 11);
                    L("Drive: " + root2 + "   " + free, 24, 132, 11); L("Running from: " + Paths.Root, 24, 164, 10); L("Version 1.0." + Updater.Cur, 24, 196, 10); var ul = L("", 24, 276, 10);
                    B("⬆  Check for updates", 24, 230, 200, async () =>
                    {
                        ul.Text = "Checking...";
                        try { var u = await Updater.Check(); if (u.latest > Updater.Cur) { ul.Text = "Version " + u.latest + " found. Downloading..."; await Updater.Apply(u.url); } else ul.Text = "You're up to date ✅"; }
                        catch (Exception ex) { ul.Text = "Couldn't check: " + ex.Message; }
                    }); break;
            }
        };
        show(Math.Clamp(start, 0, 6)); return root;
    }
}
