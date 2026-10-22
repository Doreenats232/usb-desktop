using System.Drawing.Drawing2D;
namespace UsbDesktop;

// The walking buddy: a small animated character that wanders the desktop when the assistant window is closed.
// Click it for a small chat bar at its feet; replies appear in a white speech bubble and are spoken out loud.
class Outfit
{
    public string Name = ""; public Color T1 = Color.Empty, T2 = Color.Empty, Pants = Color.FromArgb(50, 62, 112), Shoes = Color.FromArgb(240, 240, 245), HatC = Color.Gray, CapeC = Color.Crimson, MaskC = Color.Navy, ScarfC = Color.Gold, PackC = Color.FromArgb(230, 120, 40);
    public string Hat = ""; public bool Glasses, Sun, Cape, Mask, Scarf, Tie, Pack, Shorts, Phones;
}

class PetSprite
{
    public int Width = 150, Height = 170;
    public bool Right = true, Moving, Talking; public int Push; public Outfit Fit = Pet.Outfits[0];
    public float Bend; public Image? Held;   // Bend: 0 standing .. 1 squatting down to pick up / put down; Held: the icon picture being carried
    float phase, dustT; DateTime nextBlink = DateTime.Now.AddSeconds(2), blinkEnd; static readonly Random R = new();
    class Puff { public float X, Y, Age; } readonly List<Puff> dust = new();
    public void Step(float dt)
    {
        if (Moving) phase += dt * (Push > 0 ? 6f : 9f); var now = DateTime.Now; if (now >= nextBlink) { blinkEnd = now.AddMilliseconds(140); nextBlink = now.AddSeconds(2 + R.NextDouble() * 3); }
        foreach (var p in dust) p.Age += dt / 0.7f; dust.RemoveAll(p => p.Age >= 1f);
        if (Push > 0 && Moving)
        {
            dustT += dt; float s = Math.Min(Width / 110f, Height / 195f);
            if (dustT > 0.2f) { dustT = 0; dust.Add(new Puff { X = Width / 2f + (Push == 1 ? (Right ? -1 : 1) * 40 * s : (R.Next(2) == 0 ? -30 : 30) * s), Y = Height - 10 * s }); }
        }
    }
    static Color Sh(Color c, float k) => Color.FromArgb(c.A, Math.Clamp((int)(c.R * k), 0, 255), Math.Clamp((int)(c.G * k), 0, 255), Math.Clamp((int)(c.B * k), 0, 255));
    static Color Al(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c.R, c.G, c.B);
    static GraphicsPath RR(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = Math.Max(1f, Math.Min(r * 2f, Math.Min(w, h)));
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90); p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    // a shaded ball: bright where the light hits (upper side), dark on the far edge, so flat ovals look round
    static void Blob(Graphics g, float cx, float cy, float rx, float ry, Color c, float L, float k1 = 1.18f, float k2 = 0.7f)
    {
        using var path = new GraphicsPath(); path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        using var pb = new PathGradientBrush(path) { CenterPoint = new PointF(cx + L * rx * 0.38f, cy - ry * 0.38f), CenterColor = Sh(c, k1), SurroundColors = new[] { Sh(c, k2) } };
        g.FillPath(pb, path);
    }
    // a round arm or leg: dark edge, body colour, bright highlight strip on the lit side
    static PointF Limb(Graphics g, float x, float y, float len, float deg, Color c, float w, float L)
    {
        float r = deg * MathF.PI / 180f, ex = x + MathF.Sin(r) * len, ey = y + MathF.Cos(r) * len;
        using (var p = new Pen(Sh(c, .6f), w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x, y, ex, ey);
        using (var p = new Pen(c, w * .76f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x + L * w * .08f, y, ex + L * w * .08f, ey);
        using (var p = new Pen(Al(Sh(c, 1.5f), 150), w * .24f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x + L * w * .22f, y, ex + L * w * .22f, ey);
        return new PointF(ex, ey);
    }
    public void Draw(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = Math.Min(Width / 110f, Height / 195f); float t = Environment.TickCount / 1000f; var f = Fit;
        bool flip = !Right && Push < 2; float L = flip ? 1f : -1f; float bd = Math.Clamp(Bend, 0f, 1f);   // L: which way the light falls so shading stays on the same side when the character turns
        foreach (var d in dust) { using var b = new SolidBrush(Color.FromArgb((int)(120 * (1 - d.Age)), 215, 205, 195)); float r = (6 + d.Age * 16) * s; g.FillEllipse(b, d.X - r, d.Y - r * 0.6f, r * 2, r * 1.2f); }
        g.TranslateTransform(Width / 2f, Height - 8 * s); if (flip) g.ScaleTransform(-1, 1); g.ScaleTransform(s, s);
        float sw = Moving ? MathF.Sin(phase) : 0f, bob = Moving ? -MathF.Abs(MathF.Cos(phase)) * (Push > 0 ? 2f : 4f) : MathF.Sin(t * 2f) * -1.5f;
        var skin = Color.FromArgb(226, 176, 136); var hair = Color.FromArgb(44, 32, 34); var c1 = f.T1.IsEmpty ? Theme.Accent : f.T1; var c2 = f.T2.IsEmpty ? Ui.Accent2 : f.T2; var dark = Color.FromArgb(30, 24, 28);
        // soft contact shadow on the floor
        using (var gp = new GraphicsPath()) { gp.AddEllipse(-36f, -10f, 72f, 20f); using var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(120, 0, 0, 0), SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) } }; g.FillPath(pb, gp); }
        g.TranslateTransform(0, bob); if (Push == 1) g.RotateTransform(10); else if (Push == 3) g.RotateTransform(12);
        float dy = bd * 22f, tilt = bd * 40f, la = (Push > 0 ? 24f : 30f) * (1f - bd);
        void Leg(float x, float deg, Color shoe)
        {
            deg += (x > 0 ? 1 : -1) * bd * 18f; float len = 54f - dy, hy = -62f + dy; PointF e;
            if (f.Shorts) { var k = Limb(g, x, hy, len * .48f, deg, f.Pants, 16, L); e = Limb(g, k.X, k.Y, len * .52f, deg, skin, 11, L); }
            else e = Limb(g, x, hy, len, deg, f.Pants, 16, L);
            Blob(g, e.X + 2f, e.Y, 11f, 6.5f, shoe, L);
            using var sole = new Pen(Al(Sh(shoe, .5f), 190), 2f); g.DrawArc(sole, e.X - 9f, e.Y - 2f, 22f, 9f, 10, 160);
        }
        Leg(8, -sw * la, Sh(f.Shoes, .93f)); Leg(-8, sw * la, f.Shoes);
        // upper body bends forward from the hips (squat) when picking things up
        g.TranslateTransform(0, dy); g.TranslateTransform(0, -62f); g.RotateTransform(tilt); g.TranslateTransform(0, 62f);
        if (f.Cape)
        {
            float sway = Moving ? MathF.Sin(phase + 1f) * 8f : MathF.Sin(t * 2f) * 3f;
            using var lb = new LinearGradientBrush(new PointF(-48, -118), new PointF(0, -38), Sh(f.CapeC, 1.2f), Sh(f.CapeC, .55f));
            g.FillPolygon(lb, new[] { new PointF(-22, -118), new PointF(18, -118), new PointF(-6 - sway, -38), new PointF(-48 - sway, -50) });
        }
        if (f.Pack) { using var pp = RR(-42, -118, 22, 48, 8); using var lb = new LinearGradientBrush(new PointF(-42, -118), new PointF(-20, -70), Sh(f.PackC, 1.25f), Sh(f.PackC, .65f)); g.FillPath(lb, pp); using var pn = new Pen(Al(Sh(f.PackC, .5f), 200), 1.5f); g.DrawPath(pn, pp); }
        // torso with light and shadow
        using (var path = RR(-25, -124, 50, 66, 16))
        {
            using (var lg = new LinearGradientBrush(new PointF(L * 25, -124), new PointF(-L * 25, -58), Sh(c1, 1.25f), Sh(c2, .7f))) g.FillPath(lg, path);
            g.SetClip(path, CombineMode.Replace);
            float lx = L < 0 ? -23f : 3f, dx2 = L < 0 ? 8f : -28f;
            using (var hb = new SolidBrush(Al(Color.White, 40))) g.FillEllipse(hb, lx, -121f, 20f, 40f);
            using (var db = new SolidBrush(Al(Color.Black, 55))) g.FillEllipse(db, dx2, -118f, 20f, 56f);
            using (var ao = new LinearGradientBrush(new PointF(0, -84), new PointF(0, -58), Al(Color.Black, 0), Al(Color.Black, 90))) g.FillRectangle(ao, -26, -84, 52, 28);
            using (var fp = new Pen(Al(Color.Black, 38), 1.6f)) { g.DrawArc(fp, -18, -104, 30, 22, 20, 120); g.DrawArc(fp, -14, -92, 24, 16, 30, 110); }
            g.ResetClip();
            using var rim = new Pen(Al(Color.White, 100), 2f); float rx = L < 0 ? -24f : 24f; g.DrawLine(rim, rx, -108, rx, -72);
        }
        if (f.Name == "hoodie") { using var pk = new Pen(Al(Color.Black, 70), 1.6f); using (var pp2 = RR(-14, -88, 28, 15, 6)) g.DrawPath(pk, pp2); using var cord = new Pen(Al(Color.White, 170), 1.8f); g.DrawLine(cord, -5, -122, -6, -102); g.DrawLine(cord, 5, -122, 6, -102); }
        if (f.Tie) { using var lb = new LinearGradientBrush(new PointF(-7, -122), new PointF(7, -82), Color.FromArgb(225, 70, 90), Color.FromArgb(140, 20, 40)); g.FillPolygon(lb, new[] { new PointF(-5, -122), new PointF(5, -122), new PointF(7, -100), new PointF(0, -82), new PointF(-7, -100) }); }
        if (f.Scarf)
        {
            using var lb = new LinearGradientBrush(new PointF(-24, -130), new PointF(24, -117), Sh(f.ScarfC, 1.2f), Sh(f.ScarfC, .7f)); using var sp2 = RR(-24, -130, 48, 13, 6); g.FillPath(lb, sp2); g.FillRectangle(lb, 8, -120, 11, 30);
            using var ln = new Pen(Al(Sh(f.ScarfC, .5f), 120), 1.2f); g.DrawLine(ln, 10, -102, 17, -102); g.DrawLine(ln, 10, -94, 17, -94);
        }
        // neck and the shadow the chin throws on the shirt
        using (var nb = new SolidBrush(Sh(skin, .82f))) using (var np2 = RR(-7, -134, 14, 16, 5)) g.FillPath(nb, np2);
        using (var cs = new SolidBrush(Al(Color.Black, 55))) g.FillEllipse(cs, -13f, -128f, 26f, 9f);
        // arms (a carry pose hugs a box in front of the chest; bending down reaches toward the floor)
        float talkArm = 42f + MathF.Sin(t * 7f) * 16f; float fa, na; PointF h1, h2;
        bool carryPose = Held != null || bd > 0.001f;
        if (carryPose)
        {
            float ln2 = 26f + 18f * bd;
            h1 = Limb(g, 25, -112, ln2, 27f + 28f * bd, c1, 12, L); h2 = Limb(g, -25, -112, ln2, 9f + 46f * bd, c1, 12, L);
            if (Held != null)
            {
                float cx = 8f + 28f * bd, cy = -92f + 5f * bd;
                using (var sb = new SolidBrush(Al(Color.Black, 55))) using (var p0 = RR(cx - 27f, cy - 27f, 58, 62, 9)) g.FillPath(sb, p0);
                using (var p1 = RR(cx - 29f, cy - 31f, 58, 62, 9))
                {
                    using (var lb = new LinearGradientBrush(new PointF(cx - 29f, cy - 31f), new PointF(cx + 29f, cy + 31f), Color.FromArgb(252, 252, 255), Color.FromArgb(200, 208, 230))) g.FillPath(lb, p1);
                    using var pn = new Pen(Al(Color.White, 210), 1.5f); g.DrawPath(pn, p1);
                }
                var gs = g.Save(); g.TranslateTransform(cx, cy - 1f); if (flip) g.ScaleTransform(-1, 1); g.DrawImage(Held, -18f, -20f, 36f, 36f); g.Restore(gs);
            }
        }
        else
        {
            if (Push == 1) { fa = 82f; na = 76f; } else if (Push == 2) { fa = 162f; na = -162f; } else if (Push == 3) { fa = 10f; na = -10f; }
            else if (Talking) { fa = -talkArm; na = talkArm; } else { fa = sw * 32f; na = -sw * 32f; }
            h1 = Limb(g, 25, -112, 44, fa, c1, 12, L); h2 = Limb(g, -25, -112, 44, na, c1, 12, L);
        }
        Blob(g, h1.X, h1.Y, 6f, 5.4f, skin, L); Blob(g, h2.X, h2.Y, 6f, 5.4f, skin, L);
        // head: round and lit from the upper side, with ears, hair that has depth, and a proper face
        Blob(g, -25.2f, -150f, 4.3f, 6f, skin, L, 1.0f, .78f); Blob(g, 25.2f, -150f, 4.3f, 6f, skin, L, 1.0f, .78f);
        Blob(g, 0f, -151f, 25f, 27f, skin, L, 1.14f, .76f);
        using (var hp = new GraphicsPath())
        {
            hp.AddPie(-27, -179, 54, 54, 180, 180);
            using var hb = new LinearGradientBrush(new PointF(L * 22, -180), new PointF(-L * 22, -150), Sh(hair, 2.2f), Sh(hair, .75f)); g.FillPath(hb, hp);
            using var hl = new Pen(Al(Color.White, 80), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawArc(hl, -21, -176, 42, 30, L < 0 ? 195 : 285, 60);
        }
        if (f.Phones) { using (var pn = new Pen(Theme.Accent, 6)) g.DrawArc(pn, -31, -182, 62, 62, 180, 180); Blob(g, -29.5f, -147f, 5.5f, 11f, Theme.Accent, L); Blob(g, 29.5f, -147f, 5.5f, 11f, Theme.Accent, L); }
        if (f.Hat == "cap") { using var b = new SolidBrush(f.HatC); g.FillPie(b, -29, -182, 58, 54, 180, 180); g.FillEllipse(b, 8, -162, 36, 9); using var hl = new Pen(Al(Color.White, 90), 2f); g.DrawArc(hl, -22, -178, 44, 34, L < 0 ? 200 : 280, 55); }
        else if (f.Hat == "beanie") { using var b = new SolidBrush(f.HatC); g.FillPie(b, -29, -190, 58, 60, 180, 180); using var b2 = new SolidBrush(Color.FromArgb(235, 235, 240)); g.FillRectangle(b2, -29, -161, 58, 9); Blob(g, 0f, -190f, 7f, 7f, Color.FromArgb(235, 235, 240), L); }
        else if (f.Hat == "crown") { using var lb = new LinearGradientBrush(new PointF(-22, -204), new PointF(22, -174), Color.FromArgb(255, 225, 100), Color.FromArgb(200, 140, 20)); g.FillPolygon(lb, new[] { new PointF(-20, -174), new PointF(-22, -200), new PointF(-10, -186), new PointF(0, -204), new PointF(10, -186), new PointF(22, -200), new PointF(20, -174) }); }
        if (f.Mask) { using var b = new SolidBrush(f.MaskC); using var mp = RR(-27, -162, 54, 17, 8); g.FillPath(b, mp); }
        bool blink = DateTime.Now < blinkEnd;
        void Eye(float ecx)
        {
            if (blink) { using var lp = new Pen(dark, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(lp, ecx - 4.5f, -149f, ecx + 4.5f, -149f); return; }
            using (var wb = new SolidBrush(Color.FromArgb(250, 250, 252))) g.FillEllipse(wb, ecx - 4.8f, -156f, 9.6f, 11.5f);
            using (var op = new Pen(Al(Sh(skin, .6f), 200), 1.2f)) g.DrawEllipse(op, ecx - 4.8f, -156f, 9.6f, 11.5f);
            Blob(g, ecx + 0.9f, -150f, 3.4f, 3.9f, Color.FromArgb(84, 58, 40), L, 1.5f, .8f);
            using (var pu = new SolidBrush(Color.FromArgb(12, 10, 14))) g.FillEllipse(pu, ecx - 0.9f, -152f, 3.6f, 4.2f);
            using (var sp3 = new SolidBrush(Color.White)) g.FillEllipse(sp3, ecx - 0.4f, -153.5f, 1.8f, 1.8f);
            using (var lid = new Pen(dark, 1.6f)) g.DrawArc(lid, ecx - 5f, -157f, 10f, 9f, 200, 140);
        }
        Eye(-5f); Eye(12f);
        if (!f.Mask)
        {
            using var bp = new Pen(Sh(hair, 1.1f), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; float a1 = Push > 0 ? 1.8f : 0f;
            g.DrawLine(bp, -10f, -160.5f + a1, 0f, -161.5f - a1 * 0.6f); g.DrawLine(bp, 7f, -161.5f - a1 * 0.6f, 17f, -160.5f + a1);
        }
        if (f.Glasses) { using var gp = new Pen(dark, 2); if (f.Sun) { using var sb = new SolidBrush(Color.FromArgb(200, 20, 20, 30)); g.FillEllipse(sb, -14, -158, 16, 15); g.FillEllipse(sb, 6, -158, 16, 15); using var shn = new Pen(Al(Color.White, 90), 1.6f); g.DrawArc(shn, -11, -156, 10, 10, 200, 70); g.DrawArc(shn, 9, -156, 10, 10, 200, 70); } g.DrawEllipse(gp, -14, -158, 16, 15); g.DrawEllipse(gp, 6, -158, 16, 15); g.DrawLine(gp, 2, -151, 6, -151); }
        Blob(g, 7.5f, -141.5f, 3.4f, 2.8f, skin, L, 1.22f, .85f);
        using (var ck = new SolidBrush(Color.FromArgb(46, 255, 110, 100))) { g.FillEllipse(ck, -9f, -141f, 10f, 6f); g.FillEllipse(ck, 13f, -141f, 10f, 6f); }
        using (var pn = new Pen(Color.FromArgb(150, 72, 76), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (Push > 0) g.DrawLine(pn, 0, -135, 12, -135);
            else if (Talking) { float h = 4 + MathF.Abs(MathF.Sin(t * 14f)) * 8f; using var mb = new SolidBrush(Color.FromArgb(96, 30, 42)); g.FillEllipse(mb, 0, -137, 12, h); }
            else g.DrawArc(pn, -1, -143, 16, 12, 20, 140);
        }
    }
}

class PetWin : Form
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLEND pblend, int dwFlags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    public PetWin() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Size = new Size(10, 10); }
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x08000000; return cp; } }   // layered, tool window, no-activate
    protected override bool ShowWithoutActivation => true;
    // draws a 32-bit bitmap with real per-pixel transparency: see-through everywhere the picture is empty, and clicks pass through those parts
    public void Present(Bitmap bmp, Point loc)
    {
        if (!IsHandleCreated) return; IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), hb = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            hb = bmp.GetHbitmap(Color.FromArgb(0)); old = SelectObject(mem, hb); var size = new Size(bmp.Width, bmp.Height); var src = new Point(0, 0); var dst = loc; var bf = new BLEND { Op = 0, Alpha = 255, Format = 1 };
            UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bf, 2);
        }
        finally { ReleaseDC(IntPtr.Zero, screen); if (hb != IntPtr.Zero) { SelectObject(mem, old); DeleteObject(hb); } DeleteDC(mem); }
    }
}

