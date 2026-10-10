using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;

namespace UsbDesktop;

static class Paths
{
    public static readonly string Root = Path.GetDirectoryName(Environment.ProcessPath)!;
    public static readonly string Desktop = Path.Combine(Root, "Desktop"), Apps = Path.Combine(Root, "apps"), Data = Path.Combine(Root, "data"), Vm = Path.Combine(Root, "vm");
    public static readonly string Bin = Path.Combine(Data, "Bin"), Shots = Path.Combine(Desktop, "Screenshots");
}

static class Cfg
{
    static readonly string F = Path.Combine(Paths.Data, "settings.ini");
    static readonly Dictionary<string, string> d = Load();
    static Dictionary<string, string> Load()
    {
        var r = new Dictionary<string, string>();
        try { foreach (var l in File.ReadAllLines(F)) { var i = l.IndexOf('='); if (i > 0) r[l[..i]] = l[(i + 1)..]; } } catch { }
        return r;
    }
    public static string Get(string k, string def = "") => d.TryGetValue(k, out var v) ? v : def;
    public static void Set(string k, string v) { d[k] = v; try { File.WriteAllLines(F, d.Select(x => x.Key + "=" + x.Value)); } catch { } }
}

static class Auth
{
    public static readonly string UserFile = Path.Combine(Paths.Data, "user.txt");
    public static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("usbdesk" + s)));
}

static class Theme
{
    public static readonly (string Name, Color Bg, Color A, Color B)[] Walls =
    {
        ("Purple", Color.FromArgb(38, 33, 92), Color.FromArgb(83, 74, 183), Color.FromArgb(175, 169, 236)),
        ("Ocean", Color.FromArgb(4, 44, 83), Color.FromArgb(24, 95, 165), Color.FromArgb(133, 183, 235)),
        ("Sunset", Color.FromArgb(75, 21, 40), Color.FromArgb(212, 83, 126), Color.FromArgb(250, 199, 117)),
        ("Forest", Color.FromArgb(8, 50, 40), Color.FromArgb(29, 158, 117), Color.FromArgb(151, 196, 89)),
        ("Midnight", Color.FromArgb(18, 18, 30), Color.FromArgb(55, 55, 90), Color.FromArgb(127, 119, 221)),
    };
    public static Color Accent => ColorTranslator.FromHtml(Cfg.Get("accent", "#7F77DD"));
    public static string Name { get { var n = Cfg.Get("name", ""); return string.IsNullOrWhiteSpace(n) ? Environment.UserName : n; } }
    public static string AvatarPath => Path.Combine(Paths.Data, "avatar.img");
    public static string LockPath => Path.Combine(Paths.Data, "lock.img");
    public static Bitmap Load(string p) { using var im = Image.FromFile(p); return new Bitmap(im); }

    public static void Cover(Graphics g, Image im, Size s)
    {
        float k = Math.Max((float)s.Width / im.Width, (float)s.Height / im.Height), w = im.Width * k, h = im.Height * k;
        g.DrawImage(im, (s.Width - w) / 2, (s.Height - h) / 2, w, h);
    }

