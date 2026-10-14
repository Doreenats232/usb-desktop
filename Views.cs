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
            File.WriteAllText(cur, tb.Text); sh.Toast("Saved " + Path.GetFileName(cur)); sh.Reload();
        }
        file.DropDownItems.Add("New", null, (_, _) => { cur = null; tb.Clear(); });
        file.DropDownItems.Add("Open…", null, (_, _) => { using var d = new OpenFileDialog { InitialDirectory = Paths.Desktop }; if (d.ShowDialog() == DialogResult.OK) { cur = d.FileName; tb.Text = File.ReadAllText(cur); } });
        file.DropDownItems.Add("Save  (Ctrl+S)", null, (_, _) => Save(false));
        file.DropDownItems.Add("Save as…", null, (_, _) => Save(true));
        ms.Items.Add(file); p.Controls.Add(tb); p.Controls.Add(ms);
        tb.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.S) { Save(false); e.SuppressKeyPress = true; } };
        p.Tag = new StateApi { Get = () => System.Text.Json.JsonSerializer.Serialize(new[] { cur ?? "", tb.Text }), Set = d => { try { var a = System.Text.Json.JsonSerializer.Deserialize<string[]>(d)!; cur = a[0] == "" ? null : a[0]; tb.Text = a[1]; } catch { } } };
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
            var b = new RoundButton { Text = key, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI", 15), Margin = new Padding(3), Cursor = Cursors.Hand };
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
            var cc = c; var b = new RoundButton { Size = new Size(28, 28), BackColor = c, FlatStyle = FlatStyle.Flat }; b.Click += (_, _) => col = cc; tools.Controls.Add(b);
        }
        Button T(string t, Action a) { var b = new RoundButton { Text = t, AutoSize = true, Height = 28 }; b.Click += (_, _) => a(); tools.Controls.Add(b); return b; }
        T("Thinner", () => size = Math.Max(1, size - 2)); T("Thicker", () => size += 2);
        T("Clear", () => { using var g = Graphics.FromImage(bmp); g.Clear(Color.White); pb.Invalidate(); });
        T("Save", () => { var f = U.Unique(Paths.Desktop, "Painting.png"); bmp.Save(f, System.Drawing.Imaging.ImageFormat.Png); sh.Reload(); sh.Toast("Saved " + Path.GetFileName(f)); });
        pb.MouseDown += (_, e) => last = e.Location; pb.MouseUp += (_, _) => last = null;
        pb.MouseMove += (_, e) =>
        {
            if (last == null || e.Button != MouseButtons.Left) return;
            using var g = Graphics.FromImage(bmp); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(col, size) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLine(pen, last.Value, e.Location); last = e.Location; pb.Invalidate();
        };
        p.Controls.Add(pb); p.Controls.Add(tools); p.Tag = new StateApi { Get = () => { try { using var ms = new MemoryStream(); bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); return Convert.ToBase64String(ms.ToArray()); } catch { return null; } }, Set = d => { try { using var im = Image.FromStream(new MemoryStream(Convert.FromBase64String(d))); using var g = Graphics.FromImage(bmp); g.Clear(Color.White); g.DrawImage(im, 0, 0, im.Width, im.Height); pb.Invalidate(); } catch { } } }; return p;
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
        Button B(string t, Action a) { var b = new RoundButton { Text = t, AutoSize = true, Height = 30 }; b.Click += (_, _) => a(); top.Controls.Add(b); return b; }
        B("Restore", () => { foreach (ListViewItem it in lv.SelectedItems) try { Bin.Restore((string)it.Tag!); } catch { } Fill(); sh.Reload(); });
        B("Empty Recycle Bin", () => { if (MessageBox.Show("Delete everything permanently?", "Recycle Bin", MessageBoxButtons.YesNo) == DialogResult.Yes) { Bin.Empty(); Fill(); } });
        p.Controls.Add(lv); p.Controls.Add(top); Fill(); return p;
    }

    public static Control Files(DesktopForm sh, string start)
    {
        var p = new Panel(); var top = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(240, 240, 245) };
        var path = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Segoe UI", 10) };
        var up = new RoundButton { Glyph = "up", Width = 40, Dock = DockStyle.Left }; var nf = new RoundButton { Text = "New folder", Width = 110, Dock = DockStyle.Right }; var sb = new TextBox { Dock = DockStyle.Right, Width = 150, Font = new Font("Segoe UI", 10), PlaceholderText = "Search" };
        top.Controls.Add(path); top.Controls.Add(up); top.Controls.Add(nf); top.Controls.Add(sb);
        var il = new ImageList { ImageSize = Ui.D(40, 40), ColorDepth = ColorDepth.Depth32Bit };
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
        void Sel(bool c) { clip = lv.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToList(); cutMode = c; sh.Toast((c ? "Cut " : "Copied ") + clip.Count + " item(s)"); }
        void Paste()
        {
            foreach (var s in clip) { try { var d = U.Unique(cur, Path.GetFileName(s)); if (Directory.Exists(s)) { if (cutMode) Directory.Move(s, d); else CopyDir(s, d); } else if (cutMode) File.Move(s, d); else File.Copy(s, d); } catch (Exception ex) { sh.Toast(ex.Message); } }
            if (cutMode) clip.Clear(); Go(cur); sh.Reload();
        }
        m.Items.Add("Copy", null, (_, _) => Sel(false)); m.Items.Add("Cut", null, (_, _) => Sel(true)); m.Items.Add("Paste", null, (_, _) => Paste());
        m.Items.Add("Compress to zip", null, (_, _) =>
        {
            foreach (ListViewItem it in lv.SelectedItems)
                try { var t = (string)it.Tag!; var z = U.Unique(cur, Path.GetFileNameWithoutExtension(t) + ".zip"); if (Directory.Exists(t)) System.IO.Compression.ZipFile.CreateFromDirectory(t, z); else { using var za = System.IO.Compression.ZipFile.Open(z, System.IO.Compression.ZipArchiveMode.Create); System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(za, t, Path.GetFileName(t)); } }
                catch (Exception ex) { sh.Toast(ex.Message); }
            Go(cur);
        });
        m.Items.Add("Extract here", null, (_, _) =>
        {
            foreach (ListViewItem it in lv.SelectedItems) { var t = (string)it.Tag!; if (t.EndsWith(".zip")) try { System.IO.Compression.ZipFile.ExtractToDirectory(t, U.Unique(cur, Path.GetFileNameWithoutExtension(t))); } catch (Exception ex) { sh.Toast(ex.Message); } }
            Go(cur);
        });
        m.Items.Add("Refresh", null, (_, _) => Go(cur)); lv.ContextMenuStrip = m;
        lv.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.C) Sel(false); else if (e.Control && e.KeyCode == Keys.X) Sel(true); else if (e.Control && e.KeyCode == Keys.V) Paste(); };
        p.Controls.Add(lv); p.Controls.Add(top); Go(start); p.Tag = new StateApi { Get = () => cur, Set = d => { try { if (Directory.Exists(d)) Go(d); } catch { } } }; return p;
    }

    public static Control Settings(DesktopForm sh, int start)
    {
        var root = new Panel { BackColor = Ui.Surface }; Ui.Own(root);
        var nav = new Panel { Dock = DockStyle.Left, Width = Ui.D(232), BackColor = Ui.Surface2 };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface };
        root.Controls.Add(host); root.Controls.Add(nav);
        string[] names = { "Personalization", "Display", "Accounts", "Lock screen", "Browser", "Security", "Store", "About & updates" };
        string[] icons = { "paint", "vm", "user", "vault", "browser", "tm", "store", "settings" };
        var navBtns = new List<RoundButton>(); Action<int> show = _ => { };
        for (int i = 0; i < names.Length; i++)
        {
            int k = i; var b = new RoundButton { Text = names[i], Ico = Icons.App(icons[i]), TextAlign = ContentAlignment.MiddleLeft, Font = Ui.F(10.5f), BackColor = nav.BackColor, Radius = 10 };
            b.Bounds = new Rectangle(Ui.D(10), Ui.D(14) + i * Ui.D(48), Ui.D(232) - Ui.D(20), Ui.D(42)); b.Click += (_, _) => show(k); nav.Controls.Add(b); navBtns.Add(b);
        }
        string? Pick() { using var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" }; return d.ShowDialog() == DialogResult.OK ? d.FileName : null; }

        show = k =>
        {
            foreach (Control c in host.Controls.Cast<Control>().ToList()) { host.Controls.Remove(c); c.Dispose(); }
            for (int q = 0; q < navBtns.Count; q++) navBtns[q].BackColor = q == k ? Theme.Accent : Ui.Surface2;
            var pg = new PagePanel(); host.Controls.Add(pg);
            switch (k)
            {
                case 0:
                {
                    pg.H("Personalization"); pg.P("Choose a background", true);
                    var grid = pg.Row(8);
                    for (int i = 0; i < Theme.P.Length; i++)
                    {
                        int idx = i; bool on = Cfg.Get("wallimg") == "" && Theme.Idx == i;
                        var th = new Thumb(Theme.Preset(new Size(Ui.D(300), Ui.D(170)), i), Theme.P[i].N, on) { Margin = new Padding(0, 0, Ui.D(14), Ui.D(10)) };
                        th.Click += (_, _) => { Cfg.Set("wp", idx.ToString()); Cfg.Set("wallimg", ""); sh.ApplyLook(); show(0); };
                        grid.Controls.Add(th);
                    }
                    var r = pg.Row(8); pg.Btn(r, "Use my own picture…", () => { var f = Pick(); if (f != null) { Cfg.Set("wallimg", U.SaveImg(f, "wallpaper")); sh.ApplyLook(); show(0); } });
                    break;
                }
                case 1:
                {
                    pg.H("Display"); var scr = Screen.PrimaryScreen!.Bounds; pg.P($"Resolution: {scr.Width} × {scr.Height}", true);
                    pg.P("Desktop icon size", false, true, 11, 14); var r = pg.Row(4);
                    foreach (var (n, v) in new[] { ("Small", 44), ("Medium", 56), ("Large", 72) }) { var vv = v; pg.Btn(r, n, () => { Cfg.Set("icon", vv.ToString()); sh.Reload(); show(1); }, Cfg.Int("icon", 56) == v); }
                    pg.Gap(6);
                    pg.Switch("Show desktop icons", Cfg.Get("icons", "1") == "1", on => { Cfg.Set("icons", on ? "1" : "0"); sh.Reload(); });
                    pg.Switch("24-hour clock", Cfg.Get("h24", "1") == "1", on => Cfg.Set("h24", on ? "1" : "0"));
                    pg.Switch("Show clock widget", Cfg.Get("wclock", "1") == "1", on => { Cfg.Set("wclock", on ? "1" : "0"); sh.ApplyWidgets(); });
                    pg.Switch("Show sticky note", Cfg.Get("wnote", "1") == "1", on => { Cfg.Set("wnote", on ? "1" : "0"); sh.ApplyWidgets(); });
                    break;
                }
                case 2:
                {
                    pg.H("Accounts"); var r = pg.Row(4);
                    var av = new PictureBox { Size = Ui.D(110, 110), Image = U.Avatar(Ui.D(110)), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Margin = new Padding(0, 0, Ui.D(18), Ui.D(6)) }; r.Controls.Add(av);
                    pg.Btn(r, "Change picture…", () => { var f = Pick(); if (f != null && U.SetAvatar(f)) { sh.ApplyLook(); show(2); } }, false).Margin = new Padding(0, Ui.D(36), 0, 0);
                    pg.P("Display name", false, true, 10.5f, 12); var rn = pg.Row(2); var (_, tn) = pg.Field(rn, "Your name", 260, false, Cfg.Get("name", Environment.UserName));
                    pg.Btn(rn, "Save name", () => { if (tn.Text.Trim() != "") { Cfg.Set("name", tn.Text.Trim()); sh.ApplyLook(); sh.Toast("Name saved"); } });
                    pg.P("Change password", false, true, 10.5f, 14);
                    var r1 = pg.Row(2); var (_, po) = pg.Field(r1, "Current password", 260, true);
                    var r2 = pg.Row(2); var (_, pn) = pg.Field(r2, "New password", 260, true);
                    pg.Btn(r2, "Change password", () =>
                    {
                        var uf = Path.Combine(Paths.Data, "user.txt");
                        if (!U.Check(po.Text, File.ReadAllText(uf).Trim())) { sh.Toast("Current password is wrong"); return; }
                        if (pn.Text.Length < 1) { sh.Toast("Type a new password"); return; }
                        File.WriteAllText(uf, U.MakeHash(pn.Text)); po.Clear(); pn.Clear(); sh.Toast("Password changed");
                    });
                    break;
                }
                case 3:
                {
                    pg.H("Lock screen"); pg.P("This picture shows on the sign-in screen", true);
                    var pb = new PictureBox { Size = Ui.D(380, 214), Image = Theme.Lock(Ui.D(380, 214)), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, Ui.D(8), 0, Ui.D(8)) }; pg.Controls.Add(pb);
                    var r = pg.Row(4); pg.Btn(r, "Choose picture…", () => { var f = Pick(); if (f != null) { Cfg.Set("lockimg", U.SaveImg(f, "lock")); show(3); } });
                    pg.Btn(r, "Use my wallpaper", () => { Cfg.Set("lockimg", ""); show(3); }, false);
                    break;
                }
                case 4:
                {
                    pg.H("Browser"); pg.P("Search engine", false, true, 11, 6); var r = pg.Row(4); var cur = Cfg.Get("engine", "google");
                    foreach (var (n, key) in new[] { ("Google", "google"), ("Bing", "bing"), ("DuckDuckGo", "duck") }) { var kk = key; pg.Btn(r, n, () => { Cfg.Set("engine", kk); show(4); }, cur == key); }
                    pg.Gap(6); pg.Switch("Keep me signed in to websites (accounts travel with this USB)", Cfg.Get("keeplogins", "1") == "1", on => Cfg.Set("keeplogins", on ? "1" : "0"));
                    pg.P("Your logins and saved passwords are stored in the data folder on the USB. Anyone with the USB can use them, so keep your password and the USB safe.", true, false, 10, 10);
                    var r2 = pg.Row(8); pg.Btn(r2, "Open downloads folder", () => { var d = Path.Combine(Paths.Desktop, "Downloads"); Directory.CreateDirectory(d); U.Open(d); }, false);
                    break;
                }
                case 5:
                {
                    pg.H("Security"); pg.P("Auto-lock after being idle (minutes, 0 = off)", false, false, 11); var r = pg.Row(4); pg.Num(r, 0, 240, Cfg.Int("idle", 10), 1, v => Cfg.Set("idle", v.ToString()));
                    pg.Switch("Close the desktop when the USB is removed", Cfg.Get("usbwatch", "1") == "1", on => Cfg.Set("usbwatch", on ? "1" : "0"));
                    pg.Switch("Sleep instead of shut down (resume on any PC)", Cfg.Get("sleep", "0") == "1", on => Cfg.Set("sleep", on ? "1" : "0"));
                    pg.P("Sleep saves your open windows and browser tabs, encrypted with your login password, and brings them back the next time you sign in, on any computer. Apps already open on the PC itself (like its own Chrome) can't be saved.", true, false, 10, 6);
                    pg.P("Passwords are salted and hashed (PBKDF2). Change yours in Accounts.", true, false, 10, 12);
                    break;
                }
                case 6:
                {
                    pg.H("MLD Store"); pg.P("Store website address", false, true, 11); var r = pg.Row(4); var (_, su) = pg.Field(r, "https://your-store.example.com", 400, false, Cfg.Get("store_url"));
                    pg.Btn(r, "Save", () => { Cfg.Set("store_url", su.Text.Trim()); sh.Toast("Store address saved"); });
                    pg.P("Apps you download in the store go to your apps folder and show in the Start menu, with live install progress.", true, false, 10, 10);
                    break;
                }
                default:
                {
                    pg.H("About & updates");
                    var root2 = Path.GetPathRoot(Paths.Root)!; string free = ""; try { var di = new DriveInfo(root2); free = $"{di.AvailableFreeSpace / 1073741824} GB free of {di.TotalSize / 1073741824} GB"; } catch { }
                    pg.P("USB Desktop", false, true, 13); pg.P("Version 1.0." + Updater.Cur, true);
                    pg.P("Computer: " + Cfg.Get("name", Environment.UserName) + "'s USB PC"); pg.P("Drive: " + root2 + "   " + free); pg.P("Running from: " + Paths.Root, true, false, 9.5f);
                    var ul = pg.P("", true, false, 10.5f, 10); var r = pg.Row(6);
                    pg.Btn(r, "Check for updates", async () =>
                    {
                        ul.Text = "Checking…";
                        try { var u = await Updater.Check(); if (u.latest > Updater.Cur) { ul.Text = "Version 1.0." + u.latest + " is available."; await Updater.Run(u.latest, u.url, true); } else ul.Text = "You're up to date."; }
                        catch (Exception ex) { ul.Text = "Couldn't check: " + ex.Message; }
                    });
                    break;
                }
            }
        };
        show(Math.Clamp(start, 0, 7)); return root;
    }
}

class Thumb : Control
{
    readonly Bitmap img; readonly string cap; readonly bool on; bool hover;
    public Thumb(Bitmap img, string cap, bool on)
    {
        this.img = img; this.cap = cap; this.on = on; Size = new Size(Ui.D(200), Ui.D(142)); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.Clear(Ui.PBg(this, Ui.Surface));
        var r = new Rectangle(Ui.D(3), Ui.D(3), Width - Ui.D(7), Height - Ui.D(34)); using var p = U.Round(r, Ui.D(10));
        var st = g.Save(); g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect); g.DrawImage(img, r); g.Restore(st);
        using var pen = new Pen(on ? Theme.Accent : hover ? Color.FromArgb(150, 154, 200) : Ui.Line, on ? Ui.D(3) : Ui.D(1)); g.DrawPath(pen, p);
        TextRenderer.DrawText(g, cap + (on ? "  ✓" : ""), Ui.F(9.5f), new Rectangle(Ui.D(3), Height - Ui.D(28), Width - Ui.D(6), Ui.D(24)), on ? Ui.Text : Ui.Sub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