static class Pet
{
    static DesktopForm? sh; static Panel? desk; static PetSprite? sp; static PetWin? win; static Control? bar; static TextBox? tb; static Bitmap? bmp;
    static readonly Random rnd = new(); static System.Windows.Forms.Timer? tm; static readonly Font bf = Ui.F(10.5f);
    static DateTime last = DateTime.Now, idleUntil = DateTime.Now.AddSeconds(2), pausedUntil, hideBubbleAt = DateTime.MinValue, barUntil;
    static PointF pos; static PointF? target; static bool busy, speaking, dragging, down, bubbleOn, bubbleTrunc; static int speechId; static Point dn; static PointF startPos;
    static string bubbleText = ""; static Rectangle bubbleRect;

    public static readonly Outfit[] Outfits =
    {
        new() { Name = "hoodie", Phones = true },
        new() { Name = "school", T1 = Color.FromArgb(240, 240, 246), T2 = Color.FromArgb(214, 220, 236), Pants = Color.FromArgb(35, 45, 92), Shoes = Color.FromArgb(40, 40, 48), Tie = true, Pack = true },
        new() { Name = "hero", T1 = Color.FromArgb(214, 40, 60), T2 = Color.FromArgb(150, 20, 46), Pants = Color.FromArgb(30, 70, 170), Shoes = Color.FromArgb(200, 40, 50), Cape = true, CapeC = Color.FromArgb(190, 30, 50), Mask = true, MaskC = Color.FromArgb(30, 40, 110) },
        new() { Name = "winter", T1 = Color.FromArgb(122, 132, 152), T2 = Color.FromArgb(82, 92, 112), Pants = Color.FromArgb(50, 50, 62), Shoes = Color.FromArgb(112, 82, 62), Hat = "beanie", HatC = Color.FromArgb(200, 60, 70), Scarf = true, ScarfC = Color.FromArgb(240, 200, 60) },
        new() { Name = "formal", T1 = Color.FromArgb(38, 38, 50), T2 = Color.FromArgb(22, 22, 30), Pants = Color.FromArgb(30, 30, 40), Shoes = Color.FromArgb(25, 25, 30), Tie = true, Glasses = true },
        new() { Name = "sporty", T1 = Color.FromArgb(40, 170, 90), T2 = Color.FromArgb(20, 120, 60), Pants = Color.FromArgb(40, 150, 85), Shoes = Color.FromArgb(250, 250, 250), Shorts = true, Hat = "cap", HatC = Color.FromArgb(250, 250, 250) },
        new() { Name = "royal", T1 = Color.FromArgb(112, 52, 172), T2 = Color.FromArgb(72, 30, 122), Pants = Color.FromArgb(62, 32, 102), Shoes = Color.FromArgb(240, 200, 60), Hat = "crown", Cape = true, CapeC = Color.FromArgb(160, 40, 60) },
        new() { Name = "pajamas", T1 = Color.FromArgb(150, 190, 240), T2 = Color.FromArgb(112, 152, 212), Pants = Color.FromArgb(130, 170, 230), Shoes = Color.FromArgb(245, 245, 250), Hat = "beanie", HatC = Color.FromArgb(150, 190, 240) },
        new() { Name = "summer", T1 = Color.FromArgb(250, 212, 82), T2 = Color.FromArgb(240, 170, 50), Pants = Color.FromArgb(40, 110, 200), Shoes = Color.FromArgb(250, 250, 250), Shorts = true, Hat = "cap", HatC = Color.FromArgb(255, 120, 90), Glasses = true, Sun = true },
    };