    public static Bitmap Wall(Size s, string? name = null)
    {
        var b = new Bitmap(Math.Max(s.Width, 1), Math.Max(s.Height, 1));
        using var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        name ??= Cfg.Get("wall", "Purple");
        var cp = Path.Combine(Paths.Data, "wall.img");
        if (name == "custom" && File.Exists(cp)) { using var im = Load(cp); Cover(g, im, b.Size); return b; }
        var w = Walls.FirstOrDefault(x => x.Name == name); if (w.Name == null) w = Walls[0];
        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, b.Width, b.Height), w.Bg, w.A, 55f)) g.FillRectangle(lg, 0, 0, b.Width, b.Height);
        using (var br = new SolidBrush(Color.FromArgb(70, w.B))) g.FillEllipse(br, b.Width * 0.55f, -b.Height * 0.5f, b.Width * 0.7f, b.Width * 0.7f);
        using (var br = new SolidBrush(Color.FromArgb(50, w.B))) g.FillEllipse(br, -b.Width * 0.15f, b.Height * 0.55f, b.Width * 0.5f, b.Width * 0.5f);
        using (var br = new SolidBrush(Color.FromArgb(40, Color.White))) g.FillEllipse(br, b.Width * 0.78f, b.Height * 0.6f, b.Width * 0.3f, b.Width * 0.3f);
        return b;
    }

    public static Bitmap LockBg(Size s)
    {
        if (!File.Exists(LockPath)) return Wall(s);
        var b = new Bitmap(Math.Max(s.Width, 1), Math.Max(s.Height, 1));
        using var g = Graphics.FromImage(b); using var im = Load(LockPath); Cover(g, im, b.Size);
        return b;
    }

    public static void Avatar(Graphics g, Rectangle r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var gp = new GraphicsPath(); gp.AddEllipse(r);
        var st = g.Save(); g.SetClip(gp);
        if (File.Exists(AvatarPath)) { using var im = Load(AvatarPath); g.TranslateTransform(r.X, r.Y); Cover(g, im, r.Size); }
        else
        {
            using var br = new SolidBrush(Accent); g.FillEllipse(br, r);
            using var f = new Font("Segoe UI", r.Width / 2.6f);
            TextRenderer.DrawText(g, Name[..1].ToUpper(), f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        g.Restore(st);
        using var pen = new Pen(Color.FromArgb(210, 175, 169, 236), Math.Max(1.5f, r.Width / 32f)); g.DrawEllipse(pen, r);
    }

    public static Bitmap AvatarBmp(int s) { var b = new Bitmap(s, s); using var g = Graphics.FromImage(b); Avatar(g, new Rectangle(2, 2, s - 4, s - 4)); return b; }

    public static Bitmap Tile(Color c, string glyph, int sz = 48)
    {
        var b = new Bitmap(sz, sz); using var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using var p = new GraphicsPath(); int d = sz / 2;
        p.AddArc(0, 0, d, d, 180, 90); p.AddArc(sz - d - 1, 0, d, d, 270, 90); p.AddArc(sz - d - 1, sz - d - 1, d, d, 0, 90); p.AddArc(0, sz - d - 1, d, d, 90, 90); p.CloseFigure();
        using var br = new SolidBrush(c); g.FillPath(br, p);
        using var f = new Font("Segoe UI Emoji", sz * 0.4f); using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(glyph, f, Brushes.White, new RectangleF(0, 0, sz, sz), sf);
        return b;
    }
}

class Splash : Form
{
    int t; readonly System.Windows.Forms.Timer tm = new() { Interval = 30 };
    public Splash()
    {
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; BackColor = Color.FromArgb(14, 14, 24); DoubleBuffered = true;
        tm.Tick += (_, _) => { if (++t > 80) { tm.Stop(); Close(); return; } Invalidate(); };
        tm.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; int cx = Width / 2, cy = Height / 2;
        using var f = new Font("Segoe UI Light", 44);
        TextRenderer.DrawText(g, "USB OS", f, new Rectangle(0, cy - 110, Width, 80), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        using var tr = new SolidBrush(Color.FromArgb(50, 50, 70)); g.FillRectangle(tr, cx - 130, cy + 10, 260, 4);
        using var fl = new SolidBrush(Theme.Accent); g.FillRectangle(fl, cx - 130, cy + 10, 260 * Math.Min(t, 80) / 80, 4);
        using var f2 = new Font("Segoe UI", 10);
        TextRenderer.DrawText(g, "Starting from " + Path.GetPathRoot(Paths.Root), f2, new Rectangle(0, cy + 26, Width, 24), Color.FromArgb(150, 150, 180), TextFormatFlags.HorizontalCenter);
    }
}

static class Program
{
    static void Log(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(Paths.Data, "error.log"), DateTime.Now + "\n" + ex + "\n\n"); } catch { }
        MessageBox.Show("Something went wrong:\n" + ex.Message + "\n\nSaved to data\\error.log", "USB OS");
    }

    [STAThread]
    static void Main()
    {
        try
        {
            foreach (var d in new[] { Paths.Desktop, Paths.Apps, Paths.Data, Paths.Vm, Paths.Bin }) Directory.CreateDirectory(d);
            Application.ThreadException += (_, e) => Log(e.Exception);
            ApplicationConfiguration.Initialize();
            using (var s = new Splash()) s.ShowDialog();
            using (var l = new LoginForm()) if (l.ShowDialog() != DialogResult.OK) return;
            Application.Run(new DesktopForm());
        }
        catch (Exception ex) { Log(ex); }
    }
}
