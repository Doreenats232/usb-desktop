using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
namespace UsbDesktop;

static class Paths
{
    public static readonly string Root = Path.GetDirectoryName(Environment.ProcessPath)!;
    public static readonly string Desktop = Path.Combine(Root, "Desktop"), Apps = Path.Combine(Root, "apps"), Data = Path.Combine(Root, "data"), Vm = Path.Combine(Root, "vm");
    public static readonly string Bin = Path.Combine(Data, "Recycle"), Shots = Path.Combine(Desktop, "Screenshots");
}

static class Cfg
{
    static readonly string F = Path.Combine(Paths.Data, "settings.ini");
    static readonly Dictionary<string, string> d = new();
    static Cfg() { try { if (File.Exists(F)) foreach (var l in File.ReadAllLines(F)) { var i = l.IndexOf('='); if (i > 0) d[l[..i]] = l[(i + 1)..]; } } catch { } }
    public static string Get(string k, string def = "") => d.TryGetValue(k, out var v) ? v : def;
    public static int Int(string k, int def) => int.TryParse(Get(k), out var v) ? v : def;
    public static void Set(string k, string v) { d[k] = v; try { File.WriteAllLines(F, d.Select(x => x.Key + "=" + x.Value)); } catch { } }
}

static class Theme
{
    public static readonly (string N, Color A, Color B, Color C)[] P =
    {
        ("Aurora", Color.FromArgb(38,33,92), Color.FromArgb(83,74,183), Color.FromArgb(29,158,117)),
        ("Ocean", Color.FromArgb(4,44,83), Color.FromArgb(24,95,165), Color.FromArgb(93,202,165)),
        ("Sunset", Color.FromArgb(75,21,40), Color.FromArgb(212,83,126), Color.FromArgb(239,159,39)),
        ("Forest", Color.FromArgb(8,80,65), Color.FromArgb(29,158,117), Color.FromArgb(151,196,89)),
        ("Midnight", Color.FromArgb(18,18,28), Color.FromArgb(60,60,90), Color.FromArgb(127,119,221)),
    };
    public static int Idx => Math.Clamp(Cfg.Int("wp", 0), 0, P.Length - 1);
    public static Color Accent => P[Idx].B;

    public static Bitmap Preset(Size s, int i)
    {
        var (_, ca, cb, cc) = P[i];
        int W = Math.Max(s.Width, 1), H = Math.Max(s.Height, 1);
        var bm = new Bitmap(W, H); using var g = Graphics.FromImage(bm); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, H), ca, cb, 35f)) g.FillRectangle(lg, 0, 0, W, H);
        using (var br = new SolidBrush(Color.FromArgb(70, cc))) g.FillEllipse(br, W * 0.55f, -H * 0.35f, H * 1.1f, H * 1.1f);
        using (var br = new SolidBrush(Color.FromArgb(50, cc))) g.FillEllipse(br, -H * 0.3f, H * 0.5f, H * 0.9f, H * 0.9f);
        using (var br = new SolidBrush(Color.FromArgb(35, 255, 255, 255))) g.FillEllipse(br, W * 0.72f, H * 0.55f, H * 0.5f, H * 0.5f);
        for (int k = 0; k < 3; k++)
        {
            var pts = new PointF[9];
            for (int x = 0; x < 9; x++) pts[x] = new PointF(W * x / 8f, H * (0.62f + k * 0.09f) + (float)Math.Sin(x * 0.9 + k) * H * 0.05f);
            using var pen = new Pen(Color.FromArgb(45, 255, 255, 255), 2); g.DrawCurve(pen, pts, 0.5f);
        }
        return bm;
    }
    static Bitmap Cover(Image im, Size s)
    {
        var b = new Bitmap(Math.Max(s.Width, 1), Math.Max(s.Height, 1)); using var g = Graphics.FromImage(b);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float r = Math.Max((float)s.Width / im.Width, (float)s.Height / im.Height), w = im.Width * r, h = im.Height * r;
        g.DrawImage(im, (s.Width - w) / 2, (s.Height - h) / 2, w, h); return b;
    }
    public static Bitmap Wall(Size s)
    {
        var f = Cfg.Get("wallimg"); var im = f != "" && File.Exists(f) ? U.Load(f) : null;
        if (im != null) { using (im) return Cover(im, s); }
        return Preset(s, Idx);
    }
    public static Bitmap Lock(Size s)
    {
        var f = Cfg.Get("lockimg"); var im = f != "" && File.Exists(f) ? U.Load(f) : null;
        if (im != null) { using (im) return Cover(im, s); }
        return Wall(s);
    }
}