    public static string SetOutfit(string a)
    {
        a = a.Trim().ToLowerInvariant(); string names = string.Join(", ", Outfits.Select(o => o.Name));
        if (a == "" || a == "list") return "Outfits: " + names + ", random";
        var o = a == "random" ? Outfits[rnd.Next(Outfits.Length)] : Outfits.FirstOrDefault(x => x.Name == a) ?? Outfits.FirstOrDefault(x => x.Name.Contains(a) || a.Contains(x.Name));
        if (o == null) return "ERR: I don't have an outfit called " + a + ". Outfits: " + names;
        Cfg.Set("pet_outfit", o.Name); if (sp != null) sp.Fit = o; return "";
    }

    // ---- pushing icons ----
    class PushJob { public DeskIcon Icon = null!; public PointF IconPos; public List<Point> Way = new(); public int Leg; public bool Approach = true; public Point Dest; public DateTime Started = DateTime.Now; }
    static PushJob? job;

    public static string Push(DesktopForm s, string a)
    {
        var i = a.IndexOf('|'); if (i <= 0) return "ERR: push needs an icon name and a place, like Games|topright";
        var (icon, dest, err) = s.PlanPush(a[..i], a[(i + 1)..]); if (icon == null) return err.StartsWith("It") ? err : "ERR: " + err;
        if (sp == null || Cfg.Get("pet", "1") != "1") { s.FinishPush(icon, dest); return "Moved " + icon.Label + " (the character is hidden, so there was no animation)."; }
        if (job != null || cj != null) return "ERR: the character is still busy.";
        var p0 = icon.Location; var way = new List<Point>(); bool hf = Math.Abs(dest.X - p0.X) >= Math.Abs(dest.Y - p0.Y);
        var mid = hf ? new Point(dest.X, p0.Y) : new Point(p0.X, dest.Y); if (mid != p0) way.Add(mid); if (dest != mid) way.Add(dest);
        job = new PushJob { Icon = icon, IconPos = p0, Way = way, Dest = dest };
        return win!.Visible ? "The character is pushing " + icon.Label + " now." : "Queued: close or minimize the chat window to watch the character push " + icon.Label + ".";
    }

