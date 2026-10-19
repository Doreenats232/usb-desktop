using System.Text.Json;
using System.Text.RegularExpressions;
namespace UsbDesktop;

// AI Assistant: chats through your RelayAPI (OpenAI-style), can open apps / save notes for you,
// speaks answers, takes voice input, and keeps its chat history on the USB.
static class Assistant
{
    static string Dir => Path.Combine(Paths.Data, "ai");
    static string HistFile => Path.Combine(Dir, "chat.json");
    static readonly Regex Act = new(@"\[\[(\w+):([^\]]*)\]\]", RegexOptions.Compiled);
    static readonly string[] Apps = { "files", "code", "notepad", "calc", "paint", "term", "web", "store", "tm", "vault", "vm", "settings" };
    static System.Speech.Synthesis.SpeechSynthesizer? synth;
    const string DefUrl = "https://relayapi-c5ah.onrender.com";

    const string Sys =
        "You are the built-in assistant of USB Desktop, a portable Windows desktop that runs from a USB drive. Reply short and friendly. " +
        "You can act on the desktop by putting action tags on their own in your reply: " +
        "[[open:APP]] opens an app (APP is one of files, code, notepad, calc, paint, term, web, store, tm, vault, vm, settings); " +
        "[[web:https://example.com]] opens a page in the browser; " +
        "[[note:Title|text of the note]] saves a text file on the Desktop. " +
        "Only use a tag when the user asks for that action. Never mention these tags in your words.";

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
        var hist = Load(); bool busy = false;