static class U
{
    public static GraphicsPath Round(Rectangle r, int rad)
    {
        var p = new GraphicsPath(); int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static Bitmap Tile(string glyph, Color c, int s = 56)
    {
        var b = new Bitmap(s, s); using var g = Graphics.FromImage(b); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = Round(new Rectangle(0, 0, s - 1, s - 1), s / 4);
        using var br = new LinearGradientBrush(new Rectangle(0, 0, s, s), ControlPaint.Light(c, 0.25f), ControlPaint.Dark(c, 0.05f), 60f);
        g.FillPath(br, p);
        using var f = new Font("Segoe UI Emoji", s * 0.42f, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, glyph, f, new Rectangle(0, 0, s, s), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return b;
    }
    public static Bitmap IconFor(string p)
    {
        if (Directory.Exists(p)) return Tile("📁", Color.FromArgb(239, 159, 39), 48);
        try { using var i = Icon.ExtractAssociatedIcon(p)!; return i.ToBitmap(); } catch { return Tile("📄", Color.FromArgb(95, 94, 90), 48); }
    }
    public static Bitmap? Load(string p) { try { using var i = Image.FromFile(p); return new Bitmap(i); } catch { return null; } }
    public static Bitmap Circle(Image src, int s)
    {
        var b = new Bitmap(s, s); using var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var p = new GraphicsPath(); p.AddEllipse(0, 0, s - 1, s - 1); g.SetClip(p);
        int m = Math.Min(src.Width, src.Height);
        g.DrawImage(src, new Rectangle(0, 0, s, s), (src.Width - m) / 2, (src.Height - m) / 2, m, m, GraphicsUnit.Pixel); return b;
    }
    public static Bitmap Avatar(int s)
    {
        var f = Path.Combine(Paths.Data, "avatar.png"); var im = File.Exists(f) ? Load(f) : null;
        if (im != null) { using (im) return Circle(im, s); }
        var b = new Bitmap(s, s); using var g = Graphics.FromImage(b); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var br = new SolidBrush(Theme.Accent)) g.FillEllipse(br, 0, 0, s - 1, s - 1);
        var n = Cfg.Get("name", Environment.UserName); var t = (n.Length > 0 ? n[..1] : "?").ToUpper();
        using var fn = new Font("Segoe UI", s * 0.42f, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, t, fn, new Rectangle(0, 0, s, s), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return b;
    }
    public static bool SetAvatar(string src)
    {
        var im = Load(src); if (im == null) return false;
        using (im) using (var sq = new Bitmap(256, 256))
        {
            using (var g = Graphics.FromImage(sq)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; int m = Math.Min(im.Width, im.Height); g.DrawImage(im, new Rectangle(0, 0, 256, 256), (im.Width - m) / 2, (im.Height - m) / 2, m, m, GraphicsUnit.Pixel); }
            sq.Save(Path.Combine(Paths.Data, "avatar.png"), ImageFormat.Png);
        }
        return true;
    }
    public static string SaveImg(string src, string name)
    {
        var dest = Path.Combine(Paths.Data, name + DateTime.Now.Ticks + ".png");
        using var im = Image.FromFile(src); im.Save(dest, ImageFormat.Png); return dest;
    }
    public static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("usbdesk" + s)));
    public static string MakeHash(string s)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(s), salt, 200000, HashAlgorithmName.SHA256, 32);
        return "v2:" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(h);
    }
    public static bool Check(string s, string stored)
    {
        if (!stored.StartsWith("v2:")) return stored == Hash(s);
        var p = stored.Split(':'); if (p.Length != 3) return false;
        var h = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(s), Convert.FromBase64String(p[1]), 200000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(p[2]));
    }
    public static string Unique(string dir, string name)
    {
        var n = name; int i = 2;
        while (File.Exists(Path.Combine(dir, n)) || Directory.Exists(Path.Combine(dir, n))) n = Path.GetFileNameWithoutExtension(name) + $" ({i++})" + Path.GetExtension(name);
        return Path.Combine(dir, n);
    }
    public static void Open(string p)
    {
        try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(p) ?? Paths.Root }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}

