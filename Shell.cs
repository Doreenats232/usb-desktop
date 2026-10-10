using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace UsbDesktop;

class Surface : Panel
{
    public Bitmap? Bg;
    public Surface() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e) { if (Bg != null) e.Graphics.DrawImage(Bg, 0, 0); else e.Graphics.Clear(Color.Black); }
}

class DeskIcon : Control
{
    public string Label; public Image Img; public string? FilePath; public Action Act; public bool Sel; public int Sz;
    public Action<DeskIcon>? Picked, Dropped;
    Point dn, st; bool down, moved;
    public DeskIcon(string label, Image img, Action act, int sz)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent; Label = label; Img = img; Act = act; Sz = sz; Font = new Font("Segoe UI", 9f);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Sel) { using var b = new SolidBrush(Color.FromArgb(80, 255, 255, 255)); using var p = U.Round(new Rectangle(1, 1, Width - 3, Height - 3), 8); g.FillPath(b, p); }
        g.DrawImage(Img, (Width - Sz) / 2, 8, Sz, Sz);
        var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top;
        TextRenderer.DrawText(g, Label, Font, new Rectangle(3, Sz + 13, Width - 4, Height - Sz - 14), Color.Black, fl);
        TextRenderer.DrawText(g, Label, Font, new Rectangle(2, Sz + 12, Width - 4, Height - Sz - 14), Color.White, fl);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Picked?.Invoke(this); if (e.Button == MouseButtons.Left) { down = true; moved = false; dn = Cursor.Position; st = Location; } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!down) return; var d = Cursor.Position;
        if (Math.Abs(d.X - dn.X) + Math.Abs(d.Y - dn.Y) > 4) moved = true;
        if (moved) Location = new Point(Math.Max(0, st.X + d.X - dn.X), Math.Max(0, st.Y + d.Y - dn.Y));
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (down && moved) Dropped?.Invoke(this); down = false; }
    protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); Act(); }
}

class Win : Panel
{
    public readonly string Title;
    public Action<Win>? Closed, Minned, Raised;
    readonly Panel bar = new(), grip = new();
    readonly Label tl = new();
    readonly Button cl, mx, mn;
    bool max, drag, rs, snapped; Point dn, st, rdn; Size rsz; Rectangle rest;

