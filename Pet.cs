using System.Drawing.Drawing2D;
namespace UsbDesktop;

// The walking buddy: a small animated character that wanders the desktop when the assistant window is closed.
// Click it for a small chat bar at its feet; replies appear in a white speech bubble and are spoken out loud.
class Outfit
{
    public string Name = ""; public Color T1 = Color.Empty, T2 = Color.Empty, Pants = Color.FromArgb(50, 62, 112), Shoes = Color.FromArgb(240, 240, 245), HatC = Color.Gray, CapeC = Color.Crimson, MaskC = Color.Navy, ScarfC = Color.Gold, PackC = Color.FromArgb(230, 120, 40);
    public string Hat = ""; public bool Glasses, Sun, Cape, Mask, Scarf, Tie, Pack, Shorts, Phones;
}

class PetSprite : Control
{
    public bool Right = true, Moving, Talking; public int Push; public Outfit Fit = Pet.Outfits[0];
    float phase, dustT; DateTime nextBlink = DateTime.Now.AddSeconds(2), blinkEnd; static readonly Random R = new();
    class Puff { public float X, Y, Age; } readonly List<Puff> dust = new();
    public PetSprite() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.Transparent; Cursor = Cursors.Hand; Visible = false; }
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
    static void Limb(Graphics g, float x, float y, float len, float deg, Color c, float w, Color? tip = null, float tr = 0)
    {
        float r = deg * MathF.PI / 180f, ex = x + MathF.Sin(r) * len, ey = y + MathF.Cos(r) * len;
        using var p = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(p, x, y, ex, ey);
        if (tip != null) { using var b = new SolidBrush(tip.Value); g.FillEllipse(b, ex - tr, ey - tr * 0.7f, tr * 2.3f, tr * 1.4f); }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float s = Math.Min(Width / 110f, Height / 195f); float t = Environment.TickCount / 1000f; var f = Fit;
        foreach (var d in dust) { using var b = new SolidBrush(Color.FromArgb((int)(120 * (1 - d.Age)), 215, 205, 195)); float r = (6 + d.Age * 16) * s; g.FillEllipse(b, d.X - r, d.Y - r * 0.6f, r * 2, r * 1.2f); }
        g.TranslateTransform(Width / 2f, Height - 8 * s); if (!Right && Push < 2) g.ScaleTransform(-1, 1); g.ScaleTransform(s, s);
        float sw = Moving ? MathF.Sin(phase) : 0f, bob = Moving ? -MathF.Abs(MathF.Cos(phase)) * (Push > 0 ? 2f : 4f) : MathF.Sin(t * 2f) * -1.5f;
        var skin = Color.FromArgb(224, 172, 130); var hair = Color.FromArgb(40, 30, 32); var c1 = f.T1.IsEmpty ? Theme.Accent : f.T1; var c2 = f.T2.IsEmpty ? Ui.Accent2 : f.T2; var dark = Color.FromArgb(30, 24, 28);
        using (var b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, -32, -7, 64, 14);
        g.TranslateTransform(0, bob); if (Push == 1) g.RotateTransform(10); else if (Push == 3) g.RotateTransform(12);
        if (f.Cape) { float sway = Moving ? MathF.Sin(phase + 1f) * 8f : MathF.Sin(t * 2f) * 3f; using var b = new SolidBrush(f.CapeC); g.FillPolygon(b, new[] { new PointF(-22, -118), new PointF(18, -118), new PointF(-6 - sway, -38), new PointF(-48 - sway, -50) }); }
        if (f.Pack) { using var b = new SolidBrush(f.PackC); using var pp = U.Round(new Rectangle(-42, -118, 22, 48), 8); g.FillPath(b, pp); }
        float la = Push > 0 ? 24f : 30f;
        void Leg(float x, float deg, Color shoe)
        {
            if (f.Shorts) { Limb(g, x, -62, 26, deg, f.Pants, 16); float r = deg * MathF.PI / 180f; Limb(g, x + MathF.Sin(r) * 26, -62 + MathF.Cos(r) * 26, 28, deg, skin, 11, shoe, 9); }
            else Limb(g, x, -62, 54, deg, f.Pants, 16, shoe, 9);
        }
        Leg(8, -sw * la, Color.FromArgb(Math.Max(0, f.Shoes.R - 12), Math.Max(0, f.Shoes.G - 12), Math.Max(0, f.Shoes.B - 12))); Leg(-8, sw * la, f.Shoes);
        var body = new Rectangle(-25, -124, 50, 66);
        using (var path = U.Round(body, 16)) using (var lg = new LinearGradientBrush(body, c1, c2, 80f)) g.FillPath(lg, path);
        using (var pk = new Pen(Color.FromArgb(70, 255, 255, 255), 2)) g.DrawLine(pk, -12, -80, 12, -80);
        if (f.Tie) { using var b = new SolidBrush(Color.FromArgb(200, 40, 60)); g.FillPolygon(b, new[] { new PointF(-5, -122), new PointF(5, -122), new PointF(7, -100), new PointF(0, -82), new PointF(-7, -100) }); }
        if (f.Scarf) { using var b = new SolidBrush(f.ScarfC); using var sp2 = U.Round(new Rectangle(-24, -130, 48, 13), 6); g.FillPath(b, sp2); g.FillRectangle(b, 8, -120, 11, 30); }
        float talkArm = 42f + MathF.Sin(t * 7f) * 16f; float fa, na;
        if (Push == 1) { fa = 82f; na = 76f; } else if (Push == 2) { fa = 162f; na = -162f; } else if (Push == 3) { fa = 10f; na = -10f; }
        else if (Talking) { fa = -talkArm; na = talkArm; } else { fa = sw * 32f; na = -sw * 32f; }
        Limb(g, 25, -112, 44, fa, c1, 12, skin, 6); Limb(g, -25, -112, 44, na, c1, 12, skin, 6);
        using (var b = new SolidBrush(skin)) g.FillEllipse(b, -26, -176, 52, 52);
        using (var b = new SolidBrush(hair)) g.FillPie(b, -27, -178, 54, 54, 180, 180);
        if (f.Phones) { using (var pn = new Pen(Theme.Accent, 6)) g.DrawArc(pn, -31, -182, 62, 62, 180, 180); using var b = new SolidBrush(Theme.Accent); g.FillEllipse(b, -35, -158, 11, 22); g.FillEllipse(b, 24, -158, 11, 22); }
        if (f.Hat == "cap") { using var b = new SolidBrush(f.HatC); g.FillPie(b, -29, -182, 58, 54, 180, 180); g.FillEllipse(b, 8, -162, 36, 9); }
        else if (f.Hat == "beanie") { using var b = new SolidBrush(f.HatC); g.FillPie(b, -29, -190, 58, 60, 180, 180); using var b2 = new SolidBrush(Color.FromArgb(235, 235, 240)); g.FillRectangle(b2, -29, -161, 58, 9); g.FillEllipse(b2, -7, -197, 14, 14); }
        else if (f.Hat == "crown") { using var b = new SolidBrush(Color.FromArgb(250, 200, 50)); g.FillPolygon(b, new[] { new PointF(-20, -174), new PointF(-22, -200), new PointF(-10, -186), new PointF(0, -204), new PointF(10, -186), new PointF(22, -200), new PointF(20, -174) }); }
        if (f.Mask) { using var b = new SolidBrush(f.MaskC); using var mp = U.Round(new Rectangle(-27, -162, 54, 17), 8); g.FillPath(b, mp); }
        bool blink = DateTime.Now < blinkEnd;
        using (var b = new SolidBrush(f.Mask ? Color.White : dark)) using (var pn = new Pen(dark, 2))
        {
            if (blink) { g.DrawLine(pn, -8, -148, -1, -148); g.DrawLine(pn, 8, -148, 15, -148); }
            else { g.FillEllipse(b, -8, -153, 7, 10); g.FillEllipse(b, 8, -153, 7, 10); }
            if (f.Glasses) { using var gp = new Pen(dark, 2); if (f.Sun) { using var sb = new SolidBrush(Color.FromArgb(200, 20, 20, 30)); g.FillEllipse(sb, -14, -158, 16, 15); g.FillEllipse(sb, 6, -158, 16, 15); } g.DrawEllipse(gp, -14, -158, 16, 15); g.DrawEllipse(gp, 6, -158, 16, 15); g.DrawLine(gp, 2, -151, 6, -151); }
            if (Push > 0) g.DrawLine(pn, 0, -138, 12, -138);
            else if (Talking) { float h = 4 + MathF.Abs(MathF.Sin(t * 14f)) * 8f; using var mb = new SolidBrush(dark); g.FillEllipse(mb, 0, -138, 12, h); }
            else g.DrawArc(pn, -1, -146, 16, 12, 20, 140);
        }
    }
}

