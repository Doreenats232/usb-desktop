using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace UsbDesktop;

class Surface : Panel
{
    public Bitmap? Bg;
    public Surface() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; if (Bg != null) g.DrawImage(Bg, 0, 0); else g.Clear(Ui.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var w in Controls.OfType<Win>().Where(x => x.Visible && !x.IsMax))
            for (int i = 8; i >= 1; i--)
            {
                var r = Rectangle.Inflate(w.Bounds, i * Ui.D(2), i * Ui.D(2)); r.Offset(0, Ui.D(6));
                using var p = U.Round(r, Ui.D(10) + i * 2); using var b = new SolidBrush(Color.FromArgb(9, 0, 0, 0)); g.FillPath(b, p);
            }
    }
}

class DeskIcon : Control
{
    public string Label; public Image Img; public string? FilePath; public Action Act; public bool Sel; public int Sz;
    public Action<DeskIcon>? Picked, Dropped;
    Point dn, st; bool down, moved, hover;
    public DeskIcon(string label, Image img, Action act, int sz)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent; Label = label; Img = img; Act = act; Sz = sz; Font = Ui.F(9f);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (Sel || hover) { using var b = new SolidBrush(Color.FromArgb(Sel ? 80 : 38, 255, 255, 255)); using var p = U.Round(new Rectangle(1, 1, Width - 3, Height - 3), Ui.D(10)); g.FillPath(b, p); if (Sel) { using var pn = new Pen(Color.FromArgb(90, 255, 255, 255)); g.DrawPath(pn, p); } }
        g.DrawImage(Img, new Rectangle((Width - Sz) / 2, Ui.D(8), Sz, Sz));
        var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top | TextFormatFlags.NoPrefix;
        var tr = new Rectangle(Ui.D(3), Sz + Ui.D(13), Width - Ui.D(6), Height - Sz - Ui.D(14));
        TextRenderer.DrawText(g, Label, Font, new Rectangle(tr.X + 1, tr.Y + 1, tr.Width, tr.Height), Color.FromArgb(170, 0, 0, 0), fl);
        TextRenderer.DrawText(g, Label, Font, tr, Color.White, fl);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Picked?.Invoke(this); if (e.Button == MouseButtons.Left) { down = true; moved = false; dn = Cursor.Position; st = Location; } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!down) return; var d = Cursor.Position;
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
    readonly Label tl = new(); readonly PictureBox ico = new();
    readonly RoundButton cl, mx, mn;
    bool max, drag, rs, snapped; Point dn, st, rdn; Size rsz; Rectangle rest, last;
    public bool IsMax => max; public string Kind = ""; public string? Arg; public Control Content = null!;
    public Rectangle RestBounds => (max || snapped) ? rest : Bounds;

    RoundButton Tb(string glyph, Color hover) => new() { Glyph = glyph, BackColor = bar.BackColor, Hover = hover, Radius = 8, GlyphScale = 0.34f };
    public Win(string title, string icon, Control content, Size sz)
    {
        Title = title; Content = content; Size = sz; Padding = new Padding(1); BackColor = Ui.Line; DoubleBuffered = true;
        bar.Dock = DockStyle.Top; bar.Height = Ui.D(40); bar.BackColor = Color.FromArgb(20, 21, 38);
        bar.Paint += (_, e) => { using var lg = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, bar.Width), Ui.D(3)), Ui.Accent, Ui.Accent2, 0f); e.Graphics.FillRectangle(lg, 0, 0, bar.Width, Ui.D(3)); };
        ico.Image = Icons.Scaled(Icons.App(icon), Ui.D(24)); ico.SizeMode = PictureBoxSizeMode.Zoom; ico.BackColor = Color.Transparent;
        tl.Text = title; tl.ForeColor = Ui.Text; tl.Font = Ui.F(10); tl.TextAlign = ContentAlignment.MiddleLeft; tl.BackColor = Color.Transparent; tl.AutoEllipsis = true; tl.UseMnemonic = false;
        cl = Tb("close", Color.FromArgb(220, 60, 60)); mx = Tb("max", Color.FromArgb(56, 60, 96)); mn = Tb("min", Color.FromArgb(56, 60, 96));
        bar.Controls.Add(ico); bar.Controls.Add(tl); bar.Controls.Add(cl); bar.Controls.Add(mx); bar.Controls.Add(mn);
        bar.Resize += (_, _) =>
        {
            int bw = Ui.D(46), bh = Math.Max(4, bar.Height - Ui.D(10));
            ico.SetBounds(Ui.D(12), (bar.Height - Ui.D(24)) / 2 + Ui.D(1), Ui.D(24), Ui.D(24));
            tl.SetBounds(Ui.D(44), Ui.D(3), Math.Max(10, bar.Width - bw * 3 - Ui.D(48)), bar.Height - Ui.D(3));
            cl.SetBounds(bar.Width - bw, Ui.D(6), bw - Ui.D(4), bh); mx.SetBounds(bar.Width - bw * 2, Ui.D(6), bw - Ui.D(4), bh); mn.SetBounds(bar.Width - bw * 3, Ui.D(6), bw - Ui.D(4), bh);
        };
        content.Dock = DockStyle.Fill; Controls.Add(content); Controls.Add(bar);
        grip.Size = Ui.D(20, 20); grip.Cursor = Cursors.SizeNWSE; grip.BackColor = Color.Transparent; Controls.Add(grip); grip.BringToFront();
        Resize += (_, _) => grip.Location = new Point(Width - Ui.D(22), Height - Ui.D(22));
        cl.Click += (_, _) => Closed?.Invoke(this); mn.Click += (_, _) => Minned?.Invoke(this); mx.Click += (_, _) => Toggle();
        foreach (Control c in new Control[] { bar, tl, ico })
        {
            c.MouseDown += (_, e) => { Raise(); if (max || e.Button != MouseButtons.Left) return; if (snapped) { Size = rest.Size; snapped = false; } drag = true; dn = Cursor.Position; st = Location; };
            c.MouseMove += (_, _) => { if (drag) { var d = Cursor.Position; Location = new Point(st.X + d.X - dn.X, Math.Max(0, st.Y + d.Y - dn.Y)); } };
            c.MouseUp += (_, _) =>
            {
                if (!drag) return; drag = false; if (Parent == null) return; var pt = Parent.PointToClient(Cursor.Position);
                if (pt.Y <= 4) Toggle(); else if (pt.X <= 4) SnapTo(0); else if (pt.X >= Parent.Width - 5) SnapTo(1);
            };
            c.DoubleClick += (_, _) => Toggle();
        }
        grip.MouseDown += (_, _) => { rs = true; rdn = Cursor.Position; rsz = Size; };
        grip.MouseMove += (_, _) => { if (rs) { var d = Cursor.Position; Size = new Size(Math.Max(Ui.D(340), rsz.Width + d.X - rdn.X), Math.Max(Ui.D(240), rsz.Height + d.Y - rdn.Y)); } };
        grip.MouseUp += (_, _) => rs = false;
        Hook(content); MouseDown += (_, _) => Raise();
    }
    void Hook(Control c) { c.MouseDown += (_, _) => Raise(); foreach (Control k in c.Controls) Hook(k); }
    public void Raise() { BringToFront(); Raised?.Invoke(this); }
    public void Pop()
    {
        var end = Location; int n = 0; Location = new Point(end.X, end.Y + Ui.D(22));
        var t = new System.Windows.Forms.Timer { Interval = 15 };
        t.Tick += (_, _) => { n++; double k = 1 - Math.Pow(1 - n / 9.0, 3); Location = new Point(end.X, end.Y + (int)(Ui.D(22) * (1 - k))); if (n >= 9) { t.Stop(); t.Dispose(); Location = end; } };
        t.Start();
    }
    void SnapTo(int side) { if (!snapped && !max) rest = Bounds; snapped = true; max = false; mx.Glyph = "max"; Bounds = new Rectangle(side == 0 ? 0 : Parent!.Width / 2, 0, Parent!.Width / 2, Parent.Height); grip.BringToFront(); }
    public void Toggle()
    {
        if (Parent == null) return;
        if (!max) { if (!snapped) rest = Bounds; snapped = false; Bounds = new Rectangle(0, 0, Parent.Width, Parent.Height); max = true; mx.Glyph = "restore"; } else { Bounds = rest; max = false; mx.Glyph = "max"; }
        mx.Invalidate(); grip.BringToFront(); Corners();
    }
    void Corners()
    {
        if (Width < 20 || Height < 20) return; var old = Region;
        if (max || snapped) Region = null; else { using var p = U.Round(new Rectangle(0, 0, Width, Height), Ui.D(10)); Region = new Region(p); }
        old?.Dispose();
    }
    void Shadow() { if (Parent == null) return; if (last.IsEmpty) last = Bounds; Parent.Invalidate(Rectangle.Inflate(Rectangle.Union(last, Bounds), Ui.D(26), Ui.D(32))); last = Bounds; }
    protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); Shadow(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Shadow(); Corners(); }
}