    Button Tb(string t, Color hover)
    {
        var b = new Button { Text = t, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = bar.BackColor, Size = new Size(46, 34), Font = new Font("Segoe UI", 10) };
        b.FlatAppearance.BorderSize = 0; b.MouseEnter += (_, _) => b.BackColor = hover; b.MouseLeave += (_, _) => b.BackColor = bar.BackColor; return b;
    }
    public Win(string title, string glyph, Control content, Size sz)
    {
        Title = title; Size = sz; Padding = new Padding(1); BackColor = Theme.Accent;
        bar.Dock = DockStyle.Top; bar.Height = 34; bar.BackColor = Color.FromArgb(28, 28, 44);
        tl.Text = "  " + glyph + "  " + title; tl.ForeColor = Color.White; tl.Font = new Font("Segoe UI Emoji", 10); tl.TextAlign = ContentAlignment.MiddleLeft;
        cl = Tb("✕", Color.FromArgb(200, 50, 50)); mx = Tb("▢", Color.FromArgb(70, 70, 100)); mn = Tb("—", Color.FromArgb(70, 70, 100));
        bar.Controls.Add(tl); bar.Controls.Add(cl); bar.Controls.Add(mx); bar.Controls.Add(mn);
        bar.Resize += (_, _) => { tl.SetBounds(0, 0, bar.Width - 138, 34); cl.Left = bar.Width - 46; mx.Left = bar.Width - 92; mn.Left = bar.Width - 138; };
        content.Dock = DockStyle.Fill; Controls.Add(content); Controls.Add(bar);
        grip.Size = new Size(18, 18); grip.Cursor = Cursors.SizeNWSE; grip.BackColor = Color.Transparent; Controls.Add(grip); grip.BringToFront();
        Resize += (_, _) => grip.Location = new Point(Width - 20, Height - 20);
        cl.Click += (_, _) => Closed?.Invoke(this); mn.Click += (_, _) => Minned?.Invoke(this); mx.Click += (_, _) => Toggle();
        foreach (Control c in new Control[] { bar, tl })
        {
            c.MouseDown += (_, e) => { Raise(); if (max || e.Button != MouseButtons.Left) return; if (snapped) { Size = rest.Size; snapped = false; } drag = true; dn = Cursor.Position; st = Location; };
            c.MouseMove += (_, _) => { if (drag) { var d = Cursor.Position; Location = new Point(st.X + d.X - dn.X, Math.Max(0, st.Y + d.Y - dn.Y)); } };
            c.MouseUp += (_, _) => { if (!drag) return; drag = false; if (Parent == null) return; var pt = Parent.PointToClient(Cursor.Position); if (pt.Y <= 4) Toggle(); else if (pt.X <= 4) SnapTo(0); else if (pt.X >= Parent.Width - 5) SnapTo(1); }; c.DoubleClick += (_, _) => Toggle();
        }
        grip.MouseDown += (_, _) => { rs = true; rdn = Cursor.Position; rsz = Size; };
        grip.MouseMove += (_, _) => { if (rs) { var d = Cursor.Position; Size = new Size(Math.Max(320, rsz.Width + d.X - rdn.X), Math.Max(220, rsz.Height + d.Y - rdn.Y)); } };
        grip.MouseUp += (_, _) => rs = false;
        Hook(content); MouseDown += (_, _) => Raise();
    }
    void Hook(Control c) { c.MouseDown += (_, _) => Raise(); foreach (Control k in c.Controls) Hook(k); }
    public void Raise() { BringToFront(); Raised?.Invoke(this); }
    void SnapTo(int side) { if (!snapped && !max) rest = Bounds; snapped = true; max = false; Bounds = new Rectangle(side == 0 ? 0 : Parent!.Width / 2, 0, Parent!.Width / 2, Parent.Height); grip.BringToFront(); }
    public void Toggle()
    {
        if (Parent == null) return;
        if (!max) { if (!snapped) rest = Bounds; snapped = false; Bounds = new Rectangle(0, 0, Parent.Width, Parent.Height); max = true; } else { Bounds = rest; max = false; }
        grip.BringToFront();
    }
}

class DesktopForm : Form
{
    public readonly Surface desk = new();
    readonly Panel bar = new(), menu = new(), toast = new();
    readonly Label clock = new(), tray = new(), toastLb = new(), mName = new();
    readonly Button startBtn = new(), shotBtn = new(), showBtn = new();
    readonly List<(Win w, Button b)> wins = new();
    readonly FlowLayoutPanel grid = new();
    readonly PictureBox mAv = new();
    readonly System.Windows.Forms.Timer tm = new() { Interval = 1000 }, tt = new() { Interval = 3200 };
    public readonly List<string> Notes = new();
    QuickPanel quick = null!; ClockWidget? wclock; Panel? wnote; DateTime lastInput = DateTime.Now; bool locking;

    static readonly (string N, string G, Color C, string K)[] Catalog =
    {
        ("Files", "📁", Color.FromArgb(239,159,39), "files"), ("Full OS", "🖥", Color.FromArgb(83,74,183), "vm"), ("Notepad", "📝", Color.FromArgb(55,138,221), "notepad"),
        ("Calculator", "🧮", Color.FromArgb(29,158,117), "calc"), ("Paint", "🎨", Color.FromArgb(212,83,126), "paint"),
        ("Terminal", "⌨", Color.FromArgb(68,68,80), "term"), ("Settings", "⚙", Color.FromArgb(95,94,110), "settings"),
        ("Browser", "🌐", Color.FromArgb(24,95,165), "web"), ("Screenshot", "📷", Color.FromArgb(127,119,221), "shot"),
        ("Recycle Bin", "🗑", Color.FromArgb(120,125,135), "bin"), ("MLD Store", "🛍", Color.FromArgb(212,83,126), "store"), ("Task Manager", "📊", Color.FromArgb(29,158,117), "tm"), ("Vault", "🔐", Color.FromArgb(60,60,110), "vault"),
    };
    static readonly string[] OnDesk = { "files", "vm", "store", "vault", "bin", "web", "settings", "notepad", "calc", "paint", "term" };

