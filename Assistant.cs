using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace UsbDesktop;

// Round avatar that pulses while the assistant is thinking, listening or speaking.
class AiOrb : Control
{
    public int Mode; public string Letter = "M"; int tick;
    readonly System.Windows.Forms.Timer tm = new() { Interval = 60 };
    public AiOrb()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.Transparent;
        tm.Tick += (_, _) => { tick++; if (Mode != 0) Invalidate(); }; tm.Start();
    }
    protected override void Dispose(bool d) { if (d) tm.Dispose(); base.Dispose(d); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; int m = Ui.D(6);
        var r = new Rectangle(m, m, Math.Max(4, Width - 2 * m), Math.Max(4, Height - 2 * m));
        if (Mode != 0)
        {
            float ph = (tick % 20) / 20f; int grow = (int)(m * ph); var rr = Rectangle.Inflate(r, grow, grow);
            var ring = Mode == 2 ? Color.FromArgb(90, 220, 160) : Theme.Accent;
            using var pn = new Pen(Color.FromArgb((int)(150 * (1 - ph)), ring), 2); g.DrawEllipse(pn, rr);
        }
        using var lg = new LinearGradientBrush(r, Theme.Accent, Ui.Accent2, 45f); g.FillEllipse(lg, r);
        TextRenderer.DrawText(g, Letter, Ui.F(13, true), r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

// Chat bubbles, drawn by hand so they wrap and scroll smoothly.
class ChatList : Control
{
    public readonly List<(bool user, string text)> Items = new();
    public string? Typing; public string Letter = "M"; int scroll, dots; readonly List<(Rectangle r, int i)> hit = new();
    readonly System.Windows.Forms.Timer tm = new() { Interval = 380 };
    readonly Font f = Ui.F(10.5f);
    const TextFormatFlags TF = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top;
    public ChatList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); BackColor = Ui.Surface;
        tm.Tick += (_, _) => { if (Typing != null) { dots = (dots + 1) % 4; Invalidate(); } }; tm.Start(); MouseEnter += (_, _) => Focus();
        var m = new ContextMenuStrip(); m.Items.Add("Copy message", null, (_, _) => { var p = PointToClient(Cursor.Position); foreach (var (r, i) in hit) if (r.Contains(p)) { try { Clipboard.SetText(Items[i].text); } catch { } break; } }); ContextMenuStrip = m;
    }
    protected override void Dispose(bool d) { if (d) tm.Dispose(); base.Dispose(d); }
    static string Clean(string t) => Regex.Replace(t.Replace("**", "").Replace("`", ""), @"^#+\s*", "", RegexOptions.Multiline).Trim();
    int Total(List<(Rectangle r, string t, bool u, bool first)> lay) => lay.Count == 0 ? 0 : lay[^1].r.Bottom + Ui.D(16);
    List<(Rectangle r, string t, bool u, bool first)> Lay()
    {
        var l = new List<(Rectangle, string, bool, bool)>(); int pad = Ui.D(18), px = Ui.D(14), py = Ui.D(10), gap = Ui.D(10), av = Ui.D(38), sb = Ui.D(8);
        int maxW = Math.Max(Ui.D(120), (int)(Width * 0.74) - px * 2 - av - gap); int y = pad; bool prevUser = true;
        void One(string t, bool user)
        {
            var sz = TextRenderer.MeasureText(t, f, new Size(maxW, 0), TF); int w = sz.Width + px * 2 + Ui.D(2), h = sz.Height + py * 2;
            int x = user ? Width - pad - sb - w : pad + av + gap; l.Add((new Rectangle(x, y, w, h), t, user, !user && prevUser)); y += h + gap; prevUser = user;
        }
        foreach (var it in Items) One(Clean(it.text), it.user);
        if (Typing != null) One(Typing + new string('.', dots), false);
        return l;
    }
    public void Add(bool user, string text) { bool follow = scroll >= MaxScroll - Ui.D(6); Items.Add((user, text)); if (follow || user) ToEnd(); Invalidate(); }
    int MaxScroll => Math.Max(0, Total(Lay()) - Height);
    public void ToEnd() { scroll = MaxScroll; Invalidate(); }
    public void SetTyping(string? t) { Typing = t; dots = 0; ToEnd(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); scroll = Math.Clamp(scroll - e.Delta / 120 * Ui.D(70), 0, MaxScroll); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Ui.Surface); hit.Clear(); var lay = Lay(); int total = Total(lay); scroll = Math.Clamp(scroll, 0, Math.Max(0, total - Height)); int av = Ui.D(38);
        if (Items.Count == 0 && Typing == null) TextRenderer.DrawText(g, "Say hi, or tell me what to do", Ui.F(12), ClientRectangle, Ui.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        for (int k = 0; k < lay.Count; k++)
        {
            var (r0, t, user, first) = lay[k]; var r = new Rectangle(r0.X, r0.Y - scroll, r0.Width, r0.Height); if (r.Bottom < 0 || r.Top > Height) continue;
            if (k < Items.Count) hit.Add((r, k));
            using var path = U.Round(r, Ui.D(16));
            if (user) { using var lg = new LinearGradientBrush(r, Theme.Accent, Ui.Accent2, 20f); g.FillPath(lg, path); } else { using var b = new SolidBrush(Color.FromArgb(44, 47, 76)); g.FillPath(b, path); }
            TextRenderer.DrawText(g, t, f, new Rectangle(r.X + Ui.D(14), r.Y + Ui.D(10), r.Width - Ui.D(24), r.Height - Ui.D(16)), Color.White, TF);
            if (first) { var a = new Rectangle(Ui.D(18), r.Y, av, av); using var lg2 = new LinearGradientBrush(a, Theme.Accent, Ui.Accent2, 45f); g.FillEllipse(lg2, a); TextRenderer.DrawText(g, Letter, Ui.F(11, true), a, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding); }
        }
        if (total > Height) { int bh = Math.Max(Ui.D(30), Height * Height / total); int by = (int)((Height - bh) * (scroll / (float)(total - Height))); using var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)); g.FillRectangle(b, Width - Ui.D(5), by, Ui.D(3), bh); }
    }
}

