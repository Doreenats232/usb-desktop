using System.Media;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace UsbDesktop;

// Scheduled and startup tasks. Safe by design: you approve each task once when it is created, tasks can only do harmless things
// (open an app, open a page, save a note, change the sticky note, show a reminder, tidy icons), never delete/move/run commands,
// they only run while USB Desktop is open, and you can pause or remove them any time (assistant header: Tasks).
static class Sched
{
    public class TaskItem { public string Id { get; set; } = ""; public string When { get; set; } = ""; public string Action { get; set; } = ""; public bool On { get; set; } = true; public DateTime Last { get; set; } }
    static string File_ => Path.Combine(Paths.Data, "ai", "tasks.json");
    static readonly string[] Apps = { "files", "code", "notepad", "calc", "paint", "term", "web", "store", "tm", "games", "getapps", "ai" };
    static readonly string[] Kinds = { "open", "web", "note", "sticky", "remind", "arrange" };
    static List<TaskItem> tasks = Load();
    static System.Windows.Forms.Timer? timer; static DesktopForm? shell;

    static List<TaskItem> Load() { try { if (File.Exists(File_)) return JsonSerializer.Deserialize<List<TaskItem>>(File.ReadAllText(File_)) ?? new(); } catch { } return new(); }
    static void Save() { try { Directory.CreateDirectory(Path.GetDirectoryName(File_)!); File.WriteAllText(File_, JsonSerializer.Serialize(tasks)); } catch { } }

    // "startup" | "daily HH:mm" | "every N"  ->  normalized text, or null if invalid
    static string? NormWhen(string w)
    {
        w = w.Trim().ToLowerInvariant();
        if (w == "startup" || w == "on startup" || w == "at startup") return "startup";
        var d = Regex.Match(w, @"^daily\s+(\d{1,2}):(\d{2})$"); if (d.Success) { int h = int.Parse(d.Groups[1].Value), m = int.Parse(d.Groups[2].Value); if (h < 24 && m < 60) return $"daily {h:00}:{m:00}"; }
        var e = Regex.Match(w, @"^every\s+(\d{1,4})(\s*min\w*)?$"); if (e.Success) { int n = int.Parse(e.Groups[1].Value); if (n >= 5) return "every " + n; }
        return null;
    }