    public DesktopForm()
    {
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; Text = "USB Desktop"; KeyPreview = true; BackColor = Color.Black;
        bar.Dock = DockStyle.Bottom; bar.Height = 50; bar.BackColor = Color.FromArgb(22, 22, 34);
        startBtn.Text = "⊞"; startBtn.Font = new Font("Segoe UI", 16); shotBtn.Text = "📷"; shotBtn.Font = new Font("Segoe UI Emoji", 13);
        foreach (var b in new[] { startBtn, shotBtn, showBtn }) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.ForeColor = Color.White; b.BackColor = Color.FromArgb(40, 40, 58); b.Cursor = Cursors.Hand; }
        startBtn.Click += (_, _) => { if (menu.Visible) HideMenu(); else ShowMenu(); };
        shotBtn.Click += (_, _) => Shot(); showBtn.Click += (_, _) => { foreach (var x in wins) x.w.Visible = false; Sync(); };
        tray.ForeColor = Color.White; tray.Font = new Font("Segoe UI Emoji", 10); tray.TextAlign = ContentAlignment.MiddleCenter;
        clock.ForeColor = Color.White; clock.Font = new Font("Segoe UI", 9.5f); clock.TextAlign = ContentAlignment.MiddleCenter;
        bar.Controls.AddRange(new Control[] { startBtn, shotBtn, showBtn, tray, clock }); bar.Resize += (_, _) => LayoutBar();
        desk.Dock = DockStyle.Fill; Controls.Add(desk); Controls.Add(bar);

        toast.Size = new Size(320, 64); toast.BackColor = Color.FromArgb(32, 32, 48); toast.Visible = false; toast.Padding = new Padding(12);
        toastLb.Dock = DockStyle.Fill; toastLb.ForeColor = Color.White; toastLb.Font = new Font("Segoe UI", 10); toastLb.TextAlign = ContentAlignment.MiddleLeft; toast.Controls.Add(toastLb); Controls.Add(toast);
        tt.Tick += (_, _) => { tt.Stop(); toast.Visible = false; };