// AI Assistant: chats through your RelayAPI (OpenAI-style), can act on the desktop, talks and listens, keeps history and memory on the USB.
static class Assistant
{
    static string Dir => Path.Combine(Paths.Data, "ai");
    static string HistFile => Path.Combine(Dir, "chat.json");
    static string MemFile => Path.Combine(Dir, "memory.txt");
    static readonly Regex Act = new(@"\[\[(\w+):([^\]]*)\]\]", RegexOptions.Compiled);
    static readonly string[] Apps = { "files", "code", "notepad", "calc", "paint", "term", "web", "store", "tm", "vault", "vm", "settings", "ai", "getapps", "games" };
    static System.Speech.Synthesis.SpeechSynthesizer? synth;
    const string DefUrl = "https://relayapi-c5ah.onrender.com";

    static string Sys(string snap)
    {
        string name = Cfg.Get("ai_name", "Morales"), user = Cfg.Get("ai_user", "Boss"); string mem = "";
        try { if (File.Exists(MemFile)) mem = string.Join("; ", File.ReadAllLines(MemFile).TakeLast(60)); } catch { }
        return $"You are {name}, the built-in assistant of USB Desktop, a portable Windows desktop that runs from a USB drive. The user is called {user}; talk like a friendly, warm, short-spoken friend. " +
            (mem != "" ? "Things you know about the user: " + mem + ". " : "") +
            "You can act on the desktop by putting action tags in your reply: " +
            "[[open:APP]] opens an app (APP is one of files, code, notepad, calc, paint, term, web, store, tm, vault, vm, settings, ai, getapps, games); " +
            "[[web:https://example.com]] opens a page in the browser; " +
            "[[note:Title|text of the note]] saves a text file on the Desktop; " +
            "[[read:Desktop/file.txt]] reads a text file from the USB (you will get its text back); " +
            "[[run:command]] runs a Windows command (cmd) after the user approves, and you get its output back; " +
            "[[remember:a short fact about the user]] saves something to remember for next time; " +
            "paths below are relative to the USB root: [[list:Desktop]] lists a folder; [[search:word]] searches file names and text; [[sysinfo:]] gives OS, RAM, USB space, battery and Wi-Fi; [[desktop:]] shows icons, windows and the sticky note; " +
            "[[sticky:new text]] replaces the sticky note and [[stickyread:]] reads it; [[mkdir:Desktop/New]]; [[move:Desktop/a.txt|Desktop/Folder]]; [[copy:src|dst]]; [[rename:Desktop/a.txt|b.txt]]; [[delete:Desktop/a.txt]] (goes to the Recycle Bin); " +
            "[[schedule:startup|open:notepad]], [[schedule:daily 07:30|remind:text]] or [[schedule:every 30|remind:text]] (every N minutes, 5 or more) create a task (allowed actions: open:APP, web:URL, note:Title|text, sticky:text, remind:text, arrange:), [[tasks:]] lists them, [[unschedule:id or words]] removes one, tasks only run while USB Desktop is open; " +
            "these control the Browser window: [[scroll:down|up|top|bottom]], [[nav:back|forward|reload]], [[goto:address or search words]], [[play:song or video name]] searches YouTube and plays the first result, [[media:play|pause|toggle|mute|unmute|next|fullscreen]], [[page:]] reads the page title, text and clickable items, [[click:text of a button or link]], [[type:box name|text to type]], [[key:enter]] presses Enter; open a Browser first with [[web:...]] if none is open; " +
            "[[arrange:]] tidies desktop icons; [[undo:]] undoes the last change; [[clipread:]] and [[clipwrite:text]] use the clipboard; [[eject:]] safely ejects this USB; [[lock:]] locks the PC; [[sleep:]] sleeps the PC. " +
            "Right now: " + snap.Replace("\n", " | ") + ". " +
            "Only use a tag when it helps or the user asks. Never mention these tags in your words. Keep replies short because they may be read out loud.";
    }