class DesktopForm : Form
{
    public readonly Surface desk = new();
    readonly Panel bar = new();
    readonly RoundPanel menu = new() { Floating = true }, toast = new() { Floating = true };
    readonly Label clock = new(), toastLb = new(), mName = new(); readonly TrayStrip tray = new();
    readonly RoundButton startBtn = new(), shotBtn = new(), showBtn = new(), searchBtn = new();
    readonly List<(Win w, RoundButton b)> wins = new();
    readonly FlowLayoutPanel grid = new();
    readonly PictureBox mAv = new();
    readonly System.Windows.Forms.Timer tm = new() { Interval = 1000 }, tt = new() { Interval = 3200 };
    public readonly List<string> Notes = new();
    readonly List<DlCard> cards = new();
    RoundButton? pwBtn; QuickPanel quick = null!; TextBox msearch = null!; ClockWidget? wclock; Panel? wnote; DateTime lastInput = DateTime.Now; bool locking;

    static readonly (string N, string K)[] Catalog =
    {
        ("Files", "files"), ("Full OS", "vm"), ("Notepad", "notepad"), ("Calculator", "calc"), ("Paint", "paint"), ("Terminal", "term"), ("Settings", "settings"),
        ("Browser", "web"), ("Screenshot", "shot"), ("Recycle Bin", "bin"), ("MLD Store", "store"), ("Task Manager", "tm"), ("Vault", "vault"),
    };
    public static string IconKey(string k) => k switch { "files" => "folder", "web" => "browser", _ => k };
    static readonly string[] OnDesk = { "files", "web", "store", "vm", "vault", "bin", "settings", "notepad", "calc", "paint", "term" };