static class Bin
{
    public static void Trash(string p)
    {
        var d = Path.Combine(Paths.Bin, DateTime.Now.Ticks + "_" + Path.GetFileName(p));
        if (Directory.Exists(p)) Directory.Move(p, d); else File.Move(p, d);
        File.WriteAllText(d + ".orig", p);
    }
    public static string[] Items() => Directory.GetFileSystemEntries(Paths.Bin).Where(x => !x.EndsWith(".orig")).ToArray();
    public static string Name(string d) { var n = Path.GetFileName(d); return n[(n.IndexOf('_') + 1)..]; }
    public static void Restore(string d)
    {
        var o = File.Exists(d + ".orig") ? File.ReadAllText(d + ".orig") : Path.Combine(Paths.Desktop, Name(d));
        Directory.CreateDirectory(Path.GetDirectoryName(o)!);
        var t = U.Unique(Path.GetDirectoryName(o)!, Path.GetFileName(o));
        if (Directory.Exists(d)) Directory.Move(d, t); else File.Move(d, t);
        File.Delete(d + ".orig");
    }
    public static void Empty() { foreach (var x in Directory.GetFileSystemEntries(Paths.Bin)) { if (Directory.Exists(x)) Directory.Delete(x, true); else File.Delete(x); } }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        foreach (var d in new[] { Paths.Desktop, Paths.Apps, Paths.Data, Paths.Vm, Paths.Bin, Paths.Shots }) Directory.CreateDirectory(d);
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Err(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Err((Exception)e.ExceptionObject);
        try
        {
            using (var l = new LoginForm()) if (l.ShowDialog() != DialogResult.OK) return;
            Application.Run(new DesktopForm());
        }
        catch (Exception ex) { Err(ex); }
    }
    static void Err(Exception e)
    {
        try { File.AppendAllText(Path.Combine(Paths.Data, "error.log"), DateTime.Now + " " + e + "\n\n"); } catch { }
        MessageBox.Show(e.Message + "\n\nDetails saved to data\\error.log", "USB Desktop");
    }
}

class LoginForm : Form
{
    readonly string uf = Path.Combine(Paths.Data, "user.txt");
    readonly bool first;
    readonly Label clock = new(), date = new(), name = new(), hint = new(), err = new();
    readonly TextBox pass = new();
    readonly PictureBox av = new();
    readonly Panel col = new();
    Bitmap? bg; int fails;

    static Label Lb(Color c, float sz, string fam = "Segoe UI") => new() { ForeColor = c, Font = new Font(fam, sz), BackColor = Color.Transparent, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };

