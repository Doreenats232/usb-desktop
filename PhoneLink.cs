using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Phone link: the USB Link Android app sends commands through a small relay server; this loop fetches them,
// runs them through the assistant (risky actions still ask on this PC) and sends the answer back.
static class PhoneLink
{
    static CancellationTokenSource? cts;
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45) };
    public static string Status = "Off";

    public static void Start(DesktopForm sh)
    {
        Stop();
        if (Cfg.Get("phone_on", "0") != "1") { Status = "Off"; return; }
        var url = Cfg.Get("phone_url").Trim().TrimEnd('/'); var code = Cfg.Get("phone_code").Trim();
        if (url == "" || code.Length < 8) { Status = "Add the server address and an 8+ character code"; return; }
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        cts = new CancellationTokenSource(); _ = Loop(sh, url, code, cts.Token);
    }
    public static void Stop() { try { cts?.Cancel(); } catch { } cts = null; }

    static HttpRequestMessage Req(HttpMethod m, string url, string code, string? json = null)
    {
        var r = new HttpRequestMessage(m, url); r.Headers.Add("x-pair", code);
        if (json != null) r.Content = new StringContent(json, Encoding.UTF8, "application/json"); return r;
    }

    static async Task Loop(DesktopForm sh, string url, string code, CancellationToken ct)
    {
        Status = "Connecting…";
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var resp = await http.SendAsync(Req(HttpMethod.Get, url + "/api/poll", code), ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) { Status = "Server said: " + body; await Task.Delay(8000, ct); continue; }
                Status = "Connected";
                using var doc = JsonDocument.Parse(body); var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var idEl)) continue;
                string id = idEl.GetString() ?? "", text = root.GetProperty("text").GetString() ?? "";
                var tcs = new TaskCompletionSource<string>();
                sh.BeginInvoke(new Action(async () =>
                {
                    try { if (!sh.DesktopSnapshot().Contains("AI Assistant")) sh.Launch("ai"); tcs.SetResult(await Assistant.Converse(sh, text, true, "📱 ")); }
                    catch (Exception ex) { tcs.SetResult("I couldn't answer that: " + ex.Message); }
                }));
                var reply = await tcs.Task;
                using var r2 = await http.SendAsync(Req(HttpMethod.Post, url + "/api/reply", code, JsonSerializer.Serialize(new { id, text = reply })), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Status = "Can't reach the server: " + ex.Message; try { await Task.Delay(8000, ct); } catch { break; } }
        }
        Status = "Off";
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
        var st = new Label { Text = "Status: " + Status, Left = 14, Top = 200, Width = 490, Height = 40 };
        var save = new Button { Text = "Save", Left = 14, Top = 270, Width = 110, Height = 34, DialogResult = DialogResult.OK };
        var close = new Button { Text = "Close", Left = 394, Top = 270, Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => { Cfg.Set("phone_url", u.Text.Trim()); Cfg.Set("phone_code", c.Text.Trim()); Cfg.Set("phone_on", on.Checked ? "1" : "0"); Start(sh); };
        f.Controls.AddRange(new Control[] { info, L("Server address (from Render)", 52), u, L("Pairing code (8+ characters, keep it secret)", 108), c, gen, on, st, save, close });
        f.AcceptButton = save; f.CancelButton = close; f.ShowDialog();
    }
}