    static string? CheckAction(string when, string act)
    {
        var i = act.IndexOf(':'); var kind = (i > 0 ? act[..i] : act).Trim().ToLowerInvariant(); var arg = i > 0 ? act[(i + 1)..].Trim() : "";
        if (!Kinds.Contains(kind)) return "Tasks can only: open an app, open a web page, save a note, change the sticky note, show a reminder, or tidy icons.";
        if (kind == "open" && !Apps.Contains(arg.ToLowerInvariant())) return "That app can't be opened by a task.";
        if (kind == "web" && !arg.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "A web task needs a full address starting with http.";
        if (when.StartsWith("every") && kind is "open" or "web" or "arrange") return "Repeating tasks can only show reminders, save notes or change the sticky note (so they can't spam windows).";
        if ((kind is "remind" or "note" or "sticky") && arg == "") return "That task has no text.";
        return null;
    }

    public static string Describe(string a)
    {
        var i = a.IndexOf('|'); return i > 0 ? $"{a[..i].Trim()}  →  {a[(i + 1)..].Trim()}" : a;
    }

    public static string Add(string a)
    {
        var i = a.IndexOf('|'); if (i <= 0) return "A task needs a time and an action, like: daily 07:30|remind:Wake up";
        var when = NormWhen(a[..i]); if (when == null) return "I couldn't understand that time. Use: startup, daily HH:mm, or every N (minutes, 5 or more).";
        var act = a[(i + 1)..].Trim(); var err = CheckAction(when, act); if (err != null) return err;
        if (tasks.Count >= 20) return "You already have 20 tasks. Remove one first.";
        if (tasks.Any(t => t.When == when && t.Action == act)) return "That task already exists.";
        tasks.Add(new TaskItem { Id = Guid.NewGuid().ToString("N")[..6], When = when, Action = act, Last = DateTime.Now }); Save();
        return "Task saved: " + when + " → " + act + ". It runs while USB Desktop is open.";
    }

    public static string Remove(string key)
    {
        key = key.Trim(); var t = tasks.FirstOrDefault(x => x.Id == key) ?? tasks.FirstOrDefault(x => key != "" && (x.Action + " " + x.When).Contains(key, StringComparison.OrdinalIgnoreCase));
        if (t == null) return "I couldn't find that task."; tasks.Remove(t); Save(); return "Removed the task: " + t.When + " → " + t.Action;
    }

    public static string ListText() => tasks.Count == 0 ? "No tasks scheduled." : "Tasks:\n" + string.Join("\n", tasks.Select(t => $"[{t.Id}] {(t.On ? "on" : "paused")}  {t.When}  →  {t.Action}"));

    public static void Start(DesktopForm sh)
    {
        shell = sh; if (timer != null) return;
        foreach (var t in tasks) if (t.When.StartsWith("every") && t.Last == default) t.Last = DateTime.Now;
        var first = new System.Windows.Forms.Timer { Interval = 5000 }; first.Tick += (_, _) => { first.Stop(); first.Dispose(); foreach (var t in tasks.Where(x => x.On && x.When == "startup").ToList()) Run(t); };
        first.Start();
        timer = new System.Windows.Forms.Timer { Interval = 30000 }; timer.Tick += (_, _) => Tick(); timer.Start();
    }

    static void Tick()
    {
        var now = DateTime.Now;
        foreach (var t in tasks.Where(x => x.On).ToList())
        {
            if (t.When.StartsWith("daily"))
            {
                var p = t.When[6..].Split(':'); var due = now.Date.AddHours(int.Parse(p[0])).AddMinutes(int.Parse(p[1]));
                if (now >= due && now < due.AddMinutes(15) && t.Last < due) Run(t);
            }
            else if (t.When.StartsWith("every") && (now - t.Last).TotalMinutes >= int.Parse(t.When[6..])) Run(t);
        }
    }

    static void Run(TaskItem t)
    {
        var sh = shell; if (sh == null) return; t.Last = DateTime.Now; Save();
        try
        {
            var i = t.Action.IndexOf(':'); var kind = (i > 0 ? t.Action[..i] : t.Action).Trim().ToLowerInvariant(); var arg = i > 0 ? t.Action[(i + 1)..].Trim() : "";
            switch (kind)
            {
                case "open": sh.Launch(arg.ToLowerInvariant()); break;
                case "web": sh.Launch("web", arg); break;
                case "remind": SystemSounds.Asterisk.Play(); sh.Toast("⏰ " + arg); break;
                case "sticky": Cfg.Set("sticky", arg.Replace("\\n", "¶")); sh.ApplyWidgets(); break;
                case "arrange": sh.ArrangeIcons(); break;
                case "note":
                    {
                        int j = arg.IndexOf('|'); var name = j > 0 ? arg[..j].Trim() : "Note"; var body = j > 0 ? arg[(j + 1)..] : arg;
                        foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_'); if (name == "") name = "Note";
                        File.WriteAllText(U.Unique(Paths.Desktop, name + ".txt"), body.Replace("\\n", "\n")); sh.Reload(); break;
                    }
            }
        }
        catch { }
    }

    // Simple manager window: pause/resume or remove tasks.
    public static void Manage(DesktopForm sh)
    {
        using var f = new Form { Text = "Scheduled tasks", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(620, 340), MaximizeBox = false, MinimizeBox = false, TopMost = true };
        var lb = new ListBox { Left = 12, Top = 12, Width = 596, Height = 270, Font = new Font("Segoe UI", 10) };
        void Fill() { int s = lb.SelectedIndex; lb.Items.Clear(); foreach (var t in tasks) lb.Items.Add($"{(t.On ? "●" : "○")}  {t.When}   →   {t.Action}"); if (lb.Items.Count > 0) lb.SelectedIndex = Math.Clamp(s, 0, lb.Items.Count - 1); }
        var bp = new Button { Text = "Pause / resume", Left = 12, Top = 292, Width = 140, Height = 32 };
        var br = new Button { Text = "Remove", Left = 162, Top = 292, Width = 110, Height = 32 };
        var bc = new Button { Text = "Close", Left = 498, Top = 292, Width = 110, Height = 32, DialogResult = DialogResult.OK };
        bp.Click += (_, _) => { if (lb.SelectedIndex >= 0) { tasks[lb.SelectedIndex].On = !tasks[lb.SelectedIndex].On; Save(); Fill(); } };
        br.Click += (_, _) => { if (lb.SelectedIndex >= 0) { tasks.RemoveAt(lb.SelectedIndex); Save(); Fill(); } };
        Fill(); f.Controls.AddRange(new Control[] { lb, bp, br, bc }); f.AcceptButton = bc; f.ShowDialog();
    }
}