    // ---- carrying icons: walk over, squat, pick the icon up like a box, walk it to its place, squat, put it down ----
    class CarryJob { public List<(DeskIcon icon, Point dest)> Moves = new(); public int I, Phase, Side = 1; public float T; public bool Held, Setup; public PointF Stand, Hand, From; public DateTime Started = DateTime.Now; }
    static CarryJob? cj;
    const float ReachX = 44f, ReachY = 36f;   // where the hands meet the box when squatting, in character units (matches the squat pose in PetSprite.Draw)

    public static string Carry(DesktopForm s, string a)
    {
        var i = a.IndexOf('|'); if (i <= 0) return "ERR: carry needs an icon name and a place, like Games|topright";
        var (icon, dest, err) = s.PlanPush(a[..i], a[(i + 1)..]); if (icon == null) return err.StartsWith("It") ? err : "ERR: " + err;
        return StartCarry(s, new List<(DeskIcon icon, Point dest)> { (icon, dest) }, "carrying " + icon.Label);
    }

    public static string SortIcons(DesktopForm s, string a)
    {
        bool desc = a.Trim().ToLowerInvariant() is "za" or "z-a" or "z to a" or "desc" or "descending" or "reverse";
        var (moves, err) = s.PlanSort(desc); if (err != "") return "ERR: " + err; if (moves.Count == 0) return "It's already in order.";
        return StartCarry(s, moves, desc ? "sorting the icons Z to A" : "sorting the icons A to Z");
    }

