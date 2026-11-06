using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Phone link: the USB Link Android app sends commands (and files) through a small relay server; this loop fetches them,
// runs them through the assistant and sends the answer back. Risky actions ask on the PC AND on the phone (first answer wins).
// It also publishes upcoming alarms and homework so the phone can ring its own notifications.
static class PhoneLink
{
    public class Ctx { public string? AttachPath, AttachName, SavedPath; public byte[]? AttachBytes; }
    public static Ctx? Current;                     // set while a phone command is running
    static CancellationTokenSource? cts;
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
    static string baseUrl = "", pair = "";
    static DesktopForm? shell;
    public static string Status = "Off";

    public static void Start(DesktopForm sh)
    {
        Stop();
        if (Cfg.Get("phone_on", "0") != "1") { Status = "Off"; return; }
        var url = Cfg.Get("phone_url").Trim().TrimEnd('/'); var code = Cfg.Get("phone_code").Trim();
        if (url == "" || code.Length < 8) { Status = "Add the server address and an 8+ character code"; return; }
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        baseUrl = url; pair = code; shell = sh;
        cts = new CancellationTokenSource(); _ = Loop(sh, url, code, cts.Token); _ = RemoteInput.Loop(sh, url, code, cts.Token); _ = Realtime.Loop(sh, url, code, cts.Token);
    }
    public static void Stop() { try { cts?.Cancel(); } catch { } cts = null; }

    static HttpRequestMessage Req(HttpMethod m, string url, string code, string? json = null)
    {
        var r = new HttpRequestMessage(m, url); r.Headers.Add("x-pair", code);
        if (json != null) r.Content = new StringContent(json, Encoding.UTF8, "application/json"); return r;
    }
    // ---- a short alert the phone shows as a notification (battery low, backup failed, download finished...)
    static readonly Dictionary<string, DateTime> alertSeen = new();
    public static void Alert(string title, string body = "")
    {
        try
        {
            if (baseUrl == "" || Cfg.Get("phone_on", "0") != "1") return;
            var key = title + "|" + body; var now = DateTime.Now;
            lock (alertSeen) { if (alertSeen.TryGetValue(key, out var t) && (now - t).TotalMinutes < 10) return; alertSeen[key] = now; if (alertSeen.Count > 50) alertSeen.Clear(); }
            _ = Api(HttpMethod.Post, "/api/alert", new { title, body });
        }
        catch { }
    }

    static async Task<JsonElement?> Api(HttpMethod m, string path, object? body = null)
    {
        try
        {
            using var req = Req(m, baseUrl + path, pair, body == null ? null : JsonSerializer.Serialize(body));
            using var r = await http.SendAsync(req); var s = await r.Content.ReadAsStringAsync();
            using var d = JsonDocument.Parse(s); return d.RootElement.Clone();
        }
        catch { return null; }
    }