class PetBubble : Control
{
    string text = ""; int tail; readonly Font f = Ui.F(10);
    public PetBubble() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.White; Visible = false; }
    public void SetText(string t)
    {
        if (t.Length > 260) t = t[..260] + "…"; text = t; int pad = Ui.D(12); tail = Ui.D(10);
        var sz = TextRenderer.MeasureText(t, f, new Size(Ui.D(240), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        Size = new Size(Math.Max(Ui.D(46), sz.Width + pad * 2 + 2), sz.Height + pad * 2 + tail);
        using var rp = U.Round(new Rectangle(0, 0, Width, Height - tail), Ui.D(14)); var rg = new Region(rp);
        using var tp = new GraphicsPath(); tp.AddPolygon(new[] { new Point(Width / 2 - Ui.D(8), Height - tail - 1), new Point(Width / 2, Height), new Point(Width / 2 + Ui.D(8), Height - tail - 1) }); rg.Union(tp);
        Region = rg; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.White);
        TextRenderer.DrawText(e.Graphics, text, f, new Rectangle(Ui.D(12), Ui.D(12), Math.Max(10, Width - Ui.D(24)), Math.Max(10, Height - tail - Ui.D(24))), Color.FromArgb(30, 30, 44), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }
}

static class Pet
{
    static DesktopForm? sh; static Panel? desk; static PetSprite? sp; static PetBubble? bu; static Control? bar; static TextBox? tb;
    static readonly Random rnd = new(); static System.Windows.Forms.Timer? tm;
    static DateTime last = DateTime.Now, idleUntil = DateTime.Now.AddSeconds(2), pausedUntil, hideBubbleAt = DateTime.MaxValue, barUntil, nextFix;
    static PointF pos; static PointF? target; static bool busy, speaking, dragging, down; static int speechId; static Point dn, startLoc;

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
        if (job != null) return "ERR: the character is still busy pushing something.";
        var p0 = icon.Location; var way = new List<Point>(); bool hf = Math.Abs(dest.X - p0.X) >= Math.Abs(dest.Y - p0.Y);
        var mid = hf ? new Point(dest.X, p0.Y) : new Point(p0.X, dest.Y); if (mid != p0) way.Add(mid); if (dest != mid) way.Add(dest);
        job = new PushJob { Icon = icon, IconPos = p0, Way = way, Dest = dest };
        return sp.Visible ? "The character is pushing " + icon.Label + " now." : "Queued: close or minimize the chat window to watch the character push " + icon.Label + ".";
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
        sp = new PetSprite { Width = Ui.D(150), Height = Ui.D(170) }; sp.Fit = Outfits.FirstOrDefault(o => o.Name == Cfg.Get("pet_outfit", "hoodie")) ?? Outfits[0]; bu = new PetBubble();
        var (box, t) = Ui.Input("Talk to " + Cfg.Get("ai_name", "Morales") + "…", false, 38); bar = box; tb = t; bar.Width = Ui.D(260); bar.Height = Ui.D(38); bar.Visible = false;
        d.Controls.Add(bar); d.Controls.Add(bu); d.Controls.Add(sp);
        pos = new PointF(Ui.D(320), Math.Max(Ui.D(60), d.Height - sp.Height - Ui.D(140))); sp.Location = Point.Round(pos);
        var menu = new ContextMenuStrip(); menu.Items.Add("Talk to me", null, (_, _) => ToggleBar()); var cm = new ToolStripMenuItem("Change clothes"); foreach (var o in Outfits) { var n = o.Name; cm.DropDownItems.Add(char.ToUpper(n[0]) + n[1..], null, (_, _) => SetOutfit(n)); } menu.Items.Add(cm); menu.Items.Add("Hide the character", null, (_, _) => Cfg.Set("pet", "0")); sp.ContextMenuStrip = menu;
        sp.MouseDown += (_, e) => { if (e.Button != MouseButtons.Left) return; down = true; dragging = false; dn = Cursor.Position; startLoc = sp.Location; };
        sp.MouseMove += (_, e) =>
        {
            if (!down) return; var c = Cursor.Position; int dx = c.X - dn.X, dy = c.Y - dn.Y;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) > 6) { dragging = true; target = null; }
            if (dragging) pos = Clamp(new PointF(startLoc.X + dx, startLoc.Y + dy));
        };
        sp.MouseUp += (_, e) => { if (e.Button != MouseButtons.Left) return; bool was = dragging; down = false; dragging = false; pausedUntil = DateTime.Now.AddSeconds(5); if (!was) ToggleBar(); };
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Ask(tb.Text); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) { bar!.Visible = false; e.SuppressKeyPress = true; } };
        tb.TextChanged += (_, _) => barUntil = DateTime.Now.AddSeconds(15);
        Assistant.Replied += (text, fromPet) => { if (!fromPet && sp != null && sp.Visible) ShowBubble(text, Math.Max(5, text.Length * 0.06)); };
        tm = new System.Windows.Forms.Timer { Interval = 40 }; tm.Tick += (_, _) => Tick(); tm.Start();
    }

    public static void Command(string a)
    {
        a = a.Trim().ToLowerInvariant();
        if (a == "off") Cfg.Set("pet", "0");
        else { Cfg.Set("pet", "1"); if (a == "come" && desk != null && sp != null) { pausedUntil = default; target = Clamp(new PointF(desk.Width / 2f - sp.Width / 2f, desk.Height / 2f)); } }
    }

    static PointF Clamp(PointF p) => desk == null || sp == null ? p : new PointF(Math.Clamp(p.X, 0, Math.Max(0, desk.Width - sp.Width)), Math.Clamp(p.Y, Ui.D(20), Math.Max(Ui.D(20), desk.Height - sp.Height - Ui.D(54))));

    static void Tick()
    {
        if (sh == null || desk == null || sp == null || bu == null || bar == null || tb == null) return;
        var now = DateTime.Now; float dt = Math.Min(0.2f, (float)(now - last).TotalSeconds); last = now;
        bool show = Cfg.Get("pet", "1") == "1" && desk.Visible && !sh.AssistantOpen();
        if (!show) { if (sp.Visible) { sp.Visible = false; bu.Visible = false; bar.Visible = false; } return; }
        if (!sp.Visible) { sp.Visible = true; pos = Clamp(pos); }
        if (now >= nextFix) { nextFix = now.AddSeconds(1.5); Fix(); }
        if (job != null) { RunJob(dt, now); sp.Location = Point.Round(pos); sp.Step(dt); sp.Invalidate(); Place(); return; }
        bool still = dragging || speaking || busy || bar.Visible || now < pausedUntil;
        if (still) { sp.Moving = false; if (!dragging) target = null; }
        else if (target == null) { sp.Moving = false; if (now >= idleUntil) { var a = rnd.NextDouble() * Math.PI * 2; float r = Ui.D(150) + (float)rnd.NextDouble() * Ui.D(300); target = Clamp(new PointF(pos.X + MathF.Cos((float)a) * r, pos.Y + MathF.Sin((float)a) * r * 0.6f)); } }
        else
        {
            float dx = target.Value.X - pos.X, dy = target.Value.Y - pos.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 4) { target = null; idleUntil = now.AddSeconds(2.5 + rnd.NextDouble() * 5); sp.Moving = false; }
            else { float st = Math.Min(dist, Ui.D(70) * dt); pos = new PointF(pos.X + dx / dist * st, pos.Y + dy / dist * st); sp.Moving = true; if (Math.Abs(dx) > 2) sp.Right = dx > 0; }
        }
        sp.Location = Point.Round(Clamp(pos)); sp.Step(dt); sp.Invalidate(); Place();
        if (bu.Visible && now >= hideBubbleAt && !speaking && !busy) bu.Visible = false;
        if (bar.Visible && !busy && now > barUntil && tb.Text == "") bar.Visible = false;
    }

    // keep the character above the icons but below every window
    static void Fix()
    {
        if (desk == null || sp == null) return; var c = desk.Controls;
        int maxWin = c.OfType<Win>().Select(w => c.GetChildIndex(w)).DefaultIfEmpty(-1).Max(); int minIcon = c.OfType<DeskIcon>().Select(i => c.GetChildIndex(i)).DefaultIfEmpty(int.MaxValue).Min();
        int idx = c.GetChildIndex(sp); if (idx < maxWin || idx > minIcon) c.SetChildIndex(sp, Math.Min(maxWin + 1, c.Count - 1));
    }

    static void Place()
    {
        if (desk == null || sp == null || bu == null || bar == null) return;
        if (bar.Visible) bar.Location = new Point(Math.Clamp(sp.Left + (sp.Width - bar.Width) / 2, Ui.D(4), Math.Max(Ui.D(4), desk.Width - bar.Width - Ui.D(4))), sp.Bottom + Ui.D(2));
        if (bu.Visible) bu.Location = new Point(Math.Clamp(sp.Left + sp.Width / 2 - bu.Width / 2, Ui.D(4), Math.Max(Ui.D(4), desk.Width - bu.Width - Ui.D(4))), Math.Max(Ui.D(4), sp.Top - bu.Height + Ui.D(8)));
    }

    static void ToggleBar()
    {
        if (bar == null || tb == null) return; if (bar.Visible) { bar.Visible = false; return; }
        bar.Visible = true; Place(); bar.BringToFront(); tb.Focus(); barUntil = DateTime.Now.AddSeconds(15);
    }

    static void ShowBubble(string t, double secs)
    {
        if (bu == null) return; bu.SetText(t); bu.Visible = true; Place(); bu.BringToFront(); hideBubbleAt = DateTime.Now.AddSeconds(secs);
    }

    static async void Ask(string text)
    {
        text = text.Trim(); if (text == "" || busy || sh == null || tb == null) return;
        busy = true; tb.Text = ""; ShowBubble("…", 120);
        try { var r = await Assistant.Converse(sh, text, true); Speak(r); }
        catch (Exception ex) { ShowBubble("Hmm: " + ex.Message, 7); }
        finally { busy = false; barUntil = DateTime.Now.AddSeconds(12); }
    }

    static void Speak(string text)
    {
        ShowBubble(text, Math.Max(5, text.Length * 0.06)); if (Cfg.Get("pet_voice", "1") != "1" || sp == null) return;
        int id = ++speechId; speaking = true; sp.Talking = true;
        Assistant.Say(text, () => { try { desk!.BeginInvoke(new Action(() => { if (id != speechId) return; speaking = false; if (sp != null) sp.Talking = false; hideBubbleAt = DateTime.Now.AddSeconds(2); })); } catch { } });
    }
}
