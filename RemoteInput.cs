using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Remote touchpad and keyboard for the phone app. The phone sends small batches of mouse/keyboard events through the relay.
// It only works while "Let the phone move the mouse and type" is ticked in the Phone link window ON THE PC, and the relay
// drops events that are older than a few seconds so nothing old is ever replayed.
static class RemoteInput
{
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern short VkKeyScan(char ch);

    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(40) };
    public static bool Enabled => Cfg.Get("remote_input", "0") == "1";
    static bool held; static DateTime lastEvt = DateTime.MinValue;

    public static async Task Loop(DesktopForm sh, string url, string code, CancellationToken ct)
    {
        var lastBeat = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (held && (DateTime.Now - lastEvt).TotalSeconds > 6) { Drag(false); held = false; }   // never leave a mouse button stuck down
                if (!Enabled) { await Task.Delay(3000, ct); continue; }
                using var req = new HttpRequestMessage(HttpMethod.Get, url + "/api/input/poll"); req.Headers.Add("x-pair", code);
                using var resp = await http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) { await Task.Delay(5000, ct); continue; }
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("events", out var evs) || evs.ValueKind != JsonValueKind.Array) continue;
                if (!Enabled) continue;
                if ((DateTime.Now - lastBeat).TotalSeconds > 60) { try { sh.BeginInvoke(new Action(() => Pet.React("📱 Phone is controlling the PC", 4))); } catch { } }
                lastBeat = DateTime.Now; lastEvt = DateTime.Now;
                int n = 0;
                foreach (var e in evs.EnumerateArray()) { if (++n > 80) break; try { Apply(sh, e); } catch { } }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch { try { await Task.Delay(4000, ct); } catch { break; } }
        }
        if (held) { try { Drag(false); } catch { } held = false; }
    }

    static double Num(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : 0;
    static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    static void Apply(DesktopForm sh, JsonElement e)
    {
        switch (Str(e, "t"))
        {
            case "m": Move(Num(e, "dx"), Num(e, "dy")); break;
            case "c": Click(Str(e, "b"), (int)Math.Clamp(Num(e, "n"), 1, 3)); break;
            case "d": { bool down = Str(e, "s") == "down"; Drag(down); held = down; break; }
            case "s": Wheel((int)Math.Clamp(Num(e, "dy"), -40, 40)); break;
            case "t": TypeText(sh, Str(e, "text")); break;
            case "k": Combo(Str(e, "k")); break;
        }
    }

    static void Move(double dx, double dy)
    {
        dx = Math.Clamp(dx, -2500, 2500); dy = Math.Clamp(dy, -2500, 2500);
        var p = Cursor.Position; var vs = SystemInformation.VirtualScreen;
        int x = (int)Math.Clamp(p.X + dx, vs.Left, vs.Right - 1), y = (int)Math.Clamp(p.Y + dy, vs.Top, vs.Bottom - 1);
        Cursor.Position = new Point(x, y);
    }
    static void Click(string b, int n)
    {
        uint down = b == "r" ? 0x0008u : b == "m" ? 0x0020u : 0x0002u, up = b == "r" ? 0x0010u : b == "m" ? 0x0040u : 0x0004u;
        for (int i = 0; i < n; i++) { mouse_event(down, 0, 0, 0, UIntPtr.Zero); mouse_event(up, 0, 0, 0, UIntPtr.Zero); if (i < n - 1) Thread.Sleep(40); }
    }
    static void Drag(bool down) => mouse_event(down ? 0x0002u : 0x0004u, 0, 0, 0, UIntPtr.Zero);
    static void Wheel(int notches) { if (notches != 0) mouse_event(0x0800u, 0, 0, notches * 120, UIntPtr.Zero); }

    static void Down(byte vk, bool ext) => keybd_event(vk, 0, ext ? 1u : 0u, UIntPtr.Zero);
    static void Up(byte vk, bool ext) => keybd_event(vk, 0, (ext ? 1u : 0u) | 2u, UIntPtr.Zero);
    static void KeyPress(byte vk, bool shift, bool ctrl, bool alt, bool win = false, bool ext = false)
    {
        if (win) Down(0x5B, true); if (ctrl) Down(0x11, false); if (alt) Down(0x12, false); if (shift) Down(0x10, false);
        if (vk != 0) { Down(vk, ext); Up(vk, ext); }
        if (shift) Up(0x10, false); if (alt) Up(0x12, false); if (ctrl) Up(0x11, false); if (win) Up(0x5B, true);
    }

    // types real characters (works on any keyboard layout VkKeyScan knows); anything else (emoji, odd symbols) is pasted
    static void TypeText(DesktopForm sh, string text)
    {
        if (text.Length > 400) text = text[..400];
        foreach (var r in text.EnumerateRunes())
        {
            if (r.Value == '\r') continue;
            if (r.Value == '\n') { KeyPress(0x0D, false, false, false); continue; }
            if (r.Value == '\t') { KeyPress(0x09, false, false, false); continue; }
            short sc = r.IsBmp ? VkKeyScan((char)r.Value) : (short)-1;
            if (sc == -1) { PasteText(sh, r.ToString()); continue; }
            int st = (sc >> 8) & 0xFF;
            KeyPress((byte)(sc & 0xFF), (st & 1) != 0, (st & 2) != 0, (st & 4) != 0);
        }
    }
    static void PasteText(DesktopForm sh, string s)
    {
        try
        {
            sh.Invoke(new Action(() =>
            {
                string? old = null; try { if (Clipboard.ContainsText()) old = Clipboard.GetText(); } catch { }
                try { Clipboard.SetText(s); } catch { return; }
                KeyPress(0x56, false, true, false); Thread.Sleep(150);
                try { if (old != null) Clipboard.SetText(old); } catch { }
            }));
        }
        catch { }
    }

    static readonly Dictionary<string, (byte vk, bool ext)> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = (0x0D, false), ["backspace"] = (0x08, false), ["tab"] = (0x09, false), ["esc"] = (0x1B, false), ["space"] = (0x20, false),
        ["left"] = (0x25, true), ["up"] = (0x26, true), ["right"] = (0x27, true), ["down"] = (0x28, true),
        ["delete"] = (0x2E, true), ["home"] = (0x24, true), ["end"] = (0x23, true), ["pageup"] = (0x21, true), ["pagedown"] = (0x22, true),
    };
    // "enter", "ctrl+c", "alt+tab", "win", "win+d", "f5" ...
    static void Combo(string k)
    {
        if (k.Length == 0 || k.Length > 30) return;
        bool shift = false, ctrl = false, alt = false, win = false, ext = false; byte vk = 0;
        foreach (var part in k.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim().ToLowerInvariant();
            if (p == "ctrl") ctrl = true; else if (p == "shift") shift = true; else if (p == "alt") alt = true; else if (p == "win") win = true;
            else if (Keys.TryGetValue(p, out var kv)) { vk = kv.vk; ext = kv.ext; }
            else if (p.Length == 1 && char.IsAsciiLetterOrDigit(p[0])) vk = (byte)char.ToUpperInvariant(p[0]);
            else if (p.Length >= 2 && p[0] == 'f' && int.TryParse(p[1..], out var fn) && fn >= 1 && fn <= 12) vk = (byte)(0x70 + fn - 1);
            else return;
        }
        if (vk == 0 && !win) return;
        KeyPress(vk, shift, ctrl, alt, win, ext);
    }
}
