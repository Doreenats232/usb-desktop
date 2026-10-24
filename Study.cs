using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Study helper for the assistant: homework list with due dates, a weekly timetable, and a shortcut to your StudyFeed site. Everything is a plain text file on the USB.
static class Study
{
    static string F => Path.Combine(Paths.Data, "study.txt");
    class Hw { public string Subj = "", Text = "", Due = ""; public bool Done; }
    static List<Hw> LoadHw() => File.Exists(F) ? File.ReadAllLines(F).Where(l => l.StartsWith("hw|")).Select(l => l.Split('|')).Where(p => p.Length >= 5).Select(p => new Hw { Subj = p[1], Text = p[2], Due = p[3], Done = p[4] == "1" }).ToList() : new();
    static Dictionary<string, string> LoadTt() => File.Exists(F) ? File.ReadAllLines(F).Where(l => l.StartsWith("tt|")).Select(l => l.Split('|', 3)).Where(p => p.Length == 3).ToDictionary(p => p[1], p => p[2]) : new();
    static void Save(List<Hw> hw, Dictionary<string, string> tt)
    {
        var lines = hw.Select(h => "hw|" + h.Subj.Replace('|', '/') + "|" + h.Text.Replace('|', '/') + "|" + h.Due + "|" + (h.Done ? "1" : "0")).Concat(tt.Select(k => "tt|" + k.Key + "|" + k.Value.Replace('|', '/')));
        Directory.CreateDirectory(Paths.Data); File.WriteAllLines(F, lines);
    }
    static string Day(string s)
    {
        s = s.Trim().ToLowerInvariant(); foreach (var d in new[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun" }) if (s.StartsWith(d)) return d; return "";
    }
    public static string Run(string kind, string a, DesktopForm sh)
    {
        try
        {
            if (kind == "studyfeed")
            {
                if (a.StartsWith("set ", StringComparison.OrdinalIgnoreCase)) { Cfg.Set("studyfeed_url", a[4..].Trim()); return "Saved your StudyFeed address."; }
                var u = Cfg.Get("studyfeed_url"); if (u == "") return "No StudyFeed address saved yet. Ask the user for it, then use studyfeed set ADDRESS.";
                sh.Launch("web", u); return "Opened StudyFeed.";
            }
            var hw = LoadHw(); var tt = LoadTt(); var i = a.IndexOf(' '); var cmd = (i > 0 ? a[..i] : a).Trim().ToLowerInvariant(); var arg = i > 0 ? a[(i + 1)..].Trim() : "";
            if (kind == "timetable")
            {
                if (cmd == "set")
                {
                    var p = arg.Split('|', 2); var d = Day(p[0]); if (d == "" || p.Length < 2) return "timetable set needs a day and lessons, like: timetable set Mon|Maths, English, Science";
                    tt[d] = p[1].Trim(); Save(hw, tt); return "Saved " + d.ToUpper() + ": " + p[1].Trim();
                }
                var want = cmd == "today" || cmd == "" ? DateTime.Now.ToString("ddd").ToLowerInvariant()[..3] : cmd == "tomorrow" ? DateTime.Now.AddDays(1).ToString("ddd").ToLowerInvariant()[..3] : Day(cmd);
                if (cmd == "show" || cmd == "week") return tt.Count == 0 ? "No timetable saved yet." : string.Join("\n", new[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun" }.Where(tt.ContainsKey).Select(d => d.ToUpper() + ": " + tt[d]));
                return tt.TryGetValue(want, out var l) ? want.ToUpper() + ": " + l : "Nothing saved for " + (want == "" ? "that day" : want.ToUpper()) + ".";
            }
            // homework
            switch (cmd)
            {
                case "add":
                    { var p = arg.Split('|', 2); var txt = p[0].Trim(); if (txt == "") return "homework add needs the task, like: homework add Maths exercise 5|2026-10-28";
                      var due = p.Length > 1 ? p[1].Trim() : ""; if (due != "" && !DateTime.TryParse(due, out var dd)) return "I couldn't read the due date. Use year-month-day."; else if (due != "") due = DateTime.Parse(due).ToString("yyyy-MM-dd");
                      var sp = txt.IndexOf(' '); hw.Add(new Hw { Subj = sp > 0 ? txt[..sp] : txt, Text = sp > 0 ? txt[(sp + 1)..] : "", Due = due }); Save(hw, tt); return "Added: " + txt + (due != "" ? " (due " + due + ")" : ""); }
                case "done":
                    { var open = hw.Where(h => !h.Done).ToList(); if (int.TryParse(arg, out var n) && n >= 1 && n <= open.Count) { open[n - 1].Done = true; Save(hw, tt); return "Ticked off: " + open[n - 1].Subj + " " + open[n - 1].Text; } return "Tell me the number from the homework list."; }
                case "clear": hw.RemoveAll(h => h.Done); Save(hw, tt); return "Cleared the finished homework.";
                default:
                    {
                        var open = hw.Where(h => !h.Done).OrderBy(h => h.Due == "" ? "9999" : h.Due).ToList(); if (open.Count == 0) return "No homework waiting. 🎉";
                        // numbers must match what "done N" uses, which counts the saved order
                        var saved = hw.Where(h => !h.Done).ToList(); var today = DateTime.Now.ToString("yyyy-MM-dd");
                        return string.Join("\n", open.Select(h => (saved.IndexOf(h) + 1) + ". " + h.Subj + " " + h.Text + (h.Due == "" ? "" : "  (due " + h.Due + (string.CompareOrdinal(h.Due, today) < 0 ? ", OVERDUE" : h.Due == today ? ", TODAY" : "") + ")")));
                    }
            }
        }
        catch (Exception ex) { return kind + " failed: " + ex.Message; }
    }
    // ---- StudyFeed (your live site): today's quiz questions, answers on request, simple explanations ----
    const string FeedUrl = "https://studyfeed-6v3c.onrender.com";
    static List<(string id, string subj, string text)> lastFeed = new();
    static string Str(JsonElement p, string k) => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public static async Task<string> Feed(string a, DesktopForm sh)
    {
        try
        {
            var u = Cfg.Get("studyfeed_url"); if (u == "") { u = FeedUrl; Cfg.Set("studyfeed_url", u); } u = u.TrimEnd('/');
            var i = a.IndexOf(' '); var cmd = (i > 0 ? a[..i] : a).Trim().ToLowerInvariant(); var arg = i > 0 ? a[(i + 1)..].Trim() : "";
            if (cmd == "set") { Cfg.Set("studyfeed_url", arg); return "Saved your StudyFeed address."; }
            if (cmd == "" || cmd == "open") { sh.Launch("web", u); return "Opened StudyFeed."; }
            if (cmd is "feed" or "today" or "quiz")
            {
                var arr = JsonDocument.Parse(await Net.Text(u + "/api/feed")).RootElement; var list = new List<(string id, string subj, string text)>();
                foreach (var p in arr.EnumerateArray())
                {
                    string type = Str(p, "type"), subj = Str(p, "subject"), text = Str(p, "text"), pid = Str(p, "id");
                    if (type != "quiz" && type != "question") continue;
                    if (cmd == "today") { long ts = p.TryGetProperty("ts", out var tv) && tv.TryGetInt64(out var t0) ? t0 : 0; if (DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime.Date != DateTime.Now.Date) continue; }
                    else if (arg != "" && !subj.Contains(arg, StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add((pid, subj, text)); if (list.Count >= (cmd == "today" ? 16 : 8)) break;
                }
                if (list.Count == 0) return cmd == "today" ? "No new StudyFeed questions today yet." : "No StudyFeed questions found" + (arg != "" ? " for " + arg : "") + ".";
                lastFeed = list;
                return string.Join("\n", list.Select((x, n) => (n + 1) + ". [" + x.subj + "] " + x.text)) + "\n(Answers stay hidden until the user asks; then use studyfeed answer N.)";
            }
            if (cmd == "answer")
            {
                if (!int.TryParse(arg, out var n) || n < 1 || n > lastFeed.Count) return "List the questions first (studyfeed today or studyfeed feed), then ask for an answer by its number.";
                var j = JsonDocument.Parse(await Net.Text(u + "/api/posts/" + lastFeed[n - 1].id + "/answer")).RootElement; var ans = Str(j, "answer");
                return ans == "" ? "That question has no saved answer." : "Answer to " + n + ": " + ans;
            }
            if (cmd == "explain")
            {
                if (arg == "") return "studyfeed explain needs the text to explain.";
                using var resp = await Net.Http.PostAsync(u + "/api/explain", new StringContent(JsonSerializer.Serialize(new { text = arg }), Encoding.UTF8, "application/json"));
                var j = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
                return resp.IsSuccessStatusCode ? Str(j, "text") : "StudyFeed couldn't explain that: " + Str(j, "error");
            }
            return "Unknown StudyFeed command. Use open, today, feed [subject], answer N or explain text.";
        }
        catch (Exception ex) { return "StudyFeed failed: " + ex.Message + " (the free site can take about a minute to wake up; try again)."; }
    }

    // A friendly reminder a few seconds after start when homework is due today, tomorrow or overdue.
    public static async Task Remind(DesktopForm sh)
    {
        await Task.Delay(8000);
        try
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd"); var tom = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
            var due = LoadHw().Where(h => !h.Done && h.Due != "" && string.CompareOrdinal(h.Due, tom) <= 0).ToList(); if (due.Count == 0) return;
            int late = due.Count(h => string.CompareOrdinal(h.Due, today) < 0), t0 = due.Count(h => h.Due == today), t1 = due.Count(h => h.Due == tom);
            sh.Toast("Homework: " + string.Join(", ", new[] { late > 0 ? late + " overdue" : "", t0 > 0 ? t0 + " due today" : "", t1 > 0 ? t1 + " due tomorrow" : "" }.Where(x => x != "")));
        }
        catch { }
    }
}