    public DesktopForm()
    {
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; Text = "USB Desktop"; KeyPreview = true; BackColor = Ui.Bg;
        bar.Dock = DockStyle.Bottom; bar.Height = Ui.D(56); bar.BackColor = Ui.Surface;
        bar.Paint += (_, e) => { using var p = new Pen(Ui.Line); e.Graphics.DrawLine(p, 0, 0, bar.Width, 0); };
        startBtn.Glyph = "start"; startBtn.GlyphScale = 0.42f; startBtn.Pill = true; startBtn.BackColor = Theme.Accent;
        searchBtn.Text = "Search"; searchBtn.Glyph = "search"; searchBtn.TextAlign = ContentAlignment.MiddleLeft; searchBtn.Pill = true; searchBtn.ForeColor = Ui.Sub;
        shotBtn.Glyph = "camera"; shotBtn.GlyphScale = 0.44f; shotBtn.Pill = true; showBtn.BackColor = Ui.Surface; showBtn.Hover = Ui.Line; showBtn.Radius = 2;
        startBtn.Click += (_, _) => { if (menu.Visible) HideMenu(); else ShowMenu(); };
        searchBtn.Click += (_, _) => { ShowMenu(); msearch.Focus(); };
        shotBtn.Click += (_, _) => Shot(); showBtn.Click += (_, _) => { foreach (var x in wins) x.w.Visible = false; Sync(); };
        tray.BackColor = Ui.Surface;
        clock.ForeColor = Ui.Text; clock.Font = Ui.F(9.5f); clock.TextAlign = ContentAlignment.MiddleCenter; clock.BackColor = Color.Transparent;
        bar.Controls.AddRange(new Control[] { startBtn, searchBtn, shotBtn, showBtn, tray, clock }); bar.Resize += (_, _) => LayoutBar();
        var bm = new ContextMenuStrip(); bm.Items.Add("Task Manager", null, (_, _) => Launch("tm")); bm.Items.Add("Settings", null, (_, _) => Launch("settings")); bar.ContextMenuStrip = bm;
        desk.Dock = DockStyle.Fill; Controls.Add(desk); Controls.Add(bar);

        toast.Size = Ui.D(360, 70); toast.Fill = Ui.Surface2; toast.Radius = Ui.D(14); toast.Visible = false;
        toastLb.ForeColor = Ui.Text; toastLb.Font = Ui.F(10); toastLb.TextAlign = ContentAlignment.MiddleLeft; toastLb.BackColor = Color.Transparent; toastLb.SetBounds(Ui.D(16), Ui.D(4), Ui.D(330), Ui.D(62));
        toast.Controls.Add(toastLb); Controls.Add(toast); tt.Tick += (_, _) => { tt.Stop(); toast.Visible = false; };

        BuildMenu(); BuildDeskMenu();
        quick = new QuickPanel(this); Controls.Add(quick);
        tray.Click += (_, _) => ToggleQuick(); clock.Click += (_, _) => ToggleQuick(); tray.Cursor = Cursors.Hand; clock.Cursor = Cursors.Hand;
        Application.AddMessageFilter(new IdleFilter(() => { lastInput = DateTime.Now; LastInputAt = lastInput; }));
        desk.MouseDown += (_, _) => { HideMenu(); foreach (var i in desk.Controls.OfType<DeskIcon>()) { i.Sel = false; i.Invalidate(); } };
        desk.SizeChanged += (_, _) => { SetWall(); if (wclock != null) wclock.Left = Math.Max(0, desk.Width - wclock.Width - Ui.D(20)); if (wnote != null) wnote.Left = Math.Max(0, desk.Width - wnote.Width - Ui.D(24)); };
        KeyUp += (_, e) => { if (e.KeyCode == Keys.PrintScreen) Shot(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) HideMenu(); };
        tm.Tick += (_, _) => Tick(); tm.Start();
        Load += (_, _) => { ApplyLook(); ApplyWidgets(); LayoutBar(); Tick(); };
        FormClosing += (_, _) => { if (Cfg.Get("sleep", "0") == "1") { if (VmApp.Running) Task.Run(VmApp.Freeze).GetAwaiter().GetResult(); SaveState(); } };
        Shown += async (_, _) =>
        {
            var sp = new Splash(ClientSize); Controls.Add(sp); sp.BringToFront(); Toast("Welcome, " + Cfg.Get("name", Environment.UserName) + " ");
            await Task.Delay(400); RestoreSession(); if (Cfg.Get("companion", "1") == "1") { try { Companion.Greet(); } catch { } }
            try { var u = await Updater.Check(); if (u.latest > Updater.Cur) { Toast("Update 1.0." + u.latest + " is available"); await Updater.Run(u.latest, u.url, true); } } catch { }
        };
    }

