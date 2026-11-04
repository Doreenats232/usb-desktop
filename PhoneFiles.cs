using System.Text.Json;
namespace UsbDesktop;

// File browser for the phone app: list, download, upload, new folder, delete (to the Recycle Bin) and open on the PC.
// Only paths inside the USB are allowed. The private "data" folders are hidden, and programs are never launched from here.
static class PhoneFiles
{
    static readonly char Sep = Path.DirectorySeparatorChar;
    static readonly HashSet<string> Programs = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".lnk", ".reg", ".jar", ".dll", ".cpl" };
    static readonly HashSet<string> Keep = new(StringComparer.OrdinalIgnoreCase) { "launcher.exe", "launcher-x86.exe", "launcher-arm64.exe", "profiles", "apps", "vm", "data", "desktop" };

    static string RootDir { get { var r = Paths.Root; return r.Length > 3 ? r.TrimEnd(Sep) : r; } }
    static string Norm(string p) => p.Length > 3 ? p.TrimEnd(Sep) : p;

    static bool IsPrivate(string full)
    {
        var rel = Path.GetRelativePath(RootDir, full);
        if (rel == ".") return false;
        var s = rel.Split(Sep, StringSplitOptions.RemoveEmptyEntries); if (s.Length == 0) return false;
        if (s[0].Equals("data", StringComparison.OrdinalIgnoreCase)) return true;
        return s[0].Equals("profiles", StringComparison.OrdinalIgnoreCase) && s.Length >= 3 && s[2].Equals("data", StringComparison.OrdinalIgnoreCase);
    }
    // phone path ("Desktop/Docs") -> full path, or null when it is outside the USB or private
    static string? Res(string rel)
    {
        rel = (rel ?? "").Trim().Replace('/', Sep).Trim(Sep);
        try
        {
            var root = RootDir; var pre = root.EndsWith(Sep) ? root : root + Sep;
            var full = Norm(Path.GetFullPath(rel == "" ? root : Path.Combine(root, rel)));
            if (!full.Equals(root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return null;
            return IsPrivate(full) ? null : full;
        }
        catch { return null; }
    }
    static string RelOf(string full) { var r = Path.GetRelativePath(RootDir, full); return r == "." ? "" : r.Replace(Sep, '/'); }

    // Runs on the UI thread (called from Quick).
    public static string Run(DesktopForm sh, string a, PhoneLink.Ctx ctx)
    {
        var sp = a.Split(' ', 2); var k = sp[0].ToLowerInvariant(); var arg = sp.Length > 1 ? sp[1] : "";
        switch (k)
        {
            case "ls": return Listing(arg);
            case "get": return Get(arg, ctx);
            case "put": return Put(arg, ctx);
            case "mkdir": return MakeDir(arg);
            case "rm": return Remove(sh, arg);
            case "open": return OpenOnPc(sh, arg);
            default: return "ERR: unknown file command";
        }
    }

    // "FS1" + one line of JSON. The relay cuts replies at 6000 characters, so the list is paged (arg = "offset|folder").
    static string Listing(string arg)
    {
        int bar = arg.IndexOf('|'), off = 0; string rel = arg;
        if (bar >= 0) { int.TryParse(arg[..bar], out off); rel = arg[(bar + 1)..]; }
        if (off < 0) off = 0;
        var dir = Res(rel); if (dir == null) return "ERR: that folder isn't available";
        if (!Directory.Exists(dir)) return "ERR: folder not found";
        var all = new List<(string n, bool d, long s, long m)>();
        try
        {
            foreach (var e in new DirectoryInfo(dir).EnumerateFileSystemInfos())
            {
                try
                {
                    if ((e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    if (IsPrivate(e.FullName)) continue;
                    bool d = (e.Attributes & FileAttributes.Directory) != 0;
                    all.Add((e.Name, d, d ? 0L : ((FileInfo)e).Length, new DateTimeOffset(e.LastWriteTimeUtc).ToUnixTimeSeconds()));
                }
                catch { }
            }
        }
        catch (Exception ex) { return "ERR: " + ex.Message; }
        all = all.OrderBy(x => x.d ? 0 : 1).ThenBy(x => x.n, StringComparer.OrdinalIgnoreCase).ToList();
        var items = new List<object>(); int used = 0, i = off;
        for (; i < all.Count && items.Count < 120; i++)
        {
            var x = all[i]; var o = new { n = x.n, d = x.d ? 1 : 0, s = x.s, m = x.m };
            int len = JsonSerializer.Serialize(o).Length + 1; if (used + len > 4800) break;
            used += len; items.Add(o);
        }
        int next = i < all.Count ? i : 0;
        return "FS1\n" + JsonSerializer.Serialize(new { p = RelOf(dir), total = all.Count, off, next, items });
    }

    static string Get(string rel, PhoneLink.Ctx ctx)
    {
        var f = Res(rel); if (f == null) return "ERR: that file isn't available";
        if (!File.Exists(f)) return "ERR: file not found";
        if (new FileInfo(f).Length > 8 * 1024 * 1024) return "ERR: that file is over 8 MB, too big to send to the phone.";
        ctx.AttachPath = f; return "📄 " + Path.GetFileName(f);
    }

    // The relay handed us the uploaded file already saved in Downloads (ctx.SavedPath); move it to the folder the phone picked.
    static string Put(string rel, PhoneLink.Ctx ctx)
    {
        if (ctx.SavedPath == null || !File.Exists(ctx.SavedPath)) return "ERR: no file came with that.";
        var dir = Res(rel); if (dir == null || !Directory.Exists(dir)) return "ERR: that folder isn't available";
        var name = Path.GetFileName(ctx.SavedPath);
        if (string.Equals(Norm(Path.GetFullPath(Path.GetDirectoryName(ctx.SavedPath)!)), dir, StringComparison.OrdinalIgnoreCase)) return "Saved " + name + " here.";
        var dst = U.Unique(dir, name); File.Move(ctx.SavedPath, dst);
        return "Saved " + Path.GetFileName(dst) + " here.";
    }

    static string MakeDir(string arg)
    {
        int bar = arg.IndexOf('|'); if (bar < 0) return "ERR: need a folder name";
        var dir = Res(arg[..bar]); var name = arg[(bar + 1)..].Trim();
        if (dir == null || !Directory.Exists(dir)) return "ERR: that folder isn't available";
        if (name == "" || name.Length > 80 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..") return "ERR: that name isn't allowed.";
        var p = Path.Combine(dir, name); if (Directory.Exists(p) || File.Exists(p)) return "ERR: that name already exists.";
        Directory.CreateDirectory(p); return "Created folder " + name;
    }

    // Always the Recycle Bin (never permanent), so it can be restored from the PC.
    static string Remove(DesktopForm sh, string rel)
    {
        var f = Res(rel); if (f == null || string.Equals(f, RootDir, StringComparison.OrdinalIgnoreCase)) return "ERR: that can't be deleted";
        var r = RelOf(f); if (!r.Contains('/') && Keep.Contains(r)) return "ERR: that's part of USB Desktop itself, so I won't delete it.";
        if (!File.Exists(f) && !Directory.Exists(f)) return "ERR: not found";
        Directory.CreateDirectory(Paths.Bin); Bin.Trash(f); sh.Reload();
        return "🗑 Moved to the Recycle Bin: " + Path.GetFileName(f);
    }

    static string OpenOnPc(DesktopForm sh, string rel)
    {
        var f = Res(rel); if (f == null) return "ERR: that isn't available";
        if (!File.Exists(f) && !Directory.Exists(f)) return "ERR: not found";
        if (File.Exists(f) && Programs.Contains(Path.GetExtension(f))) return "ERR: I won't start programs from the phone. You can download it instead.";
        sh.OpenFile(f); return "Opened " + Path.GetFileName(f) + " on the PC.";
    }
}