    static string StartCarry(DesktopForm s, List<(DeskIcon icon, Point dest)> moves, string what)
    {
        if (sp == null || Cfg.Get("pet", "1") != "1") { foreach (var (ic, d) in moves) s.FinishPush(ic, d); return "Done (the character is hidden, so there was no animation)."; }
        if (job != null || cj != null) return "ERR: the character is still busy.";
        cj = new CarryJob { Moves = moves };
        return win!.Visible ? "The character is " + what + " now." : "Queued: close or minimize the chat window to watch the character " + what + ".";
    }

    static float Ease(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }
    static float Scale() => Math.Min(sp!.Width / 110f, sp.Height / 195f);

    // where the character must stand (and where its hands end up) to reach an icon sitting in rectangle r, standing on the given side of it
    static (PointF pos, PointF hand) Reach(Rectangle r, int side)
    {
        float s = Scale(), W = sp!.Width, H = sp.Height, cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
        float ox = Math.Clamp(cx - side * ReachX * s, W / 2f, Math.Max(W / 2f, desk!.Width - W / 2f));
        float top = 206 * s + 4, oy = Math.Clamp(cy + ReachY * s, top, Math.Max(top, desk.Height - 6 * s));   // keep the whole character on the desktop
        return (new PointF(ox - W / 2f, oy - H + 8 * s), new PointF(ox + side * ReachX * s, oy - ReachY * s));
    }