    void Tick()
    {
        bool h24 = Cfg.Get("h24", "1") == "1";
        wclock?.Invalidate(); var im = Cfg.Int("idle", 10);
        if (im > 0 && !locking && DateTime.Now - lastInput > TimeSpan.FromMinutes(im)) Lock();
        if (Cfg.Get("usbwatch", "1") == "1") UsbCheck();
        clock.Text = DateTime.Now.ToString(h24 ? "HH:mm" : "h:mm tt") + "\n" + DateTime.Now.ToString("dd/MM/yyyy");
        var ps = SystemInformation.PowerStatus;
        tray.Battery = ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery && ps.BatteryLifePercent <= 1 ? (int)(ps.BatteryLifePercent * 100) : -1;
    }

    public static DateTime LastInputAt = DateTime.Now; public static event Action? PanicLock;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RegisterHotKey(Handle, 77, 0x3, 0x4C); }
    protected override void OnHandleDestroyed(EventArgs e) { try { UnregisterHotKey(Handle, 77); } catch { } base.OnHandleDestroyed(e); }
    protected override void WndProc(ref Message m) { if (m.Msg == 0x0312 && m.WParam == (IntPtr)77) PanicNow(); else base.WndProc(ref m); }
    public void PanicNow()
    {
        if (locking) return; HideMenu(); PanicLock?.Invoke(); try { Clipboard.Clear(); } catch { }
        var vis = wins.Where(x => x.w.Visible).Select(x => x.w).ToList(); foreach (var w in vis) w.Visible = false; Sync();
        Lock(); foreach (var w in vis) w.Visible = true; Sync();
    }
    Panel? gone;
    void UsbCheck()
    {
        bool here = Directory.Exists(Paths.Root);
        if (!here && gone == null)
        {
            gone = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 15, 30) }; var l = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = Ui.F(18, true), Text = "USB removed\n\nPlug it back in to keep going.\nYour session is paused." };
            gone.Controls.Add(l); Controls.Add(gone); gone.BringToFront();
        }
        else if (here && gone != null) { Controls.Remove(gone); gone.Dispose(); gone = null; Toast("Welcome back. USB reconnected."); }
    }
    public async void EjectNow()
    {
        HideMenu(); Toast("Saving everything before eject…");
        try { await Web.FlushAll(); } catch { }
        if (VmApp.Running) await Task.Run(VmApp.Freeze);
        SaveState(); await Task.Delay(500);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("rundll32.exe", "shell32.dll,Control_RunDLL hotplug.dll") { UseShellExecute = true }); } catch { }
        Application.Exit();
    }
    void LayoutBar()
    {
        int h = bar.Height, pad = Ui.D(8), bh = h - 2 * pad;
        startBtn.SetBounds(Ui.D(10), pad, Ui.D(54), bh); searchBtn.SetBounds(Ui.D(72), pad, Ui.D(150), bh); int x = Ui.D(232);
        foreach (var (w, b) in wins) { b.SetBounds(x, pad, Ui.D(168), bh); x += Ui.D(174); }
        int r = bar.Width; showBtn.SetBounds(r - Ui.D(8), 0, Ui.D(8), h); clock.SetBounds(r - Ui.D(112), 0, Ui.D(100), h); tray.SetBounds(r - Ui.D(270), 0, Ui.D(150), h); shotBtn.SetBounds(r - Ui.D(326), pad, Ui.D(50), bh);
    }

    void SetWall()
    {
        if (desk.Width < 10 || desk.Height < 10) return;
        var old = desk.Bg; desk.Bg = Theme.Wall(desk.Size); old?.Dispose(); desk.Invalidate(true);
    }
    public void ApplyLook() { SetWall(); Reload(); startBtn.BackColor = Theme.Accent; mAv.Image = U.Avatar(Ui.D(44)); mName.Text = Cfg.Get("name", Environment.UserName); }

    public void Toast(string m)
    {
        toastLb.Text = m; Notes.Add(DateTime.Now.ToString("HH:mm") + "  " + m); if (Notes.Count > 30) Notes.RemoveAt(0);
        toast.Location = new Point(Width - toast.Width - Ui.D(16), Ui.D(16)); toast.Visible = true; toast.BringToFront(); tt.Stop(); tt.Start();
    }

    // ---------- downloads ----------
    public DlCard NewDl(string title, CancellationTokenSource? cts, string icon = "store")
    {
        var c = new DlCard(title, cts, icon); cards.Add(c); Controls.Add(c); c.Removed = () => { cards.Remove(c); LayoutCards(); }; LayoutCards(); return c;
    }
    void LayoutCards()
    {
        int y = Height - bar.Height - Ui.D(14);
        foreach (var c in cards) { y -= c.Height + Ui.D(10); c.Location = new Point(Width - c.Width - Ui.D(16), y); c.BringToFront(); }
    }

    public void Shot()
    {
        HideMenu(); toast.Visible = false; Application.DoEvents(); Thread.Sleep(150);
        var r = Screen.PrimaryScreen!.Bounds;
        using var bm = new Bitmap(r.Width, r.Height);
        using (var g = Graphics.FromImage(bm)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        var f = Path.Combine(Paths.Shots, "Screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        bm.Save(f, ImageFormat.Png); Reload(); Toast("Screenshot saved in Desktop\\Screenshots");
    }

    // ---------- desktop icons ----------
    DeskIcon MakeIcon(string label, Image img, Action act, string? path)
    {
        var ic = new DeskIcon(label, img, act, Ui.D(Cfg.Int("icon", 56))) { FilePath = path };
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
        int sz = Ui.D(Cfg.Int("icon", 56)), cw = sz + Ui.D(46), ch = sz + Ui.D(52);
        var list = new List<DeskIcon>();
        foreach (var k in OnDesk) { var a = Catalog.First(x => x.K == k); var key = k; list.Add(MakeIcon(k == "files" ? "This PC" : a.N, Icons.App(IconKey(k)), () => Launch(key), null)); }
        foreach (var e in Directory.GetFileSystemEntries(Paths.Desktop).OrderByDescending(Directory.Exists).ThenBy(x => x)) { var p = e; list.Add(MakeIcon(Path.GetFileName(p), U.IconFor(p), () => OpenFile(p), p)); }
        int rows = Math.Max(1, (desk.Height - Ui.D(10)) / ch), n = 0;
        foreach (var ic in list)
        {
            var sp = Cfg.Get("pos_" + ic.Label); var pt = new Point(Ui.D(8) + n / rows * cw, Ui.D(8) + n % rows * ch); n++;
            var q = sp.Split(','); if (q.Length == 2 && int.TryParse(q[0], out var px) && int.TryParse(q[1], out var py)) pt = new Point(Math.Min(px, Math.Max(0, desk.Width - cw)), Math.Min(py, Math.Max(0, desk.Height - ch)));
            ic.Bounds = new Rectangle(pt, new Size(cw, ch)); desk.Controls.Add(ic); ic.SendToBack();
        }
    }

    void Rename(DeskIcon ic)
    {
        if (ic.FilePath == null) return; bool done = false;
        var tb = new TextBox { Text = ic.Label, Font = Ui.F(9.5f), Bounds = new Rectangle(ic.Left - Ui.D(6), ic.Top + ic.Height - Ui.D(30), ic.Width + Ui.D(12), Ui.D(26)) };
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
        var cm = new ContextMenuStrip(); var view = new ToolStripMenuItem("View");
        foreach (var (n, v) in new[] { ("Small icons", 44), ("Medium icons", 56), ("Large icons", 72) }) { var vv = v; view.DropDownItems.Add(n, null, (_, _) => { Cfg.Set("icon", vv.ToString()); Reload(); }); }
        var nw = new ToolStripMenuItem("New"); nw.DropDownItems.Add("Folder", null, (_, _) => NewItem(true)); nw.DropDownItems.Add("Text document", null, (_, _) => NewItem(false));
        cm.Items.Add(view); cm.Items.Add(nw); cm.Items.Add("Refresh", null, (_, _) => Reload()); cm.Items.Add(new ToolStripSeparator());
        cm.Items.Add("Take screenshot", null, (_, _) => Shot()); cm.Items.Add("Display settings", null, (_, _) => Launch("settings", "1")); cm.Items.Add("Personalize", null, (_, _) => Launch("settings", "0"));
        desk.ContextMenuStrip = cm;
    }

    // ---------- windows ----------
    Win? FrontWin() => desk.Controls.OfType<Win>().FirstOrDefault(x => x.Visible);
    void Sync() { var t = FrontWin(); foreach (var (w, b) in wins) { bool act = w.Visible && w == t; b.BackColor = act ? Color.FromArgb(48, 52, 88) : Ui.Surface; b.Underline = w.Visible ? (act ? Theme.Accent : Color.FromArgb(120, 124, 170)) : null; b.Invalidate(); } }

    public Win AddWin(string title, string icon, Control view, Size sz)
    {
        Ui.Skin(view); Ui.Fit(view);
        sz = new Size(Math.Min(Ui.D(sz.Width), desk.Width - Ui.D(40)), Math.Min(Ui.D(sz.Height), desk.Height - Ui.D(40)));
        var w = new Win(title, icon, view, sz); int k = wins.Count % 6 * Ui.D(28); w.Location = new Point(Ui.D(110) + k, Ui.D(30) + k);
        desk.Controls.Add(w); w.BringToFront();
        var b = new RoundButton { Text = title, Ico = Icons.App(icon), TextAlign = ContentAlignment.MiddleLeft, Font = Ui.F(9.5f), BackColor = Ui.Surface };
        wins.Add((w, b)); bar.Controls.Add(b);
        b.Click += (_, _) => { var t = FrontWin(); if (!w.Visible) { w.Visible = true; w.BringToFront(); } else if (t == w) w.Visible = false; else w.BringToFront(); Sync(); desk.Invalidate(); };
        w.Closed = _ => BeginInvoke(new Action(() => { bar.Controls.Remove(b); b.Dispose(); wins.RemoveAll(x => x.w == w); desk.Controls.Remove(w); w.Dispose(); LayoutBar(); Sync(); desk.Invalidate(); }));
        w.Minned = _ => { w.Visible = false; Sync(); desk.Invalidate(); };
        w.Raised = _ => { HideMenu(); Sync(); };
        LayoutBar(); Sync(); w.Pop(); return w;
    }

    public void OpenFile(string p)
    {
        if (Directory.Exists(p)) { Launch("files", p); return; }
        var ext = Path.GetExtension(p).ToLower();
        if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(ext)) { Launch("photo", p); return; }
        if (new[] { ".txt", ".md", ".json", ".js", ".cs", ".log", ".ini", ".html", ".css", ".py", ".bat" }.Contains(ext)) Launch("notepad", p); else U.Open(p);
    }

    Win T(string k, string? arg, Win w) { w.Kind = k; w.Arg = arg; return w; }
    public void Launch(string k, string? arg = null)
    {
        HideMenu();
        switch (k)
        {
            case "files": T(k, arg, AddWin("Files", "folder", Views.Files(this, arg ?? Paths.Root), new Size(800, 520))); break;
            case "notepad": T(k, arg, AddWin(arg == null ? "Notepad" : Path.GetFileName(arg), "notepad", Views.Notepad(this, arg), new Size(680, 460))); break;
            case "calc": T(k, arg, AddWin("Calculator", "calc", Views.Calc(), new Size(330, 490))); break;
            case "paint": T(k, arg, AddWin("Paint", "paint", Views.PaintApp(this), new Size(860, 580))); break;
            case "term": T(k, arg, AddWin("Terminal", "term", Views.Term(), new Size(720, 440))); break;
            case "web": T(k, arg, AddWin("Browser", "browser", Web.Browser(this, arg), new Size(1040, 660))); break;
            case "store": T(k, arg, AddWin("MLD Store", "store", Web.Store(this), new Size(1040, 660))); break;
            case "tm": T(k, arg, AddWin("Task Manager", "tm", Tools.TaskMgr(this), new Size(640, 480))); break;
            case "photo": if (arg != null) T(k, arg, AddWin(Path.GetFileName(arg), "photo", Tools.Photo(arg), new Size(780, 560))); break;
            case "shot": Shot(); break;
            case "bin":
            case "vault":
            case "vm":
            case "settings":
                var title = k == "bin" ? "Recycle Bin" : k == "vm" ? "Full OS" : k == "vault" ? "Private Vault" : "Settings"; var ex = wins.FirstOrDefault(x => x.w.Title == title).w;
                if (ex != null) { ex.Visible = true; ex.BringToFront(); Sync(); }
                else if (k == "bin") T(k, arg, AddWin(title, "bin", Views.BinView(this), new Size(640, 440)));
                else if (k == "vault") T(k, arg, AddWin(title, "vault", VaultApp.View(this), new Size(700, 480)));
                else if (k == "vm") T(k, arg, AddWin(title, "vm", VmApp.View(this), new Size(760, 580)));
                else T(k, arg, AddWin(title, "settings", Views.Settings(this, int.TryParse(arg, out var pg) ? pg : 0), new Size(940, 640)));
                break;
        }
    }

    // ---------- start menu ----------
    RoundButton AppBtn(string name, Image img, Action act)
    {
        var b = new RoundButton { Text = name, Image = Icons.Scaled(img, Ui.D(48)), TextImageRelation = TextImageRelation.ImageAboveText, Size = Ui.D(96, 104), BackColor = Ui.Surface, Font = Ui.F(8.5f), Margin = new Padding(Ui.D(2)) };
        b.Click += (_, _) => act(); return b;
    }
    void BuildMenu()
    {
        menu.Size = Ui.D(444, 600); menu.Fill = Ui.Surface; menu.Radius = Ui.D(18); menu.Visible = false;
        var (sb, s) = Ui.Input("Search apps, files and settings", false, 44); msearch = s; sb.SetBounds(Ui.D(20), Ui.D(20), Ui.D(404), Ui.D(44));
        var pl = new Label { Text = "Pinned", ForeColor = Ui.Sub, Font = Ui.F(10, true), Location = new Point(Ui.D(26), Ui.D(80)), AutoSize = true, BackColor = Color.Transparent };
        grid.Bounds = new Rectangle(Ui.D(14), Ui.D(108), Ui.D(420), Ui.D(410)); grid.BackColor = Ui.Surface; grid.AutoScroll = true;
        foreach (var a in Catalog) { var key = a.K; grid.Controls.Add(AppBtn(a.N, Icons.App(IconKey(key)), () => Launch(key))); }
        AddUserApps();
        s.TextChanged += (_, _) => { foreach (Control b in grid.Controls) b.Visible = b.Text.Contains(s.Text, StringComparison.OrdinalIgnoreCase); };
        mAv.Bounds = new Rectangle(Ui.D(22), Ui.D(536), Ui.D(44), Ui.D(44)); mAv.SizeMode = PictureBoxSizeMode.Zoom; mAv.BackColor = Color.Transparent;
        mName.Bounds = new Rectangle(Ui.D(76), Ui.D(536), Ui.D(250), Ui.D(44)); mName.ForeColor = Ui.Text; mName.Font = Ui.F(11); mName.TextAlign = ContentAlignment.MiddleLeft; mName.BackColor = Color.Transparent;
        var pw = new RoundButton { Glyph = "power", GlyphScale = 0.42f, Bounds = new Rectangle(Ui.D(374), Ui.D(536), Ui.D(50), Ui.D(44)), Pill = true };
        var pm = new ContextMenuStrip();
        pm.Items.Add("Lock", null, (_, _) => { HideMenu(); Lock(); }); pm.Items.Add("Sleep (resume on any PC)", null, (_, _) => SleepNow()); pm.Items.Add("Safe eject USB", null, (_, _) => EjectNow()); pm.Items.Add("Panic lock (Ctrl+Alt+L)", null, (_, _) => PanicNow()); pm.Items.Add("Shut down", null, (_, _) => Application.Exit());
        pw.Click += (_, _) => pm.Show(pw, new Point(0, 0), ToolStripDropDownDirection.AboveLeft);
        menu.Controls.AddRange(new Control[] { sb, pl, grid, mAv, mName, pw }); pwBtn = pw; Ui.Dark(grid); Controls.Add(menu);
    }
    void ShowMenu() { quick.Visible = false; msearch.Text = ""; menu.Height = Math.Min(Ui.D(600), Height - bar.Height - Ui.D(24)); grid.Height = menu.Height - Ui.D(190); int uy = menu.Height - Ui.D(64); mAv.Top = uy; mName.Top = uy; if (pwBtn != null) pwBtn.Top = uy; menu.Location = new Point(Ui.D(10), Height - bar.Height - menu.Height - Ui.D(10)); mAv.Image = U.Avatar(Ui.D(44)); mName.Text = Cfg.Get("name", Environment.UserName); menu.Visible = true; menu.BringToFront(); }
    void HideMenu() { menu.Visible = false; quick.Visible = false; }
    void ToggleQuick() { if (quick.Visible) { quick.Visible = false; return; } menu.Visible = false; quick.Refresh2(); quick.Location = new Point(Width - quick.Width - Ui.D(10), Height - bar.Height - quick.Height - Ui.D(10)); quick.Visible = true; quick.BringToFront(); }
    bool SaveState()
    {
        if (Session.AlreadySaved) return true; var l = new List<WinState>();
        foreach (var (w, _) in wins)
        {
            if (w.Kind == "") continue; var r = w.RestBounds;
            var st = new WinState { Kind = w.Kind, Arg = w.Arg, X = Ui.Unscale(r.X), Y = Ui.Unscale(r.Y), W = Ui.Unscale(r.Width), H = Ui.Unscale(r.Height), Max = w.IsMax, Hidden = !w.Visible };
            if (w.Content.Tag is BrowserApi api) st.Tabs = api.Urls();
            if (w.Content.Tag is StateApi sa) st.Data = sa.Get();
            l.Add(st);
        }
        return Session.Save(l);
    }
    public async void SleepNow()
    {
        HideMenu(); Toast("Going to sleep…");
        try { await Web.FlushAll(); } catch { }
        if (VmApp.Running) { Toast("Saving Linux… this can take a moment"); var err = await Task.Run(VmApp.Freeze); if (err != null) Toast("Linux couldn't be frozen: " + err); }
        if (!SaveState()) { Toast("Couldn't save your session"); return; }
        await Task.Delay(500); Application.Exit();
    }
    async void RestoreSession()
    {
        var l = Session.Load(); if (l == null || l.Count == 0) return;
        foreach (var s in l)
        {
            try
            {
                int before = wins.Count; Launch(s.Kind, s.Kind == "web" ? s.Tabs?.FirstOrDefault() ?? s.Arg : s.Arg); if (wins.Count == before) continue;
                var w = wins[^1].w; w.Bounds = new Rectangle(Ui.D(s.X), Ui.D(s.Y), Ui.D(s.W), Ui.D(s.H)); if (s.Max) w.Toggle(); if (s.Hidden) w.Visible = false; if (s.Data != null && w.Content.Tag is StateApi sa2) sa2.Set(s.Data);
                if (s.Kind == "web" && s.Tabs != null && s.Tabs.Count > 1 && w.Content.Tag is BrowserApi api) { await Task.Delay(900); foreach (var u in s.Tabs.Skip(1)) api.Open(u); }
            }
            catch { }
        }
        Session.Clear(); Sync(); desk.Invalidate(); Toast("Welcome back. Picked up where you left off.");
    }
    void Lock() { if (locking) return; locking = true; try { using var l = new LoginForm(); l.ShowDialog(); } finally { locking = false; lastInput = DateTime.Now; } }

    public void ApplyWidgets()
    {
        if (wclock != null) { desk.Controls.Remove(wclock); wclock.Dispose(); wclock = null; }
        if (wnote != null) { desk.Controls.Remove(wnote); wnote.Dispose(); wnote = null; }
        if (Cfg.Get("wclock", "1") == "1") { wclock = new ClockWidget(); wclock.Location = new Point(Math.Max(0, desk.Width - wclock.Width - Ui.D(20)), Ui.D(16)); desk.Controls.Add(wclock); wclock.SendToBack(); }
        if (Cfg.Get("wnote", "1") == "1") { wnote = Tools.Sticky(); Ui.Fit(wnote); wnote.Size = Ui.D(wnote.Width, wnote.Height); wnote.Location = new Point(Math.Max(0, desk.Width - wnote.Width - Ui.D(24)), Ui.D(140)); desk.Controls.Add(wnote); wnote.SendToBack(); }
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
}