        // top bar
        var bar = new Panel { Dock = DockStyle.Top, Height = Ui.D(48), BackColor = Ui.Surface2 };
        var title = new Label { Text = "AI Assistant", Font = Ui.F(12, true), ForeColor = Ui.Text, BackColor = Color.Transparent, Left = Ui.D(14), Top = Ui.D(12), AutoSize = true };
        var btns = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, Ui.D(8), Ui.D(8), 0) };
        RoundButton TB(string t, int w) { var b = new RoundButton { Text = t, Pill = true, BackColor = Ui.Line, ForeColor = Ui.Text, Width = Ui.D(w), Height = Ui.D(30), Font = Ui.F(9), Margin = new Padding(Ui.D(4), 0, 0, 0) }; btns.Controls.Add(b); return b; }
        bar.Controls.Add(title); bar.Controls.Add(btns);
        var bSet = TB("Settings", 84); var bClr = TB("Clear", 70); var bVoice = TB(Cfg.Get("ai_speak", "0") == "1" ? "Voice: On" : "Voice: Off", 96);

        // settings panel
        var set = new Panel { Dock = DockStyle.Top, Height = Ui.D(196), BackColor = Ui.Surface2, Visible = false, Padding = new Padding(Ui.D(14), Ui.D(4), Ui.D(14), Ui.D(8)) };
        var (uBox, uTb) = Ui.Input("RelayAPI address, e.g. https://relayapi-xxxx.onrender.com", false, 38); uTb.Text = Cfg.Get("ai_url", DefUrl);
        var (kBox, kTb) = Ui.Input("RelayAPI key", true, 38); kTb.Text = Cfg.Get("ai_key");
        var (mBox, mTb) = Ui.Input("Provider name (optional: empty uses your first RelayAPI provider)", false, 38); mTb.Text = Cfg.Get("ai_model");
        var bSave = new RoundButton { Text = "Save", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Height = Ui.D(34), Width = Ui.D(120) };
        foreach (var c in new Control[] { bSave, mBox, kBox, uBox }) { if (c is RoundPanel) { c.Dock = DockStyle.Top; c.Margin = new Padding(0); } }
        mBox.Height = kBox.Height = uBox.Height = Ui.D(38);
        var gap = (int h) => new Panel { Dock = DockStyle.Top, Height = Ui.D(h), BackColor = Color.Transparent };
        bSave.Dock = DockStyle.Top;
        set.Controls.Add(bSave); set.Controls.Add(gap(8)); set.Controls.Add(mBox); set.Controls.Add(gap(6)); set.Controls.Add(kBox); set.Controls.Add(gap(6)); set.Controls.Add(uBox);

        // chat + input
        var chat = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Ui.Surface, ForeColor = Ui.Text, Font = Ui.F(10.5f), DetectUrls = true };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = Ui.D(70), BackColor = Ui.Surface2, Padding = new Padding(Ui.D(10)) };
        var status = new Label { Dock = DockStyle.Bottom, Height = Ui.D(20), ForeColor = Ui.Sub, BackColor = Ui.Surface2, Font = Ui.F(9), Padding = new Padding(Ui.D(12), 0, 0, 0) };
        var (box, tb) = Ui.Input("Ask me anything, or tell me to open an app…", false, 46); box.Dock = DockStyle.Fill;
        var bSend = new RoundButton { Text = "Send", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Dock = DockStyle.Right, Width = Ui.D(84) };
        var bMic = new RoundButton { Text = "Mic", Pill = true, BackColor = Ui.Line, ForeColor = Ui.Text, Dock = DockStyle.Right, Width = Ui.D(70) };
        bottom.Controls.Add(box); bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Ui.D(8), BackColor = Color.Transparent }); bottom.Controls.Add(bMic); bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Ui.D(8), BackColor = Color.Transparent }); bottom.Controls.Add(bSend);

        root.Controls.Add(chat); root.Controls.Add(status); root.Controls.Add(bottom); root.Controls.Add(set); root.Controls.Add(bar);

        void Add(string who, string text, Color c)
        {
            chat.SelectionStart = chat.TextLength; chat.SelectionColor = c; chat.SelectionFont = Ui.F(9, true); chat.AppendText(who + "\n");
            chat.SelectionColor = Ui.Text; chat.SelectionFont = Ui.F(10.5f); chat.AppendText(text + "\n\n"); chat.ScrollToCaret();
        }
        foreach (var h in hist.TakeLast(30)) Add(h["role"] == "user" ? "You" : "Assistant", h["content"], h["role"] == "user" ? Theme.Accent : Ui.Sub);
        if (hist.Count == 0) Add("Assistant", "Hi! I can chat, open apps, save notes and read answers out loud. Press Settings once and add your RelayAPI key.", Ui.Sub);

        string Run(string reply)
        {
            return Act.Replace(reply, m =>
            {
                var k = m.Groups[1].Value.ToLowerInvariant(); var a = m.Groups[2].Value.Trim();
                try
                {
                    if (k == "open" && Apps.Contains(a.ToLowerInvariant())) sh.Launch(a.ToLowerInvariant());
                    else if (k == "web" && a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) sh.Launch("web", a);
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

        async void Send(string text)
        {
            text = text.Trim(); if (busy || text == "") return;
            string url = Cfg.Get("ai_url", DefUrl), key = Cfg.Get("ai_key"), model = Cfg.Get("ai_model");
            if (url == "" || key == "") { Add("Assistant", "Add your RelayAPI key in Settings first.", Ui.Sub); set.Visible = true; return; }
            busy = true; tb.Text = ""; Add("You", text, Theme.Accent); hist.Add(new() { ["role"] = "user", ["content"] = text }); status.Text = "Thinking…";
            try
            {
                if (model == "") { model = await Net.FirstModel(url, key); if (model == "") throw new Exception("No provider found in RelayAPI. Add one in its dashboard."); }
                var msgs = new List<object> { new { role = "system", content = Sys } };
                foreach (var h in hist.TakeLast(10)) msgs.Add(new { role = h["role"], content = h["content"] });
                var reply = await Net.Chat(url, key, model, msgs.ToArray(), CancellationToken.None);
                var clean = Run(reply); if (clean == "") clean = "Done.";
                hist.Add(new() { ["role"] = "assistant", ["content"] = clean }); Save(hist); Add("Assistant", clean, Ui.Sub);
                if (Cfg.Get("ai_speak", "0") == "1") try { synth ??= new System.Speech.Synthesis.SpeechSynthesizer(); synth.SpeakAsyncCancelAll(); synth.SpeakAsync(clean); } catch { }
            }
            catch (Exception ex) { hist.RemoveAt(hist.Count - 1); Add("Assistant", "Couldn't get an answer: " + ex.Message, Color.FromArgb(255, 150, 150)); }
            finally { busy = false; if (!root.IsDisposed) status.Text = ""; }
        }

        bSend.Click += (_, _) => Send(tb.Text);
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Send(tb.Text); e.SuppressKeyPress = true; } };
        bSet.Click += (_, _) => set.Visible = !set.Visible;
        bSave.Click += (_, _) => { Cfg.Set("ai_url", uTb.Text.Trim()); Cfg.Set("ai_key", kTb.Text.Trim()); Cfg.Set("ai_model", mTb.Text.Trim()); set.Visible = false; sh.Toast("Assistant settings saved"); };
        bClr.Click += (_, _) => { if (MessageBox.Show("Clear the whole chat history?", "AI Assistant", MessageBoxButtons.YesNo) != DialogResult.Yes) return; hist.Clear(); Save(hist); chat.Clear(); };
        bVoice.Click += (_, _) => { bool on = Cfg.Get("ai_speak", "0") != "1"; Cfg.Set("ai_speak", on ? "1" : "0"); bVoice.Text = on ? "Voice: On" : "Voice: Off"; if (!on) try { synth?.SpeakAsyncCancelAll(); } catch { } };
        bMic.Click += (_, _) =>
        {
            try
            {
                var rec = new System.Speech.Recognition.SpeechRecognitionEngine(); rec.LoadGrammar(new System.Speech.Recognition.DictationGrammar()); rec.SetInputToDefaultAudioDevice();
                rec.SpeechRecognized += (_, e) => { try { root.BeginInvoke(new Action(() => { tb.Text = e.Result.Text; Send(e.Result.Text); })); } catch { } };
                rec.RecognizeCompleted += (_, _) => { try { root.BeginInvoke(new Action(() => { if (!busy) status.Text = ""; })); } catch { } rec.Dispose(); };
                rec.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Single); status.Text = "Listening… speak now";
            }
            catch { sh.Toast("No microphone or speech recognizer found on this PC"); }
        };
        root.Disposed += (_, _) => { try { synth?.SpeakAsyncCancelAll(); } catch { } };
        return root;
    }
}