    static bool WalkTo(PointF to, int speed, float dt)
    {
        float dx = to.X - pos.X, dy = to.Y - pos.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 3f) { pos = to; sp!.Moving = false; return true; }
        float st = Math.Min(d, Ui.D(speed) * dt); pos = new PointF(pos.X + dx / d * st, pos.Y + dy / d * st); sp!.Moving = true; if (Math.Abs(dx) > 2f) sp.Right = dx > 0; return false;
    }

    static Point CenterAt(PointF c, Size z) => new Point((int)Math.Round(c.X - z.Width / 2f), (int)Math.Round(c.Y - z.Height / 2f));

    static void EndCarry(bool snap)
    {
        var c = cj; cj = null; if (c == null) return;
        if (sp != null) { sp.Bend = 0; sp.Held = null; sp.Moving = false; sp.Push = 0; }
        if (snap && sh != null) for (int i = c.I; i < c.Moves.Count; i++) { var (ic, d) = c.Moves[i]; if (ic.IsDisposed) continue; ic.Visible = true; sh.FinishPush(ic, d); }
        idleUntil = DateTime.Now.AddSeconds(3); target = null;
    }

    static void RunCarry(float dt, DateTime now)
    {
        var c = cj!; if (sp == null || sh == null || desk == null) return;
        if (c.I >= c.Moves.Count) { EndCarry(false); ShowBubble("All done!", 4); return; }
        var (ic, dest) = c.Moves[c.I];
        if (ic.IsDisposed || ic.Parent == null || (now - c.Started).TotalSeconds > 600 || dragging) { EndCarry(true); return; }
        float s = Scale(); sp.Push = 0;
        switch (c.Phase)
        {
            case 0:   // walk to the icon
                if (!c.Setup) { var r = ic.Bounds; float cx = r.Left + r.Width / 2f; c.Side = cx - ReachX * s >= sp.Width / 2f ? 1 : -1; (c.Stand, c.Hand) = Reach(r, c.Side); c.Setup = true; }
                if (WalkTo(c.Stand, 150, dt)) { sp.Right = c.Side > 0; c.Phase = 1; c.T = 0; c.Setup = false; }
                break;
            case 1:   // squat down
                c.T += dt / 0.45f; sp.Bend = Ease(c.T); if (c.T >= 1f) { c.Phase = 2; c.T = 0; c.Setup = false; }
                break;
            case 2:   // grab it: the icon slides into the hands
                if (!c.Setup) { c.From = new PointF(ic.Left + ic.Width / 2f, ic.Top + ic.Height / 2f); c.Setup = true; }
                c.T += dt / 0.3f; { float e = Ease(c.T); ic.Location = CenterAt(new PointF(c.From.X + (c.Hand.X - c.From.X) * e, c.From.Y + (c.Hand.Y - c.From.Y) * e), ic.Size); }
                if (c.T >= 1f) { ic.Visible = false; sp.Held = ic.Img; c.Held = true; c.Phase = 3; c.T = 0; c.Setup = false; }
                break;
            case 3:   // stand up holding it
                c.T += dt / 0.5f; sp.Bend = 1f - Ease(c.T); if (c.T >= 1f) { sp.Bend = 0; c.Phase = 4; c.T = 0; c.Setup = false; }
                break;
            case 4:   // walk to the new spot carrying it
                if (!c.Setup) { var r = new Rectangle(dest, ic.Size); float cx = r.Left + r.Width / 2f; c.Side = cx >= pos.X + sp.Width / 2f ? 1 : -1; (c.Stand, c.Hand) = Reach(r, c.Side); c.Setup = true; }
                if (WalkTo(c.Stand, 150, dt)) { sp.Right = c.Side > 0; c.Phase = 5; c.T = 0; c.Setup = false; }
                break;
            case 5:   // squat down with it
                c.T += dt / 0.5f; sp.Bend = Ease(c.T); if (c.T >= 1f) { c.Phase = 6; c.T = 0; c.Setup = false; }
                break;
            case 6:   // put it down and slide it into its place
                if (!c.Setup) { ic.Location = CenterAt(c.Hand, ic.Size); ic.Visible = true; sp.Held = null; c.Held = false; c.From = c.Hand; c.Setup = true; }
                c.T += dt / 0.3f; { float e = Ease(c.T); var tc = new PointF(dest.X + ic.Width / 2f, dest.Y + ic.Height / 2f); ic.Location = CenterAt(new PointF(c.From.X + (tc.X - c.From.X) * e, c.From.Y + (tc.Y - c.From.Y) * e), ic.Size); }
                if (c.T >= 1f) { sh.FinishPush(ic, dest); c.Phase = 7; c.T = 0; c.Setup = false; }
                break;
            default:  // stand back up, then the next icon
                c.T += dt / 0.4f; sp.Bend = 1f - Ease(c.T); if (c.T >= 1f) { sp.Bend = 0; c.I++; c.Phase = 0; c.T = 0; c.Setup = false; }
                break;
        }
    }

    static PointF ApproachPos(Rectangle r, int pose, int sign)
    {
        float s = Math.Min(sp!.Width / 110f, sp.Height / 195f), W = sp.Width, H = sp.Height;
        if (pose == 1) { float hx = 83 * s; return new PointF(sign > 0 ? r.Left - W / 2 - hx + 2 : r.Right + hx - W / 2 - 2, r.Top + r.Height * 0.5f - H + 99 * s); }
        if (pose == 2) return new PointF(r.Left + r.Width / 2f - W / 2, r.Bottom + 163 * s - H);
        return new PointF(r.Left + r.Width / 2f - W / 2, r.Top + 76 * s - H);
    }

    static void EndJob(PushJob j, bool snap)
    {
        if (snap && sh != null && !j.Icon.IsDisposed) sh.FinishPush(j.Icon, j.Dest);
        job = null; if (sp != null) { sp.Push = 0; sp.Moving = false; } idleUntil = DateTime.Now.AddSeconds(3); target = null;
    }

    static void RunJob(float dt, DateTime now)
    {
        var j = job!; if (sp == null || sh == null) return;
        if (j.Icon.IsDisposed || j.Icon.Parent == null || (now - j.Started).TotalSeconds > 120 || dragging) { EndJob(j, true); return; }
        var tg = j.Way[j.Leg]; var cur = j.IconPos; float dx = tg.X - cur.X, dy = tg.Y - cur.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
        int pose = Math.Abs(dx) >= Math.Abs(dy) ? 1 : (dy < 0 ? 2 : 3), sign = dx >= 0 ? 1 : -1;
        var want = ApproachPos(new Rectangle(Point.Round(cur), j.Icon.Size), pose, sign);
        if (j.Approach)
        {
            sp.Push = 0; float ax = want.X - pos.X, ay = want.Y - pos.Y, ad = MathF.Sqrt(ax * ax + ay * ay);
            if (ad < 3) { j.Approach = false; pos = want; sp.Push = pose; sp.Right = sign > 0; }
            else { float st = Math.Min(ad, Ui.D(110) * dt); pos = new PointF(pos.X + ax / ad * st, pos.Y + ay / ad * st); sp.Moving = true; if (Math.Abs(ax) > 2) sp.Right = ax > 0; }
            return;
        }
        sp.Push = pose; sp.Right = sign > 0; sp.Moving = true;
        float step = Math.Min(dist, Ui.D(46) * dt); float ux = dist < 0.01f ? 0 : dx / dist, uy = dist < 0.01f ? 0 : dy / dist;
        j.IconPos = new PointF(cur.X + ux * step, cur.Y + uy * step); j.Icon.Location = Point.Round(j.IconPos);
        pos = ApproachPos(new Rectangle(Point.Round(j.IconPos), j.Icon.Size), pose, sign);
        if (dist - step <= 0.5f)
        {
            j.IconPos = tg; j.Icon.Location = tg; j.Leg++; j.Approach = true; sp.Push = 0;
            if (j.Leg >= j.Way.Count) { EndJob(j, true); ShowBubble("Done!", 3); }
        }
    }

    public static void Attach(DesktopForm s, Panel d)
    {
        if (sp != null) return; sh = s; desk = d;
        sp = new PetSprite { Width = Ui.D(150), Height = Ui.D(170) }; sp.Fit = Outfits.FirstOrDefault(o => o.Name == Cfg.Get("pet_outfit", "hoodie")) ?? Outfits[0];
        win = new PetWin(); win.CreateControl(); var _h = win.Handle;
        var (box, t) = Ui.Input("Talk to " + Cfg.Get("ai_name", "Morales") + "…", false, 38); bar = box; tb = t; bar.Width = Ui.D(260); bar.Height = Ui.D(38); bar.Visible = false; d.Controls.Add(bar);
        pos = new PointF(Ui.D(320), Math.Max(Ui.D(60), d.Height - sp.Height - Ui.D(140)));
        var menu = new ContextMenuStrip(); menu.Items.Add("Talk to me", null, (_, _) => ToggleBar());
        var cm = new ToolStripMenuItem("Change clothes"); foreach (var o in Outfits) { var n = o.Name; cm.DropDownItems.Add(char.ToUpper(n[0]) + n[1..], null, (_, _) => SetOutfit(n)); }
        menu.Items.Add(cm); menu.Items.Add("Hide the character", null, (_, _) => Cfg.Set("pet", "0")); win.ContextMenuStrip = menu;
        win.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (bubbleOn && bubbleRect.Contains(e.Location)) { if (bubbleTrunc && sh != null) { sh.Launch("ai"); } bubbleOn = false; return; }
            down = true; dragging = false; dn = Cursor.Position; startPos = pos;
        };
        win.MouseMove += (_, e) =>
        {
            if (!down) return; var c = Cursor.Position; int dx = c.X - dn.X, dy = c.Y - dn.Y;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) > 6) { dragging = true; target = null; }
            if (dragging) pos = Clamp(new PointF(startPos.X + dx, startPos.Y + dy));
        };
        win.MouseUp += (_, e) => { if (e.Button != MouseButtons.Left || !down) return; bool was = dragging; down = false; dragging = false; pausedUntil = DateTime.Now.AddSeconds(5); if (!was) { if (cj != null) { EndCarry(true); ShowBubble("Okay, finishing it up now!", 3); } else ToggleBar(); } };
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Ask(tb.Text); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) { bar!.Visible = false; e.SuppressKeyPress = true; } };
        tb.TextChanged += (_, _) => barUntil = DateTime.Now.AddSeconds(15);
        Assistant.Replied += (text, fromPet) => { if (!fromPet && win != null && win.Visible) ShowBubble(text, BubbleSecs(text)); };
        tm = new System.Windows.Forms.Timer { Interval = 40 }; tm.Tick += (_, _) => Tick(); tm.Start();
    }

    public static void Command(string a)
    {
        a = a.Trim().ToLowerInvariant();
        if (a == "off") Cfg.Set("pet", "0");
        else { Cfg.Set("pet", "1"); if (a == "come" && desk != null && sp != null) { pausedUntil = default; target = Clamp(new PointF(desk.Width / 2f - sp.Width / 2f, desk.Height / 2f)); } }
    }

    static PointF Clamp(PointF p) => desk == null || sp == null ? p : new PointF(Math.Clamp(p.X, 0, Math.Max(0, desk.Width - sp.Width)), Math.Clamp(p.Y, Ui.D(20), Math.Max(Ui.D(20), desk.Height - sp.Height - Ui.D(54))));
    static double BubbleSecs(string t) => Math.Clamp(10 + t.Length * 0.1, 12, 90);

    static void Tick()
    {
        if (sh == null || desk == null || sp == null || win == null || bar == null || tb == null) return;
        var now = DateTime.Now; float dt = Math.Min(0.2f, (float)(now - last).TotalSeconds); last = now;
        bool show = Cfg.Get("pet", "1") == "1" && desk.Visible && sh.Visible && sh.WindowState != FormWindowState.Minimized && !sh.AssistantOpen();
        if (cj != null && (cj.Held || Cfg.Get("pet", "1") != "1")) EndCarry(true);   // never leave an icon invisible if the character goes away
        if (!show) { if (win.Visible) { win.Hide(); bar.Visible = false; } return; }
        if (!win.Visible) { win.Show(sh); pos = Clamp(pos); }
        if (job != null) { RunJob(dt, now); }
        else if (cj != null) { RunCarry(dt, now); }
        else
        {
            bool still = dragging || speaking || busy || bar.Visible || now < pausedUntil;
            if (still) { sp.Moving = false; if (!dragging) target = null; }
            else if (target == null) { sp.Moving = false; if (now >= idleUntil) { var a = rnd.NextDouble() * Math.PI * 2; float r = Ui.D(150) + (float)rnd.NextDouble() * Ui.D(300); target = Clamp(new PointF(pos.X + MathF.Cos((float)a) * r, pos.Y + MathF.Sin((float)a) * r * 0.6f)); } }
            else
            {
                float dx = target.Value.X - pos.X, dy = target.Value.Y - pos.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < 4) { target = null; idleUntil = now.AddSeconds(2.5 + rnd.NextDouble() * 5); sp.Moving = false; }
                else { float st = Math.Min(dist, Ui.D(70) * dt); pos = new PointF(pos.X + dx / dist * st, pos.Y + dy / dist * st); sp.Moving = true; if (Math.Abs(dx) > 2) sp.Right = dx > 0; }
            }
            pos = Clamp(pos);
        }
        sp.Step(dt);
        if (bubbleOn)
        {
            var c = win.PointToClient(Cursor.Position); if (bubbleRect.Contains(c)) hideBubbleAt = now.AddSeconds(8);
            if (now >= hideBubbleAt && !speaking && !busy) bubbleOn = false;
        }
        if (bar.Visible)
        {
            float s = Math.Min(sp.Width / 110f, sp.Height / 195f);
            bar.Location = new Point(Math.Clamp((int)(pos.X + sp.Width / 2f - bar.Width / 2f), Ui.D(4), Math.Max(Ui.D(4), desk.Width - bar.Width - Ui.D(4))), (int)(pos.Y + sp.Height + Ui.D(2)));
            if (!busy && now > barUntil && tb.Text == "") bar.Visible = false;
        }
        Render();
    }

    static void Render()
    {
        if (sp == null || win == null || desk == null) return;
        int FW = Ui.D(320), AH = Ui.D(170), FH = AH + sp.Height;
        if (bmp == null || bmp.Width != FW || bmp.Height != FH) { bmp?.Dispose(); bmp = new Bitmap(FW, FH, System.Drawing.Imaging.PixelFormat.Format32bppArgb); }
        var loc = desk.PointToScreen(new Point((int)pos.X - (FW - sp.Width) / 2, (int)pos.Y - AH));
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var st = g.Save(); g.TranslateTransform((FW - sp.Width) / 2f, AH); sp.Draw(g); g.Restore(st);
            DrawBubble(g, FW, AH);
            // make it look like it is on the desktop: erase everything outside the desktop area and anything covered by an open window
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            var dr = desk.RectangleToScreen(desk.ClientRectangle); dr.Offset(-loc.X, -loc.Y);
            using (var outside = new Region(new Rectangle(0, 0, FW, FH))) { outside.Exclude(dr); g.SetClip(outside, System.Drawing.Drawing2D.CombineMode.Replace); g.Clear(Color.Transparent); }
            foreach (var w in desk.Controls.OfType<Win>().Where(x => x.Visible)) { var r = desk.RectangleToScreen(w.Bounds); r.Offset(-loc.X, -loc.Y); g.SetClip(r, System.Drawing.Drawing2D.CombineMode.Replace); g.Clear(Color.Transparent); }
        }
        win.Present(bmp, loc);
    }

    static void DrawBubble(Graphics g, int FW, int AH)
    {
        if (!bubbleOn || bubbleText == "") { bubbleRect = Rectangle.Empty; return; }
        int pad = Ui.D(12), tail = Ui.D(10), maxW = Ui.D(260), maxH = AH - tail - Ui.D(14); var text = bubbleText;
        var sz = g.MeasureString(text, bf, new SizeF(maxW, 5000)); float tw = Math.Min(maxW, (float)Math.Ceiling(sz.Width)), th = Math.Min(maxH - pad * 2, (float)Math.Ceiling(sz.Height)); bubbleTrunc = sz.Height > th + 2 || text.EndsWith("…");
        int w = (int)tw + pad * 2 + 2, h = (int)th + pad * 2; int x = Math.Clamp(FW / 2 - w / 2, Ui.D(4), FW - w - Ui.D(4)), y = AH - tail - Ui.D(6) - h; if (y < 0) y = 0;
        bubbleRect = new Rectangle(x, y, w, h + tail);
        var rect = new Rectangle(x, y, w, h);
        using (var path = U.Round(rect, Ui.D(14)))
        {
            using (var sb = new SolidBrush(Color.FromArgb(55, 0, 0, 0))) { var r2 = rect; r2.Offset(2, 3); using var p2 = U.Round(r2, Ui.D(14)); g.FillPath(sb, p2); }
            using var wb = new SolidBrush(Color.White); g.FillPath(wb, path);
            int cx = Math.Clamp(FW / 2, x + Ui.D(18), x + w - Ui.D(18)); g.FillPolygon(wb, new[] { new Point(cx - Ui.D(8), y + h - 1), new Point(cx + Ui.D(6), y + h + tail), new Point(cx + Ui.D(8), y + h - 1) });
        }
        using var tbru = new SolidBrush(Color.FromArgb(30, 30, 44)); using var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord };
        g.DrawString(text + (bubbleTrunc ? "\n(click to read more)" : ""), bf, tbru, new RectangleF(x + pad, y + pad, tw + 2, th), sf);
    }

    static void ToggleBar()
    {
        if (bar == null || tb == null) return; if (bar.Visible) { bar.Visible = false; return; }
        bar.Visible = true; bar.BringToFront(); tb.Focus(); barUntil = DateTime.Now.AddSeconds(15);
    }

    static void ShowBubble(string t, double secs)
    {
        if (t.Length > 400) t = t[..400] + "…"; bubbleText = t; bubbleOn = true; hideBubbleAt = DateTime.Now.AddSeconds(secs);
    }

    static async void Ask(string text)
    {
        text = text.Trim(); if (text == "" || busy || sh == null || tb == null) return;
        busy = true; tb.Text = ""; ShowBubble("…", 120);
        try { var r = await Assistant.Converse(sh, text, true); Speak(r); }
        catch (Exception ex) { ShowBubble("Hmm: " + ex.Message, 12); }
        finally { busy = false; barUntil = DateTime.Now.AddSeconds(12); }
    }

    static void Speak(string text)
    {
        ShowBubble(text, BubbleSecs(text)); if (Cfg.Get("pet_voice", "1") != "1" || sp == null) return;
        int id = ++speechId; speaking = true; sp.Talking = true;
        Assistant.Say(text, () => { try { desk!.BeginInvoke(new Action(() => { if (id != speechId) return; speaking = false; if (sp != null) sp.Talking = false; hideBubbleAt = DateTime.Now.AddSeconds(8); })); } catch { } },
            err => { try { desk!.BeginInvoke(new Action(() => sh?.Toast("Voice problem: " + err))); } catch { } });
    }
}