        BuildMenu(); BuildDeskMenu();
        quick = new QuickPanel(this); Controls.Add(quick); tray.Click += (_, _) => ToggleQuick(); clock.Click += (_, _) => ToggleQuick(); tray.Cursor = Cursors.Hand; clock.Cursor = Cursors.Hand;
        Application.AddMessageFilter(new IdleFilter(() => lastInput = DateTime.Now));
        desk.MouseDown += (_, _) => { HideMenu(); foreach (var i in desk.Controls.OfType<DeskIcon>()) { i.Sel = false; i.Invalidate(); } };
        desk.SizeChanged += (_, _) => { SetWall(); if (wclock != null) wclock.Left = Math.Max(0, desk.Width - 290); if (wnote != null) wnote.Left = Math.Max(0, desk.Width - 250); };
        KeyUp += (_, e) => { if (e.KeyCode == Keys.PrintScreen) Shot(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) HideMenu(); };
        tm.Tick += (_, _) => Tick(); tm.Start();
        Load += (_, _) => { ApplyLook(); ApplyWidgets(); LayoutBar(); Tick(); };
        Shown += async (_, _) =>
        {
            var sp = new Splash(ClientSize); Controls.Add(sp); sp.BringToFront(); Toast("Welcome, " + Cfg.Get("name", Environment.UserName) + " 👋");
            try { var u = await Updater.Check(); if (u.latest > Updater.Cur) Toast("⬆ Update " + u.latest + " is available. Open Settings > About."); } catch { }
        };
    }

    void Tick()
    {
        bool h24 = Cfg.Get("h24", "1") == "1";
        wclock?.Invalidate(); var im = Cfg.Int("idle", 10);
        if (im > 0 && !locking && DateTime.Now - lastInput > TimeSpan.FromMinutes(im)) Lock();
        if (Cfg.Get("usbwatch", "1") == "1" && !Directory.Exists(Paths.Root)) Application.Exit();
        clock.Text = DateTime.Now.ToString(h24 ? "HH:mm" : "h:mm tt") + "\n" + DateTime.Now.ToString("dd/MM/yyyy");
        var ps = SystemInformation.PowerStatus;
        tray.Text = "📶  🔊" + (ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery && ps.BatteryLifePercent <= 1 ? "  🔋" + (int)(ps.BatteryLifePercent * 100) + "%" : "");
    }

    void LayoutBar()
    {
        startBtn.SetBounds(6, 5, 46, 40); int x = 60;
        foreach (var (w, b) in wins) { b.SetBounds(x, 5, 150, 40); x += 154; }
        int r = bar.Width; showBtn.SetBounds(r - 8, 0, 8, 50); clock.SetBounds(r - 100, 0, 90, 50); tray.SetBounds(r - 232, 0, 130, 50); shotBtn.SetBounds(r - 282, 5, 44, 40);
    }

    void SetWall()
    {
        if (desk.Width < 10 || desk.Height < 10) return;
        var old = desk.Bg; desk.Bg = Theme.Wall(desk.Size); old?.Dispose(); desk.Invalidate(true);
    }
    public void ApplyLook() { SetWall(); Reload(); startBtn.BackColor = Theme.Accent; mAv.Image = U.Avatar(40); mName.Text = Cfg.Get("name", Environment.UserName); }

    public void Toast(string m)
    {
        toastLb.Text = m; Notes.Add(DateTime.Now.ToString("HH:mm") + "  " + m); if (Notes.Count > 30) Notes.RemoveAt(0); toast.Location = new Point(Width - 340, 16); toast.Visible = true; toast.BringToFront(); tt.Stop(); tt.Start();
    }

    public void Shot()
    {
        HideMenu(); toast.Visible = false; Application.DoEvents(); Thread.Sleep(150);
        var r = Screen.PrimaryScreen!.Bounds;
        using var bm = new Bitmap(r.Width, r.Height);
        using (var g = Graphics.FromImage(bm)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        var f = Path.Combine(Paths.Shots, "Screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        bm.Save(f, ImageFormat.Png); Reload(); Toast("📷 Screenshot saved in Desktop\\Screenshots");
    }

    // ---------- desktop icons ----------
    DeskIcon MakeIcon(string label, Image img, Action act, string? path)
    {
        var ic = new DeskIcon(label, img, act, Cfg.Int("icon", 52)) { FilePath = path };
        ic.Picked = i => { foreach (var o in desk.Controls.OfType<DeskIcon>()) { o.Sel = o == i; o.Invalidate(); } };
        ic.Dropped = i => Cfg.Set("pos_" + i.Label, i.Left + "," + i.Top);
        var m = new ContextMenuStrip(); m.Items.Add("Open", null, (_, _) => act());
        if (path != null)
        {
            m.Items.Add("Rename", null, (_, _) => Rename(ic));
            m.Items.Add("Delete", null, (_, _) => { try { Bin.Trash(path); Reload(); Toast("Moved to Recycle Bin"); } catch (Exception ex) { Toast(ex.Message); } });
        }
        ic.ContextMenuStrip = m; return ic;
    }

    public void Reload()
    {
        foreach (var c in desk.Controls.OfType<DeskIcon>().ToList()) { desk.Controls.Remove(c); c.Dispose(); }
        if (Cfg.Get("icons", "1") == "0") return;
        int sz = Cfg.Int("icon", 52), cw = sz + 50, ch = sz + 52;
        var list = new List<DeskIcon>();
        foreach (var k in OnDesk) { var a = Catalog.First(x => x.K == k); var key = k; list.Add(MakeIcon(k == "files" ? "This PC" : a.N, U.Tile(a.G, a.C), () => Launch(key), null)); }
        foreach (var e in Directory.GetFileSystemEntries(Paths.Desktop).OrderByDescending(Directory.Exists).ThenBy(x => x)) { var p = e; list.Add(MakeIcon(Path.GetFileName(p), U.IconFor(p), () => OpenFile(p), p)); }
        int rows = Math.Max(1, (desk.Height - 10) / ch), n = 0;
        foreach (var ic in list)
        {
            var sp = Cfg.Get("pos_" + ic.Label); var pt = new Point(8 + n / rows * cw, 8 + n % rows * ch); n++;
            var q = sp.Split(','); if (q.Length == 2 && int.TryParse(q[0], out var px) && int.TryParse(q[1], out var py)) pt = new Point(Math.Min(px, Math.Max(0, desk.Width - cw)), Math.Min(py, Math.Max(0, desk.Height - ch)));
            ic.Bounds = new Rectangle(pt, new Size(cw, ch)); desk.Controls.Add(ic); ic.SendToBack();
        }
    }

    void Rename(DeskIcon ic)
    {
        if (ic.FilePath == null) return; bool done = false;
        var tb = new TextBox { Text = ic.Label, Bounds = new Rectangle(ic.Left - 6, ic.Top + ic.Height - 26, ic.Width + 12, 24) };
        desk.Controls.Add(tb); tb.BringToFront(); tb.Focus(); tb.SelectAll();
        void Fin(bool ok)
        {
            if (done) return; done = true; var nn = tb.Text.Trim(); desk.Controls.Remove(tb); tb.Dispose();
            if (ok && nn != "" && nn != ic.Label) { try { var d = Path.Combine(Paths.Desktop, nn); if (Directory.Exists(ic.FilePath)) Directory.Move(ic.FilePath, d); else File.Move(ic.FilePath, d); } catch { Toast("Couldn't rename that"); } }
            Reload();
        }
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Fin(true); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) Fin(false); };
        tb.LostFocus += (_, _) => Fin(true);
    }

    void NewItem(bool dir)
    {
        var p = U.Unique(Paths.Desktop, dir ? "New folder" : "New document.txt");
        if (dir) Directory.CreateDirectory(p); else File.WriteAllText(p, "");
        Reload(); var ic = desk.Controls.OfType<DeskIcon>().FirstOrDefault(x => x.FilePath == p); if (ic != null) Rename(ic);
    }

    void BuildDeskMenu()
    {
        var cm = new ContextMenuStrip();
        var view = new ToolStripMenuItem("View");
        foreach (var (n, v) in new[] { ("Small icons", 40), ("Medium icons", 52), ("Large icons", 68) }) { var vv = v; view.DropDownItems.Add(n, null, (_, _) => { Cfg.Set("icon", vv.ToString()); Reload(); }); }
        var nw = new ToolStripMenuItem("New"); nw.DropDownItems.Add("📁  Folder", null, (_, _) => NewItem(true)); nw.DropDownItems.Add("📝  Text document", null, (_, _) => NewItem(false));
        cm.Items.Add(view); cm.Items.Add(nw); cm.Items.Add("Refresh", null, (_, _) => Reload());
        cm.Items.Add(new ToolStripSeparator());
        cm.Items.Add("📷  Take screenshot", null, (_, _) => Shot());
        cm.Items.Add("🖥  Display settings", null, (_, _) => Launch("settings", "1"));
        cm.Items.Add("🎨  Personalize", null, (_, _) => Launch("settings", "0"));
        desk.ContextMenuStrip = cm;
    }

    // ---------- windows ----------
    Win? FrontWin() => desk.Controls.OfType<Win>().FirstOrDefault(x => x.Visible);
    void Sync() { var t = FrontWin(); foreach (var (w, b) in wins) b.BackColor = w.Visible && w == t ? Theme.Accent : Color.FromArgb(40, 40, 58); }

    public Win AddWin(string title, string glyph, Control view, Size sz)
    {
        var w = new Win(title, glyph, view, sz); int k = wins.Count % 6 * 28; w.Location = new Point(120 + k, 30 + k);
        desk.Controls.Add(w); w.BringToFront();
        var b = new Button { Text = glyph + "  " + title, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Emoji", 9.5f) };
        b.FlatAppearance.BorderSize = 0; wins.Add((w, b)); bar.Controls.Add(b);
        b.Click += (_, _) => { var t = FrontWin(); if (!w.Visible) { w.Visible = true; w.BringToFront(); } else if (t == w) w.Visible = false; else w.BringToFront(); Sync(); };
        w.Closed = _ => BeginInvoke(new Action(() => { bar.Controls.Remove(b); b.Dispose(); wins.RemoveAll(x => x.w == w); desk.Controls.Remove(w); w.Dispose(); LayoutBar(); Sync(); }));
        w.Minned = _ => { w.Visible = false; Sync(); };
        w.Raised = _ => { HideMenu(); Sync(); };
        LayoutBar(); Sync(); return w;
    }

    public void OpenFile(string p)
    {
        if (Directory.Exists(p)) { Launch("files", p); return; }
        var ext = Path.GetExtension(p).ToLower();
        if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(ext)) { Launch("photo", p); return; }
        if (new[] { ".txt", ".md", ".json", ".js", ".cs", ".log", ".ini", ".html", ".css", ".py", ".bat" }.Contains(ext)) Launch("notepad", p); else U.Open(p);
    }

    public void Launch(string k, string? arg = null)
    {
        HideMenu();
        switch (k)
        {
            case "files": AddWin("Files", "📁", Views.Files(this, arg ?? Paths.Root), new Size(780, 500)); break;
            case "notepad": AddWin(arg == null ? "Notepad" : Path.GetFileName(arg), "📝", Views.Notepad(this, arg), new Size(660, 440)); break;
            case "calc": AddWin("Calculator", "🧮", Views.Calc(), new Size(320, 470)); break;
            case "paint": AddWin("Paint", "🎨", Views.PaintApp(this), new Size(840, 560)); break;
            case "term": AddWin("Terminal", "⌨", Views.Term(), new Size(700, 420)); break;
            case "web": AddWin("Browser", "🌐", Web.Browser(this, arg), new Size(960, 620)); break;
            case "store": AddWin("MLD Store", "🛍", Web.Store(this), new Size(980, 640)); break;
            case "tm": AddWin("Task Manager", "📊", Tools.TaskMgr(this), new Size(620, 460)); break;
            case "photo": if (arg != null) AddWin(Path.GetFileName(arg), "🖼", Tools.Photo(arg), new Size(760, 540)); break;
            case "shot": Shot(); break;
            case "bin":
            case "vault":
            case "vm":
            case "settings":
                var title = k == "bin" ? "Recycle Bin" : k == "vm" ? "Full OS" : k == "vault" ? "Private Vault" : "Settings"; var ex = wins.FirstOrDefault(x => x.w.Title == title).w;
                if (ex != null) { ex.Visible = true; ex.BringToFront(); Sync(); }
                else if (k == "bin") AddWin(title, "🗑", Views.BinView(this), new Size(620, 420));
                else if (k == "vault") AddWin(title, "🔐", VaultApp.View(this), new Size(680, 460));
                else if (k == "vm") AddWin(title, "🖥", VmApp.View(this), new Size(720, 540));
                else AddWin(title, "⚙", Views.Settings(this, int.TryParse(arg, out var pg) ? pg : 0), new Size(900, 600));
                break;
        }
    }

    // ---------- start menu ----------
    Button AppBtn(string name, Image img, Action act)
    {
        var b = new Button { Text = name, Image = new Bitmap(img, 48, 48), TextImageRelation = TextImageRelation.ImageAboveText, Size = new Size(86, 90), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 80); b.Click += (_, _) => act(); return b;
    }
    void BuildMenu()
    {
        menu.Size = new Size(404, 540); menu.BackColor = Color.FromArgb(32, 32, 48); menu.Visible = false;
        var s = new TextBox { Bounds = new Rectangle(18, 18, 368, 30), Font = new Font("Segoe UI", 11), PlaceholderText = "Search apps" };
        var pl = new Label { Text = "Pinned", ForeColor = Color.FromArgb(200, 200, 220), Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(18, 60), AutoSize = true };
        grid.Bounds = new Rectangle(14, 88, 376, 366); grid.BackColor = menu.BackColor; grid.AutoScroll = true;
        foreach (var a in Catalog) { var key = a.K; grid.Controls.Add(AppBtn(a.N, U.Tile(a.G, a.C), () => Launch(key))); }
        AddUserApps();
        s.TextChanged += (_, _) => { foreach (Button b in grid.Controls) b.Visible = b.Text.Contains(s.Text, StringComparison.OrdinalIgnoreCase); };
        mAv.Bounds = new Rectangle(18, 478, 40, 40); mAv.SizeMode = PictureBoxSizeMode.Zoom;
        mName.Bounds = new Rectangle(66, 478, 220, 40); mName.ForeColor = Color.White; mName.Font = new Font("Segoe UI", 11); mName.TextAlign = ContentAlignment.MiddleLeft;
        var pw = new Button { Text = "⏻", Bounds = new Rectangle(340, 478, 46, 40), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(55, 55, 80), Font = new Font("Segoe UI", 13) };
        pw.FlatAppearance.BorderSize = 0;
        var pm = new ContextMenuStrip();
        pm.Items.Add("🔒  Lock", null, (_, _) => { HideMenu(); Lock(); });
        pm.Items.Add("⏻  Shut down", null, (_, _) => Application.Exit());
        pw.Click += (_, _) => pm.Show(pw, new Point(0, 0), ToolStripDropDownDirection.AboveLeft);
        menu.Controls.AddRange(new Control[] { s, pl, grid, mAv, mName, pw }); Controls.Add(menu);
    }
    void ShowMenu() { menu.Location = new Point(8, Height - bar.Height - menu.Height - 8); mAv.Image = U.Avatar(40); mName.Text = Cfg.Get("name", Environment.UserName); menu.Visible = true; menu.BringToFront(); }
    void HideMenu() { menu.Visible = false; quick.Visible = false; }
    void ToggleQuick() { if (quick.Visible) { quick.Visible = false; return; } menu.Visible = false; quick.Refresh2(); quick.Location = new Point(Width - quick.Width - 8, Height - bar.Height - quick.Height - 8); quick.Visible = true; quick.BringToFront(); }
    void Lock() { if (locking) return; locking = true; try { using var l = new LoginForm(); l.ShowDialog(); } finally { locking = false; lastInput = DateTime.Now; } }
    public void ApplyWidgets()
    {
        if (wclock != null) { desk.Controls.Remove(wclock); wclock.Dispose(); wclock = null; }
        if (wnote != null) { desk.Controls.Remove(wnote); wnote.Dispose(); wnote = null; }
        if (Cfg.Get("wclock", "1") == "1") { wclock = new ClockWidget { Location = new Point(Math.Max(0, desk.Width - 290), 16) }; desk.Controls.Add(wclock); wclock.SendToBack(); }
        if (Cfg.Get("wnote", "1") == "1") { wnote = Tools.Sticky(); wnote.Location = new Point(Math.Max(0, desk.Width - 250), 140); desk.Controls.Add(wnote); wnote.SendToBack(); }
    }
    void AddUserApps()
    {
        try
        {
            foreach (var f in Directory.GetFiles(Paths.Apps).Where(x => x.EndsWith(".exe") || x.EndsWith(".lnk") || x.EndsWith(".bat"))) { var p = f; grid.Controls.Add(AppBtn(Path.GetFileNameWithoutExtension(p), U.IconFor(p), () => { HideMenu(); U.Open(p); })); }
            foreach (var d in Directory.GetDirectories(Paths.Apps)) { var exe = Directory.GetFiles(d, "*.exe").FirstOrDefault(); if (exe != null) { var p = exe; grid.Controls.Add(AppBtn(Path.GetFileName(d), U.IconFor(p), () => { HideMenu(); U.Open(p); })); } }
        }
        catch { }
    }
    public void ReloadApps() { while (grid.Controls.Count > Catalog.Length) { var c = grid.Controls[grid.Controls.Count - 1]; grid.Controls.Remove(c); c.Dispose(); } AddUserApps(); }
    public void AfterDownload(string f)
    {
        try
        {
            if (f.StartsWith(Paths.Apps) && f.EndsWith(".zip")) { System.IO.Compression.ZipFile.ExtractToDirectory(f, U.Unique(Paths.Apps, Path.GetFileNameWithoutExtension(f))); File.Delete(f); }
            ReloadApps(); Reload(); Toast("✅ Downloaded " + Path.GetFileName(f));
        }
        catch (Exception ex) { Toast(ex.Message); }
    }
}