    // ---- files from the phone
    static string SaveIncoming(string name, string b64)
    {
        var dir = Path.Combine(Paths.Desktop, "Downloads"); Directory.CreateDirectory(dir);
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)); if (safe.Trim() == "") safe = "file";
        var p = Path.Combine(dir, safe); int n = 1;
        while (File.Exists(p)) p = Path.Combine(dir, Path.GetFileNameWithoutExtension(safe) + " (" + (n++) + ")" + Path.GetExtension(safe));
        File.WriteAllBytes(p, Convert.FromBase64String(b64)); return p;
    }
    // ---- files to the phone (used by the sendfile action)
    public static string Attach(string rel)
    {
        if (Current == null) return "ERR: sendfile only works for requests that come from the phone.";
        try
        {
            if (rel.Trim().ToLowerInvariant() == "screen")
            {
                var jpg = shell?.ShotJpeg(); if (jpg == null) return "ERR: I couldn't capture the screen.";
                Current.AttachName = "screen.jpg"; Current.AttachBytes = jpg; return "Attached a screenshot. It arrives on the phone with your reply.";
            }
            var full = Path.GetFullPath(Path.IsPathRooted(rel) ? rel : Path.Combine(Paths.Root, rel));
            if (!full.StartsWith(Paths.Root, StringComparison.OrdinalIgnoreCase)) return "ERR: I can only send files that are on the USB.";
            if (!File.Exists(full)) return "ERR: I couldn't find that file: " + rel;
            if (new FileInfo(full).Length > 8 * 1024 * 1024) return "ERR: that file is over 8 MB.";
            Current.AttachPath = full; return "Attached " + Path.GetFileName(full) + ". It arrives on the phone with your reply.";
        }
        catch (Exception ex) { return "ERR: " + ex.Message; }
    }

    // ---- full access ("allow everything"): switched on only from the phone app, never by the AI itself ----
    public static bool FullAccess
    {
        get
        {
            if (Cfg.Get("ai_full", "0") != "1") return false;
            if (long.TryParse(Cfg.Get("ai_full_until", "0"), out var u) && u > 0 && DateTime.UtcNow.Ticks > u) { Cfg.Set("ai_full", "0"); Cfg.Set("ai_full_until", "0"); return false; }
            return true;
        }
    }
    // Even with full access these still ask: sending email, and clearly destructive commands
    static readonly Regex StillAsk = new(@"SEND this email|\bformat\s+[a-z]:|\bdiskpart\b|\bbcdedit\b|\b(rd|rmdir)\s+/s\b|\bdel\b[^\r\n]*\s[a-z]:\\\*?\s*$|reg\s+delete|cipher\s+/w", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static string SetFull(string a, DesktopForm sh)
    {
        var p = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool on = p.Length > 0 && p[0].Equals("on", StringComparison.OrdinalIgnoreCase);
        int min = p.Length > 1 && int.TryParse(p[1], out var m) && m >= 0 && m <= 1440 ? m : 60;
        if (!on)
        {
            Cfg.Set("ai_full", "0"); Cfg.Set("ai_full_until", "0"); _ = PostState(); Pet.React("🔒 Full access is off", 4);
            return "🔒 Full access is OFF. The AI asks again before risky things.";
        }
        Cfg.Set("ai_full", "1"); Cfg.Set("ai_full_until", min > 0 ? DateTime.UtcNow.AddMinutes(min).Ticks.ToString() : "0");
        _ = PostState(); Pet.React("🔓 Full access is on", 6); sh.Toast("🔓 Full access is on");
        return "🔓 Full access is ON " + (min > 0 ? "for " + min + " minutes" : "until you turn it off") + ". The AI won't ask before acting, except for sending email and dangerous commands.";
    }

    public static async Task PostState()
    {
        try
        {
            long ms = 0;
            if (long.TryParse(Cfg.Get("ai_full_until", "0"), out var u) && u > 0) ms = new DateTimeOffset(new DateTime(u, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            if (baseUrl != "") await Api(HttpMethod.Post, "/api/state", new { full = FullAccess, until = ms, remote = RemoteInput.Enabled });
        }
        catch { }
    }

    // ---- approvals: ask on the PC and on the phone, first answer wins
    public static bool Approve(string text)
    {
        if (FullAccess && !StillAsk.IsMatch(text)) return true;
        if (Current == null || baseUrl == "") return MessageBox.Show(text, "AI Assistant", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        var id = Guid.NewGuid().ToString("N")[..10];
        _ = Api(HttpMethod.Post, "/api/ask", new { id, text });
        using var f = new Form { Text = "AI Assistant", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(460, 190), MaximizeBox = false, MinimizeBox = false, ControlBox = false, TopMost = true };
        var lbl = new Label { Text = text + "\n\n📱 Also sent to your phone: answer here or there.", Left = 14, Top = 12, Width = 430, Height = 120 };
        var yes = new Button { Text = "Allow", Left = 14, Top = 140, Width = 130, Height = 36, DialogResult = DialogResult.Yes };
        var no = new Button { Text = "Deny", Left = 316, Top = 140, Width = 130, Height = 36, DialogResult = DialogResult.No };
        f.Controls.AddRange(new Control[] { lbl, yes, no }); f.AcceptButton = no; f.CancelButton = no;
        var t = new System.Windows.Forms.Timer { Interval = 1200 }; bool busy = false;
        t.Tick += async (_, _) =>
        {
            if (busy) return; busy = true;
            try
            {
                var j = await Api(HttpMethod.Get, "/api/answer?id=" + id);
                if (j != null && j.Value.TryGetProperty("answer", out var a) && a.ValueKind != JsonValueKind.Null) f.DialogResult = a.GetBoolean() ? DialogResult.Yes : DialogResult.No;
            }
            catch { }
            busy = false;
        };
        t.Start(); var r = f.ShowDialog(); t.Stop();
        _ = Api(HttpMethod.Post, "/api/answer", new { id, allow = r == DialogResult.Yes }); // settles it for the phone (ignored if the phone answered first)
        return r == DialogResult.Yes;
    }

    // ---- publish alarms and homework so the phone can schedule its own notifications
    public static async Task PostStudy()
    {
        try { if (baseUrl != "") await Api(HttpMethod.Post, "/api/study", Study.Snapshot()); } catch { }
    }

    public static async Task PostEvents()
    {
        try
        {
            var ev = new List<object>();
            foreach (var t in Sched.Snapshot())
                if (t.when.StartsWith("daily ") && t.action.StartsWith("remind:")) ev.Add(new { id = "t" + t.id, title = "⏰ Alarm", body = t.action[7..], time = t.when[6..], date = "", every = "day" });
            var today = DateTime.Now.Date;
            foreach (var h in Study.OpenHomework())
            {
                if (!DateTime.TryParseExact(h.due, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var due) || due < today) continue;
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(h.subj + h.text + h.due)))[..8];
                var body = (h.subj == "" ? "" : h.subj + ": ") + h.text;
                ev.Add(new { id = "h" + key, title = "📚 Homework due today", body, time = "07:30", date = due.ToString("yyyy-MM-dd"), every = "" });
                if (due.AddDays(-1) >= today) ev.Add(new { id = "p" + key, title = "📚 Homework due tomorrow", body, time = "18:00", date = due.AddDays(-1).ToString("yyyy-MM-dd"), every = "" });
            }
            // weekly lesson alarms for the phone (the PC rings its own)
            if (Cfg.Get("lesson_alarm", "1") == "1")
            {
                int lead = int.TryParse(Cfg.Get("lesson_lead", "5"), out var ln) && ln >= 1 && ln <= 60 ? ln : 5;
                string[] days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
                foreach (var l in Study.LessonsList())
                {
                    int di = Array.IndexOf(days, l.day); if (di < 0 || !TimeSpan.TryParse(l.time, out var ts)) continue;
                    var at = ts - TimeSpan.FromMinutes(lead); if (at < TimeSpan.Zero) { at += TimeSpan.FromHours(24); di = (di + 6) % 7; }
                    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(l.day + l.time + l.subj)))[..8];
                    ev.Add(new { id = "l" + key, title = "📚 " + l.subj + " in " + lead + " min", body = l.day.ToUpper() + " " + l.time, time = at.ToString(@"hh\:mm"), date = "", every = "week", weekday = di + 1 });
                }
            }
            await Api(HttpMethod.Post, "/api/events", new { events = ev });
        }
        catch { }
    }

    static async Task Loop(DesktopForm sh, string url, string code, CancellationToken ct)
    {
        Status = "Connecting…"; var lastEv = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if ((DateTime.Now - lastEv).TotalMinutes >= 5) { lastEv = DateTime.Now; _ = PostEvents(); _ = PostStudy(); _ = PostState(); }
                using var resp = await http.SendAsync(Req(HttpMethod.Get, url + "/api/poll", code), ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) { Status = "Server said: " + body; await Task.Delay(8000, ct); continue; }
                Status = "Connected";
                using var doc = JsonDocument.Parse(body); var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var idEl)) continue;
                string id = idEl.GetString() ?? "", text = root.GetProperty("text").GetString() ?? "";
                string? savedPath = null;
                if (root.TryGetProperty("file", out var fe) && fe.ValueKind == JsonValueKind.Object)
                {
                    try { savedPath = SaveIncoming(fe.GetProperty("name").GetString() ?? "file", fe.GetProperty("data").GetString() ?? ""); }
                    catch (Exception ex) { await Reply(url, code, id, "I couldn't save that file: " + ex.Message, null, ct); continue; }
                }
                string reply; Ctx? rctx = null;
                if (savedPath != null && text.Trim() == "")
                {
                    reply = "Saved " + Path.GetFileName(savedPath) + " to Downloads on the USB.";
                    sh.BeginInvoke(new Action(() => sh.Toast("📱 Received " + Path.GetFileName(savedPath))));
                }
                else if (text.StartsWith("/"))
                {
                    // quick action from the phone grid: run directly, no AI
                    var qt = new TaskCompletionSource<(string, Ctx)>();
                    sh.BeginInvoke(new Action(async () => { var c = new Ctx { SavedPath = savedPath }; string r; try { r = await Quick.RunAsync(sh, text, c); } catch (Exception ex) { r = "That didn't work: " + ex.Message; } qt.SetResult((r, c)); }));
                    var (qr, qc) = await qt.Task; reply = qr; rctx = qc;
                }
                else
                {
                    var prompt = savedPath != null ? text + "\n(The user also sent a file. It is saved on the USB at Desktop/Downloads/" + Path.GetFileName(savedPath) + ")" : text;
                    var tcs = new TaskCompletionSource<(string, Ctx)>();
                    sh.BeginInvoke(new Action(async () =>
                    {
                        var c = new Ctx(); Current = c;
                        try { if (!sh.DesktopSnapshot().Contains("AI Assistant")) sh.Launch("ai"); tcs.SetResult((await Assistant.Converse(sh, prompt, true, "📱 "), c)); }
                        catch (Exception ex) { tcs.SetResult(("I couldn't answer that: " + ex.Message, c)); }
                        finally { Current = null; }
                    }));
                    var (r, ctx) = await tcs.Task; reply = r; rctx = ctx;
                }
                await Reply(url, code, id, reply, rctx, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Status = "Can't reach the server: " + ex.Message; try { await Task.Delay(8000, ct); } catch { break; } }
        }
        Status = "Off";
    }

    static async Task Reply(string url, string code, string id, string text, Ctx? ctx, CancellationToken ct)
    {
        object? file = null;
        try
        {
            if (ctx?.AttachBytes != null) file = new { name = ctx.AttachName ?? "file", data = Convert.ToBase64String(ctx.AttachBytes) };
            else if (ctx?.AttachPath != null) file = new { name = Path.GetFileName(ctx.AttachPath), data = Convert.ToBase64String(File.ReadAllBytes(ctx.AttachPath)) };
        }
        catch { }
        using var r2 = await http.SendAsync(Req(HttpMethod.Post, url + "/api/reply", code, JsonSerializer.Serialize(new { id, text, file })), ct);
    }

    // Small settings window
    public static void Manage(DesktopForm sh)
    {
        using var f = new Form { Text = "Phone link", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(520, 330), MaximizeBox = false, MinimizeBox = false, TopMost = true };
        Label L(string t, int y) => new() { Text = t, Left = 14, Top = y, Width = 490, Height = 20 };
        var info = new Label { Text = "Talk to this USB from the USB Link phone app. Use the same server address and pairing code in both. Keep USB Desktop open.", Left = 14, Top = 10, Width = 490, Height = 40 };
        var u = new TextBox { Left = 14, Top = 72, Width = 490, Text = Cfg.Get("phone_url") };
        var c = new TextBox { Left = 14, Top = 128, Width = 380, Text = Cfg.Get("phone_code") };
        var gen = new Button { Text = "Make a code", Left = 402, Top = 126, Width = 102, Height = 28 };
        gen.Click += (_, _) => c.Text = Guid.NewGuid().ToString("N")[..16];
        var on = new CheckBox { Text = "Phone link on", Left = 14, Top = 168, Width = 300, Checked = Cfg.Get("phone_on", "0") == "1" };
        var st = new Label { Text = "Status: " + Status, Left = 14, Top = 198, Width = 490, Height = 34 };
        var rem = new CheckBox { Text = "Let the phone move the mouse and type (touchpad + keyboard)", Left = 14, Top = 238, Width = 490, Checked = Cfg.Get("remote_input", "0") == "1" };
        var save = new Button { Text = "Save", Left = 14, Top = 270, Width = 110, Height = 34, DialogResult = DialogResult.OK };
        var close = new Button { Text = "Close", Left = 394, Top = 270, Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => { Cfg.Set("phone_url", u.Text.Trim()); Cfg.Set("phone_code", c.Text.Trim()); Cfg.Set("phone_on", on.Checked ? "1" : "0"); Cfg.Set("remote_input", rem.Checked ? "1" : "0"); Start(sh); _ = PostState(); };
        f.Controls.AddRange(new Control[] { info, L("Server address (from Render)", 52), u, L("Pairing code (8+ characters, keep it secret)", 108), c, gen, on, st, rem, save, close });
        f.AcceptButton = save; f.CancelButton = close; f.ShowDialog();
    }
}
