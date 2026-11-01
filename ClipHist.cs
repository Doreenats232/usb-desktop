using System.Text.Json;
namespace UsbDesktop;

// Clipboard history: remembers the last 20 texts you copied (only when you turn it on), kept on the USB in data\clipboard.json.
static class ClipHist
{
    static string F => Path.Combine(Paths.Data, "clipboard.json");
    static readonly List<string> items = new();
    static string last = "";

    public static void Start(DesktopForm sh)
    {
        try { if (File.Exists(F)) items.AddRange(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(F)) ?? new()); } catch { }
        var t = new System.Windows.Forms.Timer { Interval = 1500 };
        t.Tick += (_, _) => Poll();
        t.Start();
    }

    static void Save() { try { Directory.CreateDirectory(Paths.Data); File.WriteAllText(F, JsonSerializer.Serialize(items)); } catch { } }

    static void Poll()
    {
        if (Cfg.Get("clip_hist", "0") != "1") return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            var s = Clipboard.GetText();
            if (s == "" || s == last || s.Length > 2000) return;
            last = s; items.Remove(s); items.Insert(0, s);
            if (items.Count > 20) items.RemoveRange(20, items.Count - 20);
            Save();
        }
        catch { }   // another program is using the clipboard right now: try again next time
    }

    static string One(string s) { s = s.Replace("\r", "").Replace("\n", " ⏎ "); return s.Length > 300 ? s[..300] + "…" : s; }

    // list | search WORD | use N | clear | on | off
    public static string Run(string a)
    {
        var i = a.IndexOf(' '); var cmd = (i > 0 ? a[..i] : a).Trim().ToLowerInvariant(); var arg = i > 0 ? a[(i + 1)..].Trim() : "";
        try
        {
            switch (cmd)
            {
                case "on": Cfg.Set("clip_hist", "1"); return "Clipboard history is on. I'll remember the last 20 things you copy (kept only on this USB).";
                case "off": Cfg.Set("clip_hist", "0"); return "Clipboard history is off. (Say clear to delete what is saved.)";
                case "clear": items.Clear(); last = ""; Save(); return "Clipboard history cleared.";
                case "use":
                    if (int.TryParse(arg, out var n) && n >= 1 && n <= items.Count) { Clipboard.SetText(items[n - 1]); last = items[n - 1]; return "Put item " + n + " back on the clipboard."; }
                    return "There is no item " + arg + ".";
                default:
                    {
                        if (Cfg.Get("clip_hist", "0") != "1") return "Clipboard history is off. Say \"turn on clipboard history\" first.";
                        var list = items.Select((s, k) => (s, k)).Where(x => cmd != "search" || arg == "" || x.s.Contains(arg, StringComparison.OrdinalIgnoreCase)).Take(20).ToList();
                        if (list.Count == 0) return cmd == "search" ? "Nothing in the history matches \"" + arg + "\"." : "The clipboard history is empty so far.";
                        return "🕘 Clipboard history\n" + string.Join("\n", list.Select(x => (x.k + 1) + ". " + One(x.s)));
                    }
            }
        }
        catch (Exception ex) { return "Clipboard history failed: " + ex.Message; }
    }
}
