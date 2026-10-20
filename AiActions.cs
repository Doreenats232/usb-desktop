using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
namespace UsbDesktop;

// Everything the assistant is allowed to do to files, the desktop and the PC. All file work stays inside the USB (never data/ or vm/),
// deletes go to the Recycle Bin, and every change is logged so it can be undone.
static class AiActions
{
    public class Op { public string Kind { get; set; } = ""; public string A { get; set; } = ""; public string B { get; set; } = ""; }
    public class Batch { public string Time { get; set; } = ""; public List<Op> Ops { get; set; } = new(); }
    static string LogFile => Path.Combine(Paths.Data, "ai", "undo.json");
    static readonly char Sep = Path.DirectorySeparatorChar;

    public static readonly HashSet<string> Kinds = new() { "read", "run", "list", "search", "sysinfo", "desktop", "stickyread", "sticky", "clipread", "clipwrite", "mkdir", "move", "copy", "rename", "delete", "arrange", "eject", "lock", "sleep", "undo", "schedule", "tasks", "unschedule", "scroll", "nav", "goto", "play", "media", "click", "type", "key", "page" };

    static List<Batch> LoadLog() { try { if (File.Exists(LogFile)) return JsonSerializer.Deserialize<List<Batch>>(File.ReadAllText(LogFile)) ?? new(); } catch { } return new(); }
    static void SaveLog(List<Batch> l) { try { Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!); if (l.Count > 60) l.RemoveRange(0, l.Count - 60); File.WriteAllText(LogFile, JsonSerializer.Serialize(l)); } catch { } }
    static string Rel(string p) => Path.GetRelativePath(Paths.Root, p);

    public static string? Resolve(string a)
    {
        a = a.Trim().Trim('"'); if (a == "") return null;
        try
        {
            var full = Path.GetFullPath(Path.IsPathRooted(a) ? a : Path.Combine(Paths.Root, a.Replace('/', Sep))).TrimEnd(Sep);
            var rel = Path.GetRelativePath(Paths.Root, full); if (rel.StartsWith("..") || Path.IsPathRooted(rel)) return null;
            if (rel != "." && rel.Split(Sep)[0].ToLowerInvariant() is "data" or "vm") return null;
            if (string.Equals(full, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return null;
            return full;
        }
        catch { return null; }
    }
    static string Need(string a) { var p = Resolve(a); if (p == null || string.Equals(p, Paths.Root.TrimEnd(Sep), StringComparison.OrdinalIgnoreCase)) throw new Exception("not allowed: " + a); return p; }

    public static string Describe(string k, string a) => k switch
    {
        "move" => "Move " + a.Replace("|", " to "),
        "copy" => "Copy " + a.Replace("|", " to "),
        "rename" => "Rename " + a.Replace("|", " to "),
        "delete" => "Delete (to Recycle Bin) " + a,
        "mkdir" => "Create folder " + a,
        "sticky" => "Change the sticky note",
        "arrange" => "Tidy the desktop icons",
        "eject" => "Safely eject this USB and close USB Desktop",
        "lock" => "Lock this PC",
        "sleep" => "Put this PC to sleep",
        "undo" => "Undo the last change",
        "schedule" => "Schedule a task: " + Sched.Describe(a),
        _ => k + " " + a
    };

    // Deleting asks only for sensitive things: non-empty folders, anything outside the Desktop, documents/archives/keys/databases, or names that sound private.
    public static bool SensitiveDelete(string a)
    {
        var p = Resolve(a); if (p == null) return true;
        if (Directory.Exists(p)) { try { return Directory.EnumerateFileSystemEntries(p).Any(); } catch { return true; } }
        if (!p.StartsWith(Paths.Desktop.TrimEnd(Sep) + Sep, StringComparison.OrdinalIgnoreCase)) return true;
        var n = Path.GetFileName(p).ToLowerInvariant(); var ext = Path.GetExtension(n);
        if (new[] { ".docx", ".doc", ".pdf", ".xlsx", ".xls", ".pptx", ".zip", ".7z", ".rar", ".kdbx", ".key", ".pem", ".pfx", ".db", ".sqlite", ".bak", ".iso", ".qcow2", ".exe" }.Contains(ext)) return true;
        return Regex.IsMatch(n, "password|passwd|secret|private|vault|backup|bank|tax|certificate|credential|token|wallet|recovery");
    }

    // Harmless read-only commands the assistant may run without asking (no chaining or redirects).
    public static bool SafeCmd(string c)
    {
        c = c.Trim(); if (Regex.IsMatch(c, @"[&|<>^%]")) return false;
        return Regex.IsMatch(c, @"^(date /t|time /t|ver|hostname|whoami|ipconfig( /all)?|dir( .*)?|echo .*|tasklist|netsh wlan show \w+|systeminfo|vol)$", RegexOptions.IgnoreCase);
    }

    public static string List(string a)
    {
        var p = a.Trim() == "" || a.Trim() == "." ? Paths.Root : Resolve(a); if (p == null || !Directory.Exists(p)) return "Can't list that folder: " + a;
        var sb = new StringBuilder("Contents of " + (Rel(p) == "." ? "USB root" : Rel(p)) + ":\n"); int n = 0;
        foreach (var e in Directory.GetFileSystemEntries(p).OrderByDescending(Directory.Exists).ThenBy(x => x))
        {
            var name = Path.GetFileName(e); if (Path.GetRelativePath(Paths.Root, e).ToLowerInvariant() is "data" or "vm") continue;
            sb.AppendLine(Directory.Exists(e) ? "[folder] " + name : name + "  (" + Ui.Bytes(new FileInfo(e).Length) + ")"); if (++n >= 80) { sb.AppendLine("…more not shown"); break; }
        }
        return n == 0 ? "That folder is empty." : sb.ToString();
    }

    public static string SysInfo()
    {
        var sb = new StringBuilder(); sb.AppendLine("OS: " + RuntimeInformation.OSDescription); sb.AppendLine("CPU cores: " + Environment.ProcessorCount);
        try { sb.AppendLine("RAM: " + Ui.Bytes(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)); } catch { }
        try { var di = new DriveInfo(Path.GetPathRoot(Paths.Root)!); sb.AppendLine("USB " + di.Name + ": " + Ui.Bytes(di.AvailableFreeSpace) + " free of " + Ui.Bytes(di.TotalSize)); } catch { }
        try { var ps = SystemInformation.PowerStatus; sb.AppendLine((ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0 ? "Battery: none (desktop PC)" : "Battery: " + (int)(ps.BatteryLifePercent * 100) + "%" + (ps.PowerLineStatus == PowerLineStatus.Online ? ", plugged in" : ", on battery")); } catch { }
        try
        {
            using var pr = Process.Start(new ProcessStartInfo("netsh", "wlan show interfaces") { RedirectStandardOutput = true, CreateNoWindow = true, UseShellExecute = false })!; var o = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(4000);
            string? ssid = null, sig = null; foreach (var l in o.Split('\n')) { var t = l.Trim(); if (t.StartsWith("SSID") && !t.StartsWith("SSID name") && t.Contains(':')) ssid ??= t[(t.IndexOf(':') + 1)..].Trim(); else if (t.StartsWith("Signal")) sig ??= t[(t.IndexOf(':') + 1)..].Trim(); }
            sb.AppendLine(ssid != null ? "Wi-Fi: " + ssid + (sig != null ? " (signal " + sig + ")" : "") : "Wi-Fi: not connected or not available");
        }
        catch { sb.AppendLine("Wi-Fi: unknown"); }
        return sb.ToString();
    }

    public static string Search(string q)
    {
        q = q.Trim(); if (q == "") return "Nothing to search for."; var hits = new List<string>(); int seen = 0;
        var textExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".html", ".css", ".js", ".json", ".cs", ".py", ".ini", ".csv", ".log", ".xml", ".bat", ".webapp" };
        void Walk(string d)
        {
            if (hits.Count >= 25 || seen > 4000) return;
            IEnumerable<string> es; try { es = Directory.GetFileSystemEntries(d); } catch { return; }
            foreach (var e in es)
            {
                var rel = Path.GetRelativePath(Paths.Root, e); if (rel.ToLowerInvariant() is "data" or "vm") continue; seen++;
                if (Directory.Exists(e)) { if (Path.GetFileName(e).Contains(q, StringComparison.OrdinalIgnoreCase)) hits.Add("[folder] " + rel); Walk(e); }
                else
                {
                    if (Path.GetFileName(e).Contains(q, StringComparison.OrdinalIgnoreCase)) hits.Add(rel);
                    else if (textExt.Contains(Path.GetExtension(e)))
                        try { var fi = new FileInfo(e); if (fi.Length < 300_000) foreach (var ln in File.ReadLines(e)) if (ln.Contains(q, StringComparison.OrdinalIgnoreCase)) { var s = ln.Trim(); hits.Add(rel + ": " + (s.Length > 100 ? s[..100] : s)); break; } } catch { }
                }
                if (hits.Count >= 25) return;
            }
        }
        Walk(Paths.Root); return hits.Count == 0 ? "No matches for \"" + q + "\"." : "Matches:\n" + string.Join("\n", hits);
    }

    static void CopyDir(string s, string d) { Directory.CreateDirectory(d); foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f))); foreach (var x in Directory.GetDirectories(s)) CopyDir(x, Path.Combine(d, Path.GetFileName(x))); }

    public static List<string> RunAll(DesktopForm sh, List<(string k, string a)> acts)
    {
        var res = new List<string>(); var log = new List<Op>();
        foreach (var (k, a) in acts) res.Add(One(sh, k, a, log));
        if (log.Count > 0) { var l = LoadLog(); l.Add(new Batch { Time = DateTime.Now.ToString("s"), Ops = log }); SaveLog(l); }
        return res;
    }

    static string One(DesktopForm sh, string k, string a, List<Op> log)
    {
        try
        {
            switch (k)
            {
                case "undo": return Undo(sh);
                case "schedule": return Sched.Add(a);
                case "mkdir": { var p = Need(a); if (Directory.Exists(p)) return "Folder already exists: " + Rel(p); Directory.CreateDirectory(p); log.Add(new Op { Kind = "mkdir", A = p }); sh.Reload(); return "Created folder " + Rel(p); }
                case "move": case "copy": case "rename":
                    {
                        var parts = a.Split('|'); if (parts.Length != 2) return k + " needs two parts: source|destination";
                        var src = Need(parts[0]); if (!File.Exists(src) && !Directory.Exists(src)) return "Not found: " + parts[0].Trim();
                        string dst;
                        if (k == "rename") { var nn = parts[1].Trim(); if (nn == "" || nn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "That name isn't allowed."; dst = Path.Combine(Path.GetDirectoryName(src)!, nn); }
                        else { var d = Need(parts[1]); dst = Directory.Exists(d) ? Path.Combine(d, Path.GetFileName(src)) : d; }
                        Need(dst); if (File.Exists(dst) || Directory.Exists(dst)) dst = U.Unique(Path.GetDirectoryName(dst)!, Path.GetFileName(dst));
                        if (Directory.Exists(src) && (dst + Sep).StartsWith(src + Sep, StringComparison.OrdinalIgnoreCase)) return "Can't put a folder inside itself.";
                        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                        if (k == "copy") { if (Directory.Exists(src)) CopyDir(src, dst); else File.Copy(src, dst); log.Add(new Op { Kind = "copy", A = src, B = dst }); }
                        else { if (Directory.Exists(src)) Directory.Move(src, dst); else File.Move(src, dst); log.Add(new Op { Kind = "move", A = src, B = dst }); }
                        sh.Reload(); return $"{k}: {Rel(src)} -> {Rel(dst)}";
                    }
                case "delete":
                    {
                        var p = Need(a); if (!File.Exists(p) && !Directory.Exists(p)) return "Not found: " + a.Trim(); Bin.Trash(p);
                        var entry = Bin.Items().Where(x => { try { return File.ReadAllText(x + ".orig") == p; } catch { return false; } }).OrderBy(Path.GetFileName).LastOrDefault();
                        log.Add(new Op { Kind = "delete", A = p, B = entry ?? "" }); sh.Reload(); return "Moved to Recycle Bin: " + Rel(p);
                    }
                case "sticky": { var old = Cfg.Get("sticky"); Cfg.Set("sticky", a.Replace("\\n", "¶").Replace("\r", "").Replace("\n", "¶")); sh.ApplyWidgets(); log.Add(new Op { Kind = "sticky", A = old }); return "Sticky note updated."; }
                case "arrange": { var old = sh.ArrangeIcons(); log.Add(new Op { Kind = "arrange", A = old }); return "Desktop icons tidied."; }
                case "eject":
                    {
                        var drive = Path.GetPathRoot(Paths.Root)!.TrimEnd(Sep); if (drive.Length != 2 || drive[1] != ':') return "This isn't on a drive letter, so I can't eject it.";
                        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"Start-Sleep 3; (New-Object -comObject Shell.Application).Namespace(17).ParseName('{drive}').InvokeVerb('Eject')\"") { CreateNoWindow = true, UseShellExecute = false });
                        sh.Toast("Ejecting the USB… closing"); Task.Delay(900).ContinueWith(_ => { try { sh.BeginInvoke(new Action(Application.Exit)); } catch { } }); return "Ejecting the USB now.";
                    }
                case "lock": Process.Start(new ProcessStartInfo("rundll32.exe", "user32.dll,LockWorkStation") { UseShellExecute = false }); return "Locking the PC.";
                case "sleep": Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { UseShellExecute = false }); return "Putting the PC to sleep.";
            }
        }
        catch (Exception ex) { return k + " failed: " + ex.Message; }
        return "";
    }

    public static string Undo(DesktopForm sh)
    {
        var l = LoadLog(); if (l.Count == 0) return "Nothing to undo."; var b = l[^1]; l.RemoveAt(l.Count - 1); SaveLog(l); var res = new List<string>();
        foreach (var o in Enumerable.Reverse(b.Ops))
        {
            try
            {
                switch (o.Kind)
                {
                    case "move": if ((File.Exists(o.B) || Directory.Exists(o.B)) && !File.Exists(o.A) && !Directory.Exists(o.A)) { Directory.CreateDirectory(Path.GetDirectoryName(o.A)!); if (Directory.Exists(o.B)) Directory.Move(o.B, o.A); else File.Move(o.B, o.A); res.Add("Moved back " + Path.GetFileName(o.A)); } else res.Add("Couldn't move back " + Path.GetFileName(o.A)); break;
                    case "copy": if (File.Exists(o.B) || Directory.Exists(o.B)) { Bin.Trash(o.B); res.Add("Removed the copy " + Path.GetFileName(o.B)); } break;
                    case "delete": if (o.B != "" && (File.Exists(o.B) || Directory.Exists(o.B))) { Bin.Restore(o.B); res.Add("Restored " + Path.GetFileName(o.A)); } else res.Add("Couldn't restore " + Path.GetFileName(o.A) + " (check the Recycle Bin)"); break;
                    case "mkdir": if (Directory.Exists(o.A) && !Directory.EnumerateFileSystemEntries(o.A).Any()) { Directory.Delete(o.A); res.Add("Removed folder " + Path.GetFileName(o.A)); } break;
                    case "sticky": Cfg.Set("sticky", o.A); sh.ApplyWidgets(); res.Add("Sticky note restored"); break;
                    case "arrange": sh.RestoreIcons(o.A); res.Add("Icons put back"); break;
                }
            }
            catch (Exception ex) { res.Add("Undo problem: " + ex.Message); }
        }
        sh.Reload(); return "Undone: " + string.Join("; ", res);
    }
}