    public LoginForm()
    {
        first = !File.Exists(uf);
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; DoubleBuffered = true; KeyPreview = true;
        var white = Color.FromArgb(245, 244, 255);
        foreach (var l in new[] { clock, date, name, hint, err }) { }
        var c1 = Lb(white, 54, "Segoe UI Light"); clock.Font = c1.Font; clock.ForeColor = white; clock.BackColor = Color.Transparent; clock.AutoSize = true; clock.Location = new Point(40, 26);
        date.Font = new Font("Segoe UI", 15); date.ForeColor = Color.FromArgb(220, 218, 250); date.BackColor = Color.Transparent; date.AutoSize = true; date.Location = new Point(46, 138);
        Controls.Add(clock); Controls.Add(date);

        col.Size = new Size(360, 380); col.BackColor = Color.Transparent;
        av.Bounds = new Rectangle(110, 0, 140, 140); av.BackColor = Color.Transparent; av.SizeMode = PictureBoxSizeMode.Zoom; av.Image = U.Avatar(140);
        if (first) { av.Cursor = Cursors.Hand; av.Click += (_, _) => { using var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" }; if (d.ShowDialog() == DialogResult.OK && U.SetAvatar(d.FileName)) av.Image = U.Avatar(140); }; }
        name.Font = new Font("Segoe UI Light", 22); name.ForeColor = white; name.BackColor = Color.Transparent; name.TextAlign = ContentAlignment.MiddleCenter; name.Text = Cfg.Get("name", Environment.UserName); name.SetBounds(0, 150, 360, 44);
        hint.Font = new Font("Segoe UI", 10); hint.ForeColor = Color.FromArgb(220, 218, 250); hint.BackColor = Color.Transparent; hint.TextAlign = ContentAlignment.MiddleCenter;
        hint.Text = first ? "Welcome! Click the circle to add a photo, then create a password" : "Enter your password"; hint.SetBounds(0, 196, 360, 24);
        pass.UseSystemPasswordChar = true; pass.Font = new Font("Segoe UI", 13); pass.SetBounds(36, 232, 248, 34); pass.BackColor = Color.FromArgb(48, 42, 100); pass.ForeColor = Color.White; pass.BorderStyle = BorderStyle.FixedSingle;
        var go = new Button { Text = "→", Font = new Font("Segoe UI", 13), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White, Bounds = new Rectangle(290, 232, 34, 34), Cursor = Cursors.Hand };
        go.FlatAppearance.BorderSize = 0; go.Click += (_, _) => Submit();
        err.Font = new Font("Segoe UI", 10); err.ForeColor = Color.FromArgb(255, 170, 170); err.BackColor = Color.Transparent; err.TextAlign = ContentAlignment.MiddleCenter; err.SetBounds(0, 274, 360, 24);
        col.Controls.AddRange(new Control[] { av, name, hint, pass, go, err });
        Controls.Add(col);

        var usb = new Label { Text = "💾  Running from " + Path.GetPathRoot(Paths.Root), ForeColor = Color.FromArgb(220, 218, 250), BackColor = Color.Transparent, Font = new Font("Segoe UI Emoji", 10), AutoSize = true };
        var off = new Button { Text = "⏻", Font = new Font("Segoe UI", 14), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Theme.Accent, Size = new Size(46, 46) };
        off.FlatAppearance.BorderSize = 0; off.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(usb); Controls.Add(off);
        Resize += (_, _) =>
        {
            col.Location = new Point((ClientSize.Width - col.Width) / 2, (ClientSize.Height - 300) / 2 - 20);
            off.Location = new Point(ClientSize.Width - 70, ClientSize.Height - 70); usb.Location = new Point(24, ClientSize.Height - 40);
            bg?.Dispose(); bg = null; Invalidate();
        };
        AcceptButton = go;
        var t = new System.Windows.Forms.Timer { Interval = 1000 }; t.Tick += (_, _) => Tick(); t.Start(); Tick();
        Shown += (_, _) => pass.Focus();
    }
    void Tick() { clock.Text = DateTime.Now.ToString("HH:mm"); date.Text = DateTime.Now.ToString("dddd, MMMM d"); }
    void Submit()
    {
        if (pass.Text.Length < 1) { err.Text = "Type a password"; return; }
        if (first) { File.WriteAllText(uf, U.MakeHash(pass.Text)); DialogResult = DialogResult.OK; return; }
        var st = File.ReadAllText(uf).Trim();
        if (U.Check(pass.Text, st)) { if (!st.StartsWith("v2:")) File.WriteAllText(uf, U.MakeHash(pass.Text)); DialogResult = DialogResult.OK; }
        else
        {
            fails++; err.Text = fails >= 3 ? "Too many tries. Wait a few seconds..." : "Wrong password, try again"; pass.SelectAll();
            if (fails >= 3) { fails = 0; pass.Enabled = false; Task.Delay(4000).ContinueWith(_ => { if (!IsDisposed) BeginInvoke(new Action(() => { pass.Enabled = true; pass.Focus(); err.Text = ""; })); }); }
        }
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        bg ??= Theme.Lock(ClientSize);
        e.Graphics.DrawImage(bg, 0, 0);
        using var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0)); e.Graphics.FillRectangle(b, ClientRectangle);
    }
}
