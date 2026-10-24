using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Lets the assistant use your own accounts (Spotify, Gmail, Google Calendar). You make a free key on each service once and press Connect;
// sign-in happens in your normal browser and only the sign-in tokens are kept on the USB (never in vault backups).
static class Accounts
{
    const int Port = 53682;
    class Svc { public string Id = "", Name = "", Auth = "", TokenUrl = "", Scope = "", Console = "", Help = ""; public bool Secret; }
    static readonly Svc[] All =
    {
        new() { Id = "spotify", Name = "Spotify", Auth = "https://accounts.spotify.com/authorize", TokenUrl = "https://accounts.spotify.com/api/token", Secret = false, Console = "https://developer.spotify.com/dashboard",
            Scope = "user-read-playback-state user-modify-playback-state user-read-currently-playing",
            Help = "1  Open the Spotify dashboard (button below) and log in.\n2  Press Create app. Any name. For Redirect URI type exactly:\n    http://127.0.0.1:53682/callback\n3  Tick Web API, save, then copy the Client ID here.\nControlling playback needs Spotify Premium, and Spotify must be open on a device." },
        new() { Id = "google", Name = "Google (Gmail + Calendar)", Auth = "https://accounts.google.com/o/oauth2/v2/auth", TokenUrl = "https://oauth2.googleapis.com/token", Secret = true, Console = "https://console.cloud.google.com/apis/credentials",
            Scope = "https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/gmail.send https://www.googleapis.com/auth/calendar.readonly",
            Help = "1  Open the Google Cloud console (button below), make a project, and enable the Gmail API and Google Calendar API.\n2  OAuth consent screen: choose External, add your own Gmail as a Test user.\n3  Credentials > Create credentials > OAuth client ID > Desktop app.\n4  Copy the Client ID and Client secret here. When Google says the app is unverified, press Advanced > Continue (it is your own app).\nIn Testing mode Google asks you to reconnect about once a week." },
    };
    static Svc Find(string id) => All.First(x => x.Id == id);
    public static bool Connected(string id) => Cfg.Get($"acct_{id}_tok_key") != "";
    static string B64Url(byte[] b) => Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    // ---------- sign-in ----------
    static async Task<JsonElement> PostForm(string url, Dictionary<string, string> form)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        using var resp = await Net.Http.SendAsync(req); var b = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception("Sign-in failed: " + (b.Length > 240 ? b[..240] : b));
        return JsonDocument.Parse(b).RootElement.Clone();
    }
    static void SaveTok(Svc s, JsonElement j)
    {
        string old = ""; try { old = JsonDocument.Parse(Cfg.Get($"acct_{s.Id}_tok_key")).RootElement.GetProperty("r").GetString() ?? ""; } catch { }
        string a = j.GetProperty("access_token").GetString() ?? "", r = old;
        if (j.TryGetProperty("refresh_token", out var rv) && rv.ValueKind == JsonValueKind.String) r = rv.GetString() ?? old;
        int secs = j.TryGetProperty("expires_in", out var ev) && ev.TryGetInt32(out var sc) ? sc : 3600;
        Cfg.Set($"acct_{s.Id}_tok_key", JsonSerializer.Serialize(new { a, r, e = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + secs - 60 }));
    }
    static async Task Connect(Svc s)
    {
        var cid = Cfg.Get($"acct_{s.Id}_cid"); if (cid == "") throw new Exception("Paste the Client ID first.");
        if (s.Secret && Cfg.Get($"acct_{s.Id}_secret_key") == "") throw new Exception("Paste the Client secret first.");
        var verifier = B64Url(RandomNumberGenerator.GetBytes(48)); var challenge = B64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N"); var redirect = "http://127.0.0.1:" + Port + "/callback";
        var url = s.Auth + "?response_type=code&client_id=" + Uri.EscapeDataString(cid) + "&redirect_uri=" + Uri.EscapeDataString(redirect) + "&scope=" + Uri.EscapeDataString(s.Scope) + "&state=" + state + "&code_challenge_method=S256&code_challenge=" + challenge + (s.Id == "google" ? "&access_type=offline&prompt=consent" : "");
        using var lis = new HttpListener(); lis.Prefixes.Add("http://127.0.0.1:" + Port + "/");
        try { lis.Start(); } catch (Exception ex) { throw new Exception("Couldn't listen for the sign-in reply (" + ex.Message + "). Close other programs using port " + Port + " and try again."); }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        var ctxTask = lis.GetContextAsync();
        if (await Task.WhenAny(ctxTask, Task.Delay(TimeSpan.FromMinutes(3))) != ctxTask) throw new Exception("Timed out waiting for you to approve in the browser.");
        var ctx = await ctxTask; var q = ctx.Request.QueryString; string code = q["code"] ?? "";
        var page = Encoding.UTF8.GetBytes("<html><body style='font-family:sans-serif;text-align:center;margin-top:20vh'><h2>" + (code != "" ? "Connected! You can close this tab and go back to USB Desktop." : "Not connected: " + (q["error"] ?? "no code received")) + "</h2></body></html>");
        ctx.Response.ContentType = "text/html; charset=utf-8"; ctx.Response.OutputStream.Write(page, 0, page.Length); ctx.Response.Close();
        if (q["state"] != state || code == "") throw new Exception("The sign-in was cancelled or did not match. Try again.");
        var form = new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["client_id"] = cid, ["code_verifier"] = verifier };
        if (s.Secret) form["client_secret"] = Cfg.Get($"acct_{s.Id}_secret_key");
        SaveTok(s, await PostForm(s.TokenUrl, form));
    }
    static async Task<string> Token(Svc s)
    {
        var raw = Cfg.Get($"acct_{s.Id}_tok_key"); if (raw == "") throw new Exception(s.Name + " is not connected. Open the assistant's Settings > Accounts and press Connect.");
        var j = JsonDocument.Parse(raw).RootElement; string a = j.GetProperty("a").GetString() ?? "", r = j.GetProperty("r").GetString() ?? ""; long exp = j.GetProperty("e").GetInt64();
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() < exp) return a;
        if (r == "") throw new Exception(s.Name + " sign-in expired. Press Connect again in Settings > Accounts.");
        var form = new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = r, ["client_id"] = Cfg.Get($"acct_{s.Id}_cid") };
        if (s.Secret) form["client_secret"] = Cfg.Get($"acct_{s.Id}_secret_key");
        try { var nj = await PostForm(s.TokenUrl, form); SaveTok(s, nj); return nj.GetProperty("access_token").GetString() ?? ""; }
        catch (Exception ex) { throw new Exception(s.Name + " sign-in expired (" + ex.Message + "). Press Connect again in Settings > Accounts."); }
    }
    static async Task<JsonElement> Api(Svc s, HttpMethod m, string url, string? json = null)
    {
        var tok = await Token(s); using var req = new HttpRequestMessage(m, url); req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok);
        if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await Net.Http.SendAsync(req); var b = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            string msg = b.Length > 240 ? b[..240] : b;
            try { var je = JsonDocument.Parse(b).RootElement; if (je.TryGetProperty("error", out var x)) msg = x.ValueKind == JsonValueKind.Object && x.TryGetProperty("message", out var mm) ? mm.GetString() ?? msg : x.ToString(); } catch { }
            throw new Exception((int)resp.StatusCode + ": " + msg);
        }
        return b.Trim() == "" ? JsonDocument.Parse("{}").RootElement.Clone() : JsonDocument.Parse(b).RootElement.Clone();
    }

    // ---------- what the assistant can do ----------
    public static async Task<string> Run(string kind, string a, bool trust, Func<string, bool> yes)
    {
        try
        {
            if (kind == "spotify") return await Spot(a);
            var g = Find("google");
            if (kind == "cal") { if (!trust && !yes("The assistant wants to look at your Google Calendar. Allow?")) return "The user did not allow reading the calendar."; return await Cal(g, a); }
            var i = a.IndexOf(' '); var cmd = (i > 0 ? a[..i] : a).Trim().ToLowerInvariant(); var arg = i > 0 ? a[(i + 1)..].Trim() : "";
            if (cmd == "send")
            {
                var p = arg.Split('|', 3); if (p.Length < 3) return "mail send needs: to|subject|body";
                if (!yes("The assistant wants to SEND this email from your Gmail:\n\nTo: " + p[0].Trim() + "\nSubject: " + p[1].Trim() + "\n\n" + p[2].Trim() + "\n\nSend it?")) return "The user did not allow sending the email.";
                var raw = "To: " + p[0].Trim() + "\r\nSubject: =?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(p[1].Trim())) + "?=\r\nContent-Type: text/plain; charset=UTF-8\r\n\r\n" + p[2].Trim();
                await Api(g, HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send", JsonSerializer.Serialize(new { raw = B64Url(Encoding.UTF8.GetBytes(raw)) }));
                return "Email sent to " + p[0].Trim() + ".";
            }
            if (!trust && !yes("The assistant wants to read your Gmail (" + (cmd == "read" ? "open one email" : "look through your inbox") + "). Allow?")) return "The user did not allow reading email.";
            if (cmd == "unread") return await MailList(g, "is:unread in:inbox");
            if (cmd == "search") return await MailList(g, arg);
            if (cmd == "read") return await MailRead(g, arg);
            return "Unknown mail command. Use unread, search words, read ID or send to|subject|body.";
        }
        catch (Exception ex) { return kind + " failed: " + ex.Message; }
    }
    static string Hdr(JsonElement m, string name)
    {
        foreach (var h in m.GetProperty("payload").GetProperty("headers").EnumerateArray()) if (string.Equals(h.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase)) return h.GetProperty("value").GetString() ?? "";
        return "";
    }
    static async Task<string> MailList(Svc g, string q)
    {
        var l = await Api(g, HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults=8&q=" + Uri.EscapeDataString(q));
        if (!l.TryGetProperty("messages", out var ms) || ms.GetArrayLength() == 0) return "No emails found for: " + q;
        var sb = new StringBuilder();
        foreach (var mm in ms.EnumerateArray())
        {
            var id = mm.GetProperty("id").GetString()!;
            var m = await Api(g, HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages/" + id + "?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date");
            sb.Append("id ").Append(id).Append(" | ").Append(Hdr(m, "From")).Append(" | ").Append(Hdr(m, "Subject")).Append(" | ").Append(Hdr(m, "Date")).Append('\n');
        }
        return sb.ToString();
    }
    static string Body(JsonElement part)
    {
        if (part.TryGetProperty("mimeType", out var mt) && mt.GetString() == "text/plain" && part.TryGetProperty("body", out var bd) && bd.TryGetProperty("data", out var dt))
        {
            var t = (dt.GetString() ?? "").Replace('-', '+').Replace('_', '/'); t = t.PadRight(t.Length + (4 - t.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(t));
        }
        if (part.TryGetProperty("parts", out var ps)) foreach (var p in ps.EnumerateArray()) { var r = Body(p); if (r != "") return r; }
        return "";
    }
    static async Task<string> MailRead(Svc g, string id)
    {
        if (id == "") return "mail read needs an email id from a previous list.";
        var m = await Api(g, HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages/" + Uri.EscapeDataString(id) + "?format=full");
        var body = Body(m.GetProperty("payload")); if (body.Length > 3000) body = body[..3000] + "\n[cut off]"; if (body.Trim() == "") body = "(this email has no plain text part)";
        return "From: " + Hdr(m, "From") + "\nSubject: " + Hdr(m, "Subject") + "\nDate: " + Hdr(m, "Date") + "\n\n" + body;
    }
    static async Task<string> Cal(Svc g, string a)
    {
        bool week = a.Trim().ToLowerInvariant() == "week"; var from = DateTime.Now.Date; var to = from.AddDays(week ? 7 : 1);
        var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events?singleEvents=true&orderBy=startTime&maxResults=15&timeMin=" + Uri.EscapeDataString(new DateTimeOffset(from).ToString("o")) + "&timeMax=" + Uri.EscapeDataString(new DateTimeOffset(to).ToString("o"));
        var j = await Api(g, HttpMethod.Get, url);
        if (!j.TryGetProperty("items", out var it) || it.GetArrayLength() == 0) return week ? "No events this week." : "No events today.";
        var sb = new StringBuilder();
        foreach (var e in it.EnumerateArray())
        {
            var st = e.GetProperty("start"); string at = st.TryGetProperty("dateTime", out var dt) ? DateTime.Parse(dt.GetString()!).ToString("ddd HH:mm") : st.TryGetProperty("date", out var dd) ? (dd.GetString() ?? "") + " (all day)" : "";
            sb.Append(at).Append(" - ").Append(e.TryGetProperty("summary", out var sm) ? sm.GetString() : "(no title)").Append('\n');
        }
        return sb.ToString();
    }
    static async Task<string> Spot(string a)
    {
        var s = Find("spotify"); var i = a.IndexOf(' '); var cmd = (i > 0 ? a[..i] : a).Trim().ToLowerInvariant(); var arg = i > 0 ? a[(i + 1)..].Trim() : ""; const string B = "https://api.spotify.com/v1";
        try
        {
            switch (cmd)
            {
                case "play":
                    if (arg == "") { await Api(s, HttpMethod.Put, B + "/me/player/play"); return "Resumed Spotify."; }
                    var sr = await Api(s, HttpMethod.Get, B + "/search?type=track&limit=1&q=" + Uri.EscapeDataString(arg)); var items = sr.GetProperty("tracks").GetProperty("items");
                    if (items.GetArrayLength() == 0) return "Spotify found nothing for " + arg + ".";
                    var tr = items[0]; await Api(s, HttpMethod.Put, B + "/me/player/play", JsonSerializer.Serialize(new { uris = new[] { tr.GetProperty("uri").GetString() } }));
                    return "Playing " + tr.GetProperty("name").GetString() + " by " + tr.GetProperty("artists")[0].GetProperty("name").GetString() + ".";
                case "resume": await Api(s, HttpMethod.Put, B + "/me/player/play"); return "Resumed.";
                case "pause": await Api(s, HttpMethod.Put, B + "/me/player/pause"); return "Paused.";
                case "next": await Api(s, HttpMethod.Post, B + "/me/player/next"); return "Skipped to the next song.";
                case "previous": await Api(s, HttpMethod.Post, B + "/me/player/previous"); return "Went back a song.";
                case "volume": { int v = int.TryParse(arg, out var vv) ? Math.Clamp(vv, 0, 100) : 50; await Api(s, HttpMethod.Put, B + "/me/player/volume?volume_percent=" + v); return "Volume set to " + v + "%."; }
                case "now":
                    {
                        var np = await Api(s, HttpMethod.Get, B + "/me/player/currently-playing");
                        if (!np.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return "Nothing is playing on Spotify right now.";
                        return "Now playing " + item.GetProperty("name").GetString() + " by " + item.GetProperty("artists")[0].GetProperty("name").GetString() + ".";
                    }
                default: return "Unknown Spotify command. Use play song name, pause, resume, next, previous, volume N or now.";
            }
        }
        catch (Exception ex) when (ex.Message.Contains("NO_ACTIVE_DEVICE") || ex.Message.StartsWith("404")) { return "Spotify has no active device. Open Spotify on this PC or your phone, play anything once, then try again."; }
        catch (Exception ex) when (ex.Message.Contains("PREMIUM")) { return "Spotify only lets apps control playback on Premium accounts."; }
    }

    // ---------- the setup window ----------
    public static void Show()
    {
        using var f = new Form { Text = "Accounts", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(Ui.D(560), Ui.D(600)), BackColor = Ui.Surface, ForeColor = Ui.Text, Font = Ui.F(10) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(Ui.D(12)) }; f.Controls.Add(flow);
        flow.Controls.Add(new Label { Text = "Let the assistant use your accounts", Font = Ui.F(13, true), AutoSize = true });
        flow.Controls.Add(new Label { Text = "You make a free key on each service once, then press Connect. Sign-in happens in your normal browser. The assistant asks you before it reads email or sends anything.", Width = Ui.D(510), Height = Ui.D(50), ForeColor = Ui.Sub });
        foreach (var s in All)
        {
            var box = new Panel { Width = Ui.D(510), Height = Ui.D(s.Secret ? 410 : 350), BackColor = Ui.Surface2, Margin = new Padding(0, Ui.D(6), 0, Ui.D(6)) };
            var status = new Label { Left = Ui.D(12), Top = Ui.D(10), AutoSize = true, Font = Ui.F(11, true) };
            var help = new Label { Left = Ui.D(12), Top = Ui.D(40), Width = Ui.D(486), Height = Ui.D(s.Secret ? 150 : 120), Text = s.Help, ForeColor = Ui.Sub };
            int y = Ui.D(s.Secret ? 196 : 166);
            var site = new Button { Text = "Open the " + s.Name.Split(' ')[0] + " website", Left = Ui.D(12), Top = y, Width = Ui.D(230), Height = Ui.D(30) }; site.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(s.Console) { UseShellExecute = true }); } catch { } };
            var l1 = new Label { Text = "Client ID", Left = Ui.D(12), Top = y + Ui.D(40), AutoSize = true };
            var cid = new TextBox { Left = Ui.D(12), Top = y + Ui.D(62), Width = Ui.D(486), Text = Cfg.Get($"acct_{s.Id}_cid") };
            TextBox? sec = null; int y2 = y + Ui.D(100);
            if (s.Secret) { box.Controls.Add(new Label { Text = "Client secret", Left = Ui.D(12), Top = y2, AutoSize = true }); sec = new TextBox { Left = Ui.D(12), Top = y2 + Ui.D(22), Width = Ui.D(486), UseSystemPasswordChar = true, Text = Cfg.Get($"acct_{s.Id}_secret_key") }; box.Controls.Add(sec); y2 += Ui.D(60); }
            var conn = new Button { Text = "Connect", Left = Ui.D(12), Top = y2, Width = Ui.D(200), Height = Ui.D(34) };
            var disc = new Button { Text = "Disconnect", Left = Ui.D(222), Top = y2, Width = Ui.D(120), Height = Ui.D(34) };
            void Upd() { bool on = Connected(s.Id); status.Text = (on ? "✅  " : "❌  ") + s.Name + (on ? "  ·  connected" : "  ·  not connected"); status.ForeColor = on ? Color.FromArgb(110, 231, 160) : Color.FromArgb(240, 120, 130); disc.Enabled = on; }
            conn.Click += async (_, _) =>
            {
                try { Cfg.Set($"acct_{s.Id}_cid", cid.Text.Trim()); if (sec != null) Cfg.Set($"acct_{s.Id}_secret_key", sec.Text.Trim()); conn.Enabled = false; conn.Text = "Waiting for your browser…"; await Connect(s); }
                catch (Exception ex) { MessageBox.Show("❌ " + ex.Message, "Accounts", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { conn.Enabled = true; conn.Text = "Connect"; Upd(); }
            };
            disc.Click += (_, _) => { Cfg.Set($"acct_{s.Id}_tok_key", ""); Upd(); };
            box.Controls.AddRange(new Control[] { status, help, site, l1, cid, conn, disc }); flow.Controls.Add(box); Upd();
        }
        f.ShowDialog();
    }
}