    static List<Dictionary<string, string>> Load()
    {
        try { if (File.Exists(HistFile)) return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(HistFile)) ?? new(); } catch { }
        return new();
    }
    static void Save(List<Dictionary<string, string>> h)
    {
        try { Directory.CreateDirectory(Dir); if (h.Count > 200) h.RemoveRange(0, h.Count - 200); File.WriteAllText(HistFile, JsonSerializer.Serialize(h)); } catch { }
    }

    public static Control View(DesktopForm sh)
    {
        var root = new Panel { BackColor = Ui.Surface }; Ui.Own(root);
        var hist = Load(); bool busy = false, talk = false, speaking = false;
        string aiName() => Cfg.Get("ai_name", "Morales");

        // header
        var head = new Panel { Dock = DockStyle.Top, Height = Ui.D(64), BackColor = Ui.Surface2 };
        var orb = new AiOrb { Left = Ui.D(10), Top = Ui.D(8), Width = Ui.D(48), Height = Ui.D(48), Letter = aiName()[..1].ToUpper() };
        var title = new Label { Text = aiName(), Font = Ui.F(12.5f, true), ForeColor = Ui.Text, BackColor = Color.Transparent, Left = Ui.D(64), Top = Ui.D(10), AutoSize = true };
        var sub = new Label { Text = "Online", Font = Ui.F(9), ForeColor = Ui.Sub, BackColor = Color.Transparent, Left = Ui.D(64), Top = Ui.D(36), AutoSize = true };
        var btns = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, Ui.D(16), Ui.D(8), 0) };
        RoundButton TB(string t, int w) { var b = new RoundButton { Text = t, Pill = true, BackColor = Ui.Line, ForeColor = Ui.Text, Width = Ui.D(w), Height = Ui.D(32), Font = Ui.F(9), Margin = new Padding(Ui.D(4), 0, 0, 0) }; btns.Controls.Add(b); return b; }
        head.Controls.Add(orb); head.Controls.Add(title); head.Controls.Add(sub); head.Controls.Add(btns);
        var bSet = TB("Settings", 82); var bClr = TB("Clear", 64); var bVoice = TB(Cfg.Get("ai_speak", "0") == "1" ? "Voice: On" : "Voice: Off", 92); var bTalk = TB("Talk", 76); var bUndo = TB("Undo", 64); var bTasks = TB("Tasks", 70);

        // settings
        var set = new Panel { Dock = DockStyle.Top, Height = Ui.D(350), BackColor = Ui.Surface2, Visible = false, Padding = new Padding(Ui.D(14), Ui.D(4), Ui.D(14), Ui.D(8)) };
        var (uBox, uTb) = Ui.Input("RelayAPI address", false, 38); uTb.Text = Cfg.Get("ai_url", DefUrl);
        var (kBox, kTb) = Ui.Input("RelayAPI key", true, 38); kTb.Text = Cfg.Get("ai_key");
        var (mBox, mTb) = Ui.Input("Provider name (optional: empty uses your first provider)", false, 38); mTb.Text = Cfg.Get("ai_model");
        var (nBox, nTb) = Ui.Input("Assistant name", false, 38); nTb.Text = Cfg.Get("ai_name", "Morales");
        var (yBox, yTb) = Ui.Input("What should it call you?", false, 38); yTb.Text = Cfg.Get("ai_user", "Boss");
        var bSave = new RoundButton { Text = "Save", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Height = Ui.D(34), Width = Ui.D(120), Dock = DockStyle.Top };
        Panel Gap(int h) => new Panel { Dock = DockStyle.Top, Height = Ui.D(h), BackColor = Color.Transparent };
        foreach (var bx in new Control[] { uBox, kBox, mBox, nBox, yBox }) { bx.Dock = DockStyle.Top; bx.Height = Ui.D(38); }
        var chk = new CheckBox { Text = "Ask me before every action (off = it only asks for risky things: shutting down/locking the PC, sensitive deletes, unknown commands, purchases)", Dock = DockStyle.Top, Height = Ui.D(46), ForeColor = Ui.Text, BackColor = Color.Transparent, Font = Ui.F(9.5f), Checked = Cfg.Get("ai_ask", "0") == "1" };
        chk.CheckedChanged += (_, _) => Cfg.Set("ai_ask", chk.Checked ? "1" : "0");
        set.Controls.Add(bSave); set.Controls.Add(Gap(6)); set.Controls.Add(chk); set.Controls.Add(Gap(6)); set.Controls.Add(yBox); set.Controls.Add(Gap(6)); set.Controls.Add(nBox); set.Controls.Add(Gap(6)); set.Controls.Add(mBox); set.Controls.Add(Gap(6)); set.Controls.Add(kBox); set.Controls.Add(Gap(6)); set.Controls.Add(uBox);

        // chat + input
        var chat = new ChatList { Dock = DockStyle.Fill, Letter = aiName()[..1].ToUpper() };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = Ui.D(72), BackColor = Ui.Surface2, Padding = new Padding(Ui.D(12)) };
        var (box, tb) = Ui.Input("Message " + aiName() + "…", false, 46); box.Dock = DockStyle.Fill;
        var bSend = new RoundButton { Text = "Send", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Dock = DockStyle.Right, Width = Ui.D(84) };
        bottom.Controls.Add(box); bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Ui.D(8), BackColor = Color.Transparent }); bottom.Controls.Add(bSend);
        root.Controls.Add(chat); root.Controls.Add(bottom); root.Controls.Add(set); root.Controls.Add(head);

        foreach (var h in hist.TakeLast(40)) chat.Items.Add((h["role"] == "user", h["content"]));
        if (hist.Count == 0) chat.Items.Add((false, "Hi! I can chat, open apps, save notes and talk out loud. Press Settings once and add your RelayAPI key. Press Talk to speak with me."));
        chat.ToEnd();

        void Mode(int m, string text) { orb.Mode = m; sub.Text = text; }

        // ---- speech ----
        System.Speech.Recognition.SpeechRecognitionEngine? rec = null;
        void StartListen()
        {
            if (!talk || busy || speaking || root.IsDisposed) return;
            try
            {
                if (rec == null)
                {
                    rec = new System.Speech.Recognition.SpeechRecognitionEngine(); rec.LoadGrammar(new System.Speech.Recognition.DictationGrammar()); rec.SetInputToDefaultAudioDevice();
                    rec.InitialSilenceTimeout = TimeSpan.FromSeconds(10); rec.BabbleTimeout = TimeSpan.FromSeconds(6); rec.EndSilenceTimeout = TimeSpan.FromMilliseconds(900); rec.EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds(1300);
                    rec.SpeechRecognized += (_, e) => { var t = e.Result.Text; if (e.Result.Confidence > 0.3 && t.Trim() != "") try { root.BeginInvoke(new Action(() => Send(t))); } catch { } };
                    rec.RecognizeCompleted += (_, _) => { try { root.BeginInvoke(new Action(async () => { await Task.Delay(250); StartListen(); })); } catch { } };
                }
                rec.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Single); Mode(2, "Listening… just talk");
            }
            catch { talk = false; bTalk.Text = "Talk"; bTalk.BackColor = Ui.Line; Mode(0, "Online"); sh.Toast("No microphone or speech recognizer found on this PC"); }
        }
        void StopTalk() { talk = false; bTalk.Text = "Talk"; bTalk.BackColor = Ui.Line; try { rec?.RecognizeAsyncCancel(); } catch { } if (!busy && !speaking) Mode(0, "Online"); }
        void Speak(string text)
        {
            try
            {
                synth ??= new System.Speech.Synthesis.SpeechSynthesizer(); synth.SpeakAsyncCancelAll(); speaking = true; Mode(3, "Speaking…");
                EventHandler<System.Speech.Synthesis.SpeakCompletedEventArgs>? done = null;
                done = (_, _) => { synth!.SpeakCompleted -= done; try { root.BeginInvoke(new Action(() => { speaking = false; if (talk) StartListen(); else Mode(0, "Online"); })); } catch { } };
                synth.SpeakCompleted += done; synth.SpeakAsync(Regex.Replace(text, @"[*`#]", ""));
            }
            catch { speaking = false; }
        }

        // ---- actions ----
        string Run(string reply)
        {
            return Act.Replace(reply, m =>
            {
                var k = m.Groups[1].Value.ToLowerInvariant(); var a = m.Groups[2].Value.Trim();
                try
                {
                    if (k == "open" && Apps.Contains(a.ToLowerInvariant())) sh.Launch(a.ToLowerInvariant());
                    else if (k == "web" && a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) sh.Launch("web", a);
                    else if (k == "remember" && a != "") { Directory.CreateDirectory(Dir); File.AppendAllText(MemFile, a.Replace("\n", " ") + "\n"); }
                    else if (k == "note")
                    {
                        int i = a.IndexOf('|'); var name = i > 0 ? a[..i].Trim() : "Note"; var body = i > 0 ? a[(i + 1)..] : a;
                        foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_'); if (name == "") name = "Note";
                        File.WriteAllText(U.Unique(Paths.Desktop, name + ".txt"), body.Replace("\\n", "\n")); sh.Reload(); sh.Toast("Saved note: " + name);
                    }
                }
                catch { }
                return "";
            }).Trim();
        }

        async Task<(string shown, string feedback)> Handle(string reply)
        {
            var fb = new List<string>(); var muts = new List<(string k, string a)>(); bool trustB = Cfg.Get("ai_ask", "0") != "1";
            bool Yes(string t) => MessageBox.Show(t, "AI Assistant", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            foreach (Match m in Act.Matches(reply))
            {
                var k = m.Groups[1].Value.ToLowerInvariant(); var a = m.Groups[2].Value.Trim(); if (!AiActions.Kinds.Contains(k)) continue;
                try
                {
                    switch (k)
                    {
                        case "read":
                            {
                                var full = AiActions.Resolve(a); if (full == null) { fb.Add("Reading " + a + " was not allowed."); break; }
                                var rel = Path.GetRelativePath(Paths.Root, full);
                                if (!trustB && !Yes("The assistant wants to read this file and see its text:\n\n" + rel + "\n\nAllow?")) { fb.Add("The user did not allow reading " + a + "."); break; }
                                if (!File.Exists(full)) { fb.Add("File not found: " + a); break; }
                                var t = File.ReadAllText(full); if (t.Length > 6000) t = t[..6000] + "\n[cut off]"; fb.Add("Contents of " + a + ":\n" + t); break;
                            }
                        case "run":
                            {
                                if (!(trustB && AiActions.SafeCmd(a)) && MessageBox.Show("The assistant wants to run this command on this PC:\n\n" + a + "\n\nOnly allow it if you understand it. Run it?", "AI Assistant", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) { fb.Add("The user did not allow running that command."); break; }
                                var o = await Task.Run(() =>
                                {
                                    using var pr = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + a) { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = Paths.Root })!;
                                    var so = pr.StandardOutput.ReadToEndAsync(); var se = pr.StandardError.ReadToEndAsync();
                                    if (!pr.WaitForExit(20000)) { try { pr.Kill(true); } catch { } return "[stopped after 20 seconds]"; }
                                    var r = so.Result + se.Result; return r.Length > 4000 ? r[..4000] + "\n[cut off]" : r;
                                });
                                fb.Add("Output of " + a + ":\n" + (o.Trim() == "" ? "(no output)" : o)); break;
                            }
                        case "list": fb.Add(AiActions.List(a)); break;
                        case "sysinfo": fb.Add(await Task.Run(AiActions.SysInfo)); break;
                        case "desktop": fb.Add(sh.DesktopSnapshot()); break;
                        case "tasks": fb.Add(Sched.ListText()); break;
                        case "scroll": case "nav": case "goto": case "play": case "media": case "click": case "type": case "key": case "page": { var br = await BrowserCtl.Do(sh, k, a, trustB, Yes); if (br != "") fb.Add(br); break; }
                        case "unschedule": fb.Add(Sched.Remove(a)); break;
                        case "stickyread": fb.Add("Sticky note: " + Cfg.Get("sticky").Replace("¶", "\n")); break;
                        case "clipwrite": try { Clipboard.SetText(a); fb.Add("Copied to the clipboard."); } catch { fb.Add("Couldn't write the clipboard."); } break;
                        case "clipread": if (trustB || Yes("The assistant wants to read what is on your clipboard. Allow?")) { var c = Clipboard.ContainsText() ? Clipboard.GetText() : ""; fb.Add("Clipboard: " + (c.Length > 2000 ? c[..2000] : c)); } else fb.Add("The user did not allow reading the clipboard."); break;
                        case "search": if (trustB || Yes("The assistant wants to search your USB files for:\n\n" + a + "\n\nAllow?")) fb.Add(await Task.Run(() => AiActions.Search(a))); else fb.Add("The user did not allow searching."); break;
                        default: muts.Add((k, a)); break;
                    }
                }
                catch (Exception ex) { fb.Add("That action failed: " + ex.Message); }
            }
            if (muts.Count > 0)
            {
                bool trust = trustB;
                bool need = muts.Any(x => x.k is "eject" or "lock" or "sleep" || (x.k == "delete" && AiActions.SensitiveDelete(x.a)) || (!trust && x.k is "move" or "copy" or "rename" or "delete" or "schedule"));
                if (need && MessageBox.Show("The assistant wants to:\n\n" + string.Join("\n", muts.Select(x => "• " + AiActions.Describe(x.k, x.a))) + "\n\nAllow? (File changes can be undone with the Undo button.)", "AI Assistant", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) fb.Add("The user did not allow those actions.");
                else fb.AddRange(AiActions.RunAll(sh, muts).Where(x => x != ""));
            }
            return (Run(Act.Replace(reply, me => AiActions.Kinds.Contains(me.Groups[1].Value.ToLowerInvariant()) ? "" : me.Value)), string.Join("\n\n", fb));
        }

        async void Send(string text)
        {
            text = text.Trim(); if (busy || text == "") return;
            string url = Cfg.Get("ai_url", DefUrl), key = Cfg.Get("ai_key"), model = Cfg.Get("ai_model");
            if (url == "" || key == "") { chat.Add(false, "Add your RelayAPI key in Settings first."); set.Visible = true; return; }
            busy = true; try { synth?.SpeakAsyncCancelAll(); rec?.RecognizeAsyncCancel(); } catch { }
            tb.Text = ""; chat.Add(true, text); hist.Add(new() { ["role"] = "user", ["content"] = text }); Mode(1, "Thinking…"); chat.SetTyping("···");
            string clean = "";
            try
            {
                if (model == "") { model = await Net.FirstModel(url, key); if (model == "") throw new Exception("No provider found in RelayAPI. Add one in its dashboard."); }
                var msgs = new List<object> { new { role = "system", content = Sys(sh.DesktopSnapshot()) } };
                foreach (var h in hist.TakeLast(12)) msgs.Add(new { role = h["role"], content = h["content"] });
                for (int round = 0; round < 3; round++)
                {
                    var reply = await Net.Chat(url, key, model, msgs.ToArray(), CancellationToken.None);
                    var (shown, fbk) = await Handle(reply);
                    if (shown != "") clean = clean == "" ? shown : clean + "\n" + shown;
                    if (fbk == "") break;
                    msgs.Add(new { role = "assistant", content = reply }); msgs.Add(new { role = "user", content = fbk });
                }
                if (clean == "") clean = "Done.";
                hist.Add(new() { ["role"] = "assistant", ["content"] = clean }); Save(hist);
            }
            catch (Exception ex) { hist.RemoveAt(hist.Count - 1); clean = ""; chat.SetTyping(null); chat.Add(false, "Couldn't get an answer: " + ex.Message); }
            finally
            {
                busy = false; chat.SetTyping(null);
                if (!root.IsDisposed)
                {
                    if (clean != "") { chat.Add(false, clean); if (Cfg.Get("ai_speak", "0") == "1" || talk) Speak(clean); else { Mode(0, "Online"); } }
                    else if (talk) StartListen(); else Mode(0, "Online");
                }
            }
        }

        bSend.Click += (_, _) => Send(tb.Text);
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Send(tb.Text); e.SuppressKeyPress = true; } };
        bSet.Click += (_, _) => set.Visible = !set.Visible;
        bTasks.Click += (_, _) => Sched.Manage(sh);
        bUndo.Click += (_, _) => chat.Add(false, AiActions.Undo(sh));
        bSave.Click += (_, _) =>
        {
            Cfg.Set("ai_url", uTb.Text.Trim()); Cfg.Set("ai_key", kTb.Text.Trim()); Cfg.Set("ai_model", mTb.Text.Trim());
            Cfg.Set("ai_name", nTb.Text.Trim() == "" ? "Morales" : nTb.Text.Trim()); Cfg.Set("ai_user", yTb.Text.Trim() == "" ? "Boss" : yTb.Text.Trim());
            title.Text = aiName(); orb.Letter = chat.Letter = aiName()[..1].ToUpper(); orb.Invalidate(); chat.Invalidate(); set.Visible = false; sh.Toast("Assistant settings saved");
        };
        bClr.Click += (_, _) => { if (MessageBox.Show("Clear the whole chat history?", "AI Assistant", MessageBoxButtons.YesNo) != DialogResult.Yes) return; hist.Clear(); Save(hist); chat.Items.Clear(); chat.Invalidate(); };
        bVoice.Click += (_, _) => { bool on = Cfg.Get("ai_speak", "0") != "1"; Cfg.Set("ai_speak", on ? "1" : "0"); bVoice.Text = on ? "Voice: On" : "Voice: Off"; if (!on) try { synth?.SpeakAsyncCancelAll(); } catch { } };
        bTalk.Click += (_, _) =>
        {
            if (talk) { StopTalk(); return; }
            talk = true; bTalk.Text = "Stop"; bTalk.BackColor = Theme.Accent; bTalk.ForeColor = Color.White; if (!busy && !speaking) StartListen();
        };
        root.Disposed += (_, _) => { try { synth?.SpeakAsyncCancelAll(); rec?.RecognizeAsyncCancel(); rec?.Dispose(); } catch { } };
        return root;
    }
}
