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
        ("Aurora", Color.FromArgb(24,20,66), Color.FromArgb(108,92,231), Color.FromArgb(29,198,150)),
        ("Ocean", Color.FromArgb(4,30,60), Color.FromArgb(30,136,229), Color.FromArgb(93,202,225)),
        ("Sunset", Color.FromArgb(60,16,40), Color.FromArgb(236,72,153), Color.FromArgb(255,170,60)),
        ("Forest", Color.FromArgb(6,50,42), Color.FromArgb(16,185,129), Color.FromArgb(170,220,90)),
        ("Midnight", Color.FromArgb(12,12,22), Color.FromArgb(99,102,241), Color.FromArgb(127,119,221)),
        ("Neon", Color.FromArgb(10,8,34), Color.FromArgb(139,92,246), Color.FromArgb(0,220,255)),
        ("Rose", Color.FromArgb(46,10,38), Color.FromArgb(244,63,94), Color.FromArgb(255,190,120)),
        ("Mint", Color.FromArgb(6,34,44), Color.FromArgb(20,184,166), Color.FromArgb(140,255,200)),
    };
    public static int Idx => Math.Clamp(Cfg.Int("wp", 0), 0, P.Length - 1);
    public static Color Accent { get { var h = Cfg.Get("accent", ""); if (h.Length == 7 && h[0] == '#') { try { return ColorTranslator.FromHtml(h); } catch { } } return P[Idx].B; } }

    static void Glow(Graphics g, float cx, float cy, float r, Color c, int alpha)
    {
        using var path = new GraphicsPath(); path.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
        using var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb(alpha, c), SurroundColors = new[] { Color.FromArgb(0, c) } };
        g.FillPath(pg, path);
    }
    public static Bitmap Preset(Size s, int i)
    {
        var (_, ca, cb, cc) = P[i]; int W = Math.Max(s.Width, 8), H = Math.Max(s.Height, 8);
        var bm = new Bitmap(W, H); using var g = Graphics.FromImage(bm); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, H), ca, Ui.Mix(ca, cb, 0.4f), 35f)) g.FillRectangle(lg, 0, 0, W, H);
        Glow(g, W * 0.82f, H * 0.12f, H * 0.95f, cc, 150); Glow(g, W * 0.12f, H * 0.95f, H * 0.85f, cb, 170); Glow(g, W * 0.5f, H * 0.55f, H * 0.6f, cb, 70); Glow(g, W * 0.95f, H * 0.9f, H * 0.5f, cc, 60);
        for (int k = 0; k < 3; k++)
        {
            var pts = new PointF[9];
            for (int x = 0; x < 9; x++) pts[x] = new PointF(W * x / 8f, H * (0.64f + k * 0.08f) + (float)Math.Sin(x * 0.9 + k) * H * 0.05f);
            using var pen = new Pen(Color.FromArgb(34, 255, 255, 255), Math.Max(1.5f, H / 400f)); g.DrawCurve(pen, pts, 0.5f);
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
        var p = new GraphicsPath(); int d = Math.Max(2, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static Bitmap IconFor(string p) => Icons.ForFile(p);
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
        using (var br = new LinearGradientBrush(new Rectangle(0, 0, s, s), Theme.Accent, Ui.Accent2, 45f)) g.FillEllipse(br, 0, 0, s - 1, s - 1);
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
    public static byte[] Pbkdf2(byte[] pw, byte[] salt, int iter)
    {
        try { return Rfc2898DeriveBytes.Pbkdf2(pw, salt, iter, HashAlgorithmName.SHA256, 32); }
        catch (CryptographicException)
        {
            using var h = new HMACSHA256(pw); var s1 = new byte[salt.Length + 4]; salt.CopyTo(s1, 0); s1[^1] = 1;
            var u = h.ComputeHash(s1); var t = (byte[])u.Clone();
            for (int i = 1; i < iter; i++) { u = h.ComputeHash(u); for (int j = 0; j < t.Length; j++) t[j] ^= u[j]; }
            return t;
        }
    }
    public static string MakeHash(string s)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var h = Pbkdf2(Encoding.UTF8.GetBytes(s), salt, 200000);
        return "v2:" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(h);
    }
    public static bool Check(string s, string stored)
    {
        if (!stored.StartsWith("v2:")) return stored == Hash(s);
        var p = stored.Split(':'); if (p.Length != 3) return false;
        var h = Pbkdf2(Encoding.UTF8.GetBytes(s), Convert.FromBase64String(p[1]), 200000);
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
    static bool InsideArchive()
    {
        try
        {
            var p = Environment.ProcessPath ?? ""; var t = Path.GetTempPath();
            return p.StartsWith(t, StringComparison.OrdinalIgnoreCase) || p.Contains("Rar$", StringComparison.OrdinalIgnoreCase) || p.Contains(".zip\\", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (InsideArchive())
        {
            MessageBox.Show("It looks like Launcher.exe is being run from inside the zip file.\n\nRight-click the zip, choose Extract All, then open Launcher.exe from the extracted folder (or copy it onto your USB drive first).", "USB Desktop");
            return;
        }
        try { foreach (var d in new[] { Paths.Desktop, Paths.Apps, Paths.Data, Paths.Vm, Paths.Bin, Paths.Shots }) Directory.CreateDirectory(d); }
        catch (Exception ex) { MessageBox.Show("This location is read-only, so USB Desktop can't set itself up here.\n\nMove Launcher.exe to a normal folder or your USB drive.\n\n" + ex.Message, "USB Desktop"); return; }
        Native.Prepare();
        ToolStripManager.Renderer = new DarkRenderer();
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
    readonly TextBox pass;
    readonly PictureBox av = new();
    readonly Panel col = new();
    readonly RoundButton go = new();
    Bitmap? bg; int fails;

    public LoginForm()
    {
        first = !File.Exists(uf);
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; DoubleBuffered = true; KeyPreview = true; BackColor = Ui.Bg;
        var white = Color.FromArgb(246, 246, 255); var soft = Color.FromArgb(214, 216, 240);

        clock.Font = new Font("Segoe UI Light", 54); clock.ForeColor = white; clock.BackColor = Color.Transparent; clock.AutoSize = true;
        date.Font = Ui.F(15); date.ForeColor = soft; date.BackColor = Color.Transparent; date.AutoSize = true;
        Tick(); clock.Location = new Point(Ui.D(44), Ui.D(24)); date.Location = new Point(Ui.D(50), clock.Bottom - Ui.D(2));
        Controls.Add(clock); Controls.Add(date);

        int cw = Ui.D(420), a = Ui.D(136), gap = Ui.D(14);
        col.BackColor = Color.Transparent;
        av.Bounds = new Rectangle((cw - a) / 2, 0, a, a); av.BackColor = Color.Transparent; av.SizeMode = PictureBoxSizeMode.Zoom; av.Image = U.Avatar(a);
        if (first) { av.Cursor = Cursors.Hand; av.Click += (_, _) => { using var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" }; if (d.ShowDialog() == DialogResult.OK && U.SetAvatar(d.FileName)) av.Image = U.Avatar(a); }; }
        int y = a + gap;
        name.Font = new Font("Segoe UI Light", 22); name.ForeColor = white; name.BackColor = Color.Transparent; name.TextAlign = ContentAlignment.MiddleCenter; name.AutoEllipsis = true; name.Text = Cfg.Get("name", Environment.UserName);
        var nh = TextRenderer.MeasureText("Ag", name.Font).Height + Ui.D(8); name.SetBounds(0, y, cw, nh); y += nh + Ui.D(4);
        hint.Font = Ui.F(10.5f); hint.ForeColor = soft; hint.BackColor = Color.Transparent; hint.TextAlign = ContentAlignment.TopCenter;
        hint.Text = first ? "Welcome! Click the circle to add a photo, then create a password" : "Enter your password to continue";
        var hh = TextRenderer.MeasureText(hint.Text, hint.Font, new Size(cw - Ui.D(40), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + Ui.D(6); hint.SetBounds(Ui.D(20), y, cw - Ui.D(40), hh); y += hh + gap;

        var (box, tb) = Ui.Input("Password", true, 52); pass = tb; box.Floating = true; box.Fill = Color.FromArgb(44, 46, 76); tb.BackColor = box.Fill; box.SetBounds(Ui.D(30), y, cw - Ui.D(60), Ui.D(52)); y += Ui.D(52) + Ui.D(8);
        go.Glyph = "fwd"; go.GlyphScale = 0.4f; go.Pill = true; go.BackColor = Theme.Accent; box.Controls.Add(go);
        void Lay() { go.SetBounds(box.Width - Ui.D(46), Ui.D(6), Ui.D(40), box.Height - Ui.D(12)); tb.Width = box.Width - Ui.D(22) - Ui.D(54); tb.Left = Ui.D(20); }
        box.Resize += (_, _) => Lay(); Lay();
        go.Click += (_, _) => Submit();
        err.Font = Ui.F(10); err.ForeColor = Color.FromArgb(255, 150, 150); err.BackColor = Color.Transparent; err.TextAlign = ContentAlignment.MiddleCenter; err.SetBounds(0, y, cw, Ui.D(28)); y += Ui.D(30);
        col.Size = new Size(cw, y);
        col.Controls.AddRange(new Control[] { av, name, hint, box, err }); Controls.Add(col);

        var usb = new Label { Text = "Running from " + Path.GetPathRoot(Paths.Root), ForeColor = soft, BackColor = Color.Transparent, Font = Ui.F(10), AutoSize = true };
        var off = new RoundButton { Glyph = "power", GlyphScale = 0.42f, Pill = true, OnImage = true, BackColor = Color.FromArgb(52, 54, 90), Size = Ui.D(50, 50) };
        off.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(usb); Controls.Add(off);
        Resize += (_, _) =>
        {
            col.Location = new Point((ClientSize.Width - col.Width) / 2, Math.Max(Ui.D(20), (ClientSize.Height - col.Height) / 2 - Ui.D(10)));
            off.Location = new Point(ClientSize.Width - Ui.D(76), ClientSize.Height - Ui.D(76)); usb.Location = new Point(Ui.D(28), ClientSize.Height - Ui.D(44));
            bg?.Dispose(); bg = null; Invalidate();
        };
        AcceptButton = go;
        var t = new System.Windows.Forms.Timer { Interval = 1000 }; t.Tick += (_, _) => Tick(); t.Start();
        Shown += (_, _) => pass.Focus();
    }
    void Tick() { clock.Text = DateTime.Now.ToString("HH:mm"); date.Text = DateTime.Now.ToString("dddd, MMMM d"); }
    void Submit()
    {
        if (pass.Text.Length < 1) { err.Text = "Type a password"; return; }
        if (first) { File.WriteAllText(uf, U.MakeHash(pass.Text)); Session.SetKey(pass.Text); DialogResult = DialogResult.OK; return; }
        var st = File.ReadAllText(uf).Trim();
        if (U.Check(pass.Text, st)) { if (!st.StartsWith("v2:")) File.WriteAllText(uf, U.MakeHash(pass.Text)); Session.SetKey(pass.Text); DialogResult = DialogResult.OK; }
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
        using var b = new SolidBrush(Color.FromArgb(80, 0, 0, 0)); e.Graphics.FillRectangle(b, ClientRectangle);
    }
}
