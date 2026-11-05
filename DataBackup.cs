using System.IO.Compression;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Backs up your settings and AI history (the "data" folder, minus anything secret) to your TeraBox vault as one small zip.
// It runs by itself a few minutes after something changes, checks that the file really arrived, and keeps the newest two copies.
static class DataBackup
{
    const string Pre = "usbdesktop-data-";
    static bool running;
    // never saved to the cloud (and never put back on restore): secrets, and switches that must be turned on by hand on each PC
    static readonly HashSet<string> SkipKeys = new(StringComparer.OrdinalIgnoreCase) { "phone_code", "ai_full", "ai_full_until", "remote_input", "vault_auto_seen", "data_auto_fp", "data_auto_last", "seen_ver", "skip_ver" };
    public static bool Skip(string key) => key.EndsWith("_key", StringComparison.OrdinalIgnoreCase) || SkipKeys.Contains(key);
    static readonly string[] Plain = { "todo.json", "study.txt", "bookmarks.txt", "pcs.txt", "avatar.png", "gavatar.png" };

    static IEnumerable<(string full, string entry)> Files()
    {
        foreach (var n in Plain) { var p = Path.Combine(Paths.Data, n); if (File.Exists(p)) yield return (p, n); }
        var ai = Path.Combine(Paths.Data, "ai");
        if (Directory.Exists(ai))
            foreach (var p in Directory.GetFiles(ai)) { var n = Path.GetFileName(p); if (!n.Equals("undo.json", StringComparison.OrdinalIgnoreCase)) yield return (p, "ai/" + n); }
    }
    static string SettingsFile => Path.Combine(Paths.Data, "settings.ini");

    // changes when something worth saving changed (our own bookkeeping keys are ignored, so a backup never triggers another one)
    static string Fingerprint()
    {
        var sb = new StringBuilder();
        try
        {
            if (File.Exists(SettingsFile)) foreach (var l in File.ReadAllLines(SettingsFile)) { var i = l.IndexOf('='); if (i > 0 && !Skip(l[..i])) sb.AppendLine(l); }
            foreach (var f in Files().OrderBy(x => x.entry, StringComparer.Ordinal)) { var fi = new FileInfo(f.full); sb.Append(f.entry).Append('|').Append(fi.Length).Append('|').Append(fi.LastWriteTimeUtc.Ticks).AppendLine(); }
        }
        catch { }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }
    static long NewestTicks()
    {
        long t = 0;
        try { if (File.Exists(SettingsFile)) t = File.GetLastWriteTimeUtc(SettingsFile).Ticks; foreach (var f in Files()) t = Math.Max(t, File.GetLastWriteTimeUtc(f.full).Ticks); } catch { }
        return t;
    }

    static string Build()
    {
        var zip = Path.Combine(Path.GetTempPath(), "UsbDesktop-data.zip"); try { File.Delete(zip); } catch { }
        int count = 0;
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            if (File.Exists(SettingsFile))
            {
                var keep = File.ReadAllLines(SettingsFile).Where(l => { var i = l.IndexOf('='); return i > 0 && !Skip(l[..i]); }).ToList();
                var se = z.CreateEntry("settings.ini"); using (var w = new StreamWriter(se.Open(), new UTF8Encoding(false))) foreach (var l in keep) w.WriteLine(l);
                count++;
            }
            foreach (var (full, entry) in Files())
            {
                try
                {
                    byte[] data; using (var src = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var ms = new MemoryStream()) { src.CopyTo(ms); data = ms.ToArray(); }
                    var e = z.CreateEntry(entry, CompressionLevel.Optimal); using var dst = e.Open(); dst.Write(data, 0, data.Length); count++;
                }
                catch { }
            }
        }
        if (count == 0) { try { File.Delete(zip); } catch { } throw new Exception("Nothing to back up yet."); }
        return zip;
    }

    public static async Task<string> Backup(Action<string> say, CancellationToken ct)
    {
        if (!VaultBackup.Ready) throw new Exception("Add your vault address and API key first (Vault → Connection).");
        if (running) throw new Exception("A data backup is already running.");
        running = true; string? zip = null;
        try
        {
            var fp = Fingerprint();
            say("Packing your settings and AI history…"); zip = Build(); long len = new FileInfo(zip).Length;
            var rn = Pre + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip";
            say("Uploading " + Math.Max(1, len / 1024) + " KB…"); await VaultBackup.Upload(zip, rn, ct);
            say("Checking that it arrived…"); var after = await VaultBackup.Listing(ct);
            if (!after.TryGetValue(rn, out var sz)) throw new Exception("NOT backed up: the file is not in your vault's list. Your USB files are untouched. Try Vault → Test backup.");
            if (sz >= 0 && sz != len) throw new Exception($"NOT backed up: the vault has the wrong size ({sz} of {len} bytes). Your USB files are untouched.");
            foreach (var old in after.Keys.Where(n => n.StartsWith(Pre, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !n.Equals(rn, StringComparison.OrdinalIgnoreCase)).OrderByDescending(n => n, StringComparer.Ordinal).Skip(1).ToList())
                await VaultBackup.TryDelete(old, ct);   // keep this one and the one before it
            Cfg.Set("data_auto_fp", fp); Cfg.Set("data_auto_last", DateTime.UtcNow.Ticks.ToString());
            return "Data backup verified in your TeraBox vault (" + Math.Max(1, len / 1024) + " KB) ✅";
        }
        finally { running = false; try { if (zip != null) File.Delete(zip); } catch { } }
    }

    public static async Task<string> Restore(Action<string> say, CancellationToken ct)
    {
        if (!VaultBackup.Ready) throw new Exception("Add your vault address and API key first (Vault → Connection).");
        say("Looking for your data backup…");
        var names = (await VaultBackup.Names(ct)).Where(n => n.StartsWith(Pre, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).OrderByDescending(n => n, StringComparer.Ordinal).ToList();
        if (names.Count == 0) throw new Exception("No data backup found in your TeraBox project.");
        say("Downloading the newest backup…");
        var s = await VaultBackup.Json(VaultBackup.Rq(HttpMethod.Post, "/v1/fs/sign", JsonSerializer.Serialize(new { path = names[0], ttl = 900 })), ct);
        var tmp = Path.Combine(Path.GetTempPath(), "UsbDesktop-data-in.zip");
        try
        {
            await Net.Download(s.GetProperty("url").GetString()!, tmp, (_, _, _) => { }, ct);
            int n = 0;
            using (var z = ZipFile.OpenRead(tmp))
                foreach (var e in z.Entries)
                {
                    var name = e.FullName.Replace('\\', '/');
                    try
                    {
                        if (name == "settings.ini")
                        {
                            using var r = new StreamReader(e.Open()); string? l;
                            while ((l = r.ReadLine()) != null) { var i = l.IndexOf('='); if (i > 0 && !Skip(l[..i])) Cfg.Set(l[..i], l[(i + 1)..]); }
                            n++; continue;
                        }
                        bool ok = Array.IndexOf(Plain, name) >= 0;
                        if (!ok && name.StartsWith("ai/")) { var fn = name[3..]; ok = fn != "" && fn.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !fn.Equals("undo.json", StringComparison.OrdinalIgnoreCase); }
                        if (!ok) continue;
                        var dest = Path.Combine(Paths.Data, name.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        e.ExtractToFile(dest, true); n++;
                    }
                    catch { }
                }
            if (n == 0) throw new Exception("The backup had nothing I could restore.");
            return "Restored " + n + " item(s). Close and reopen USB Desktop to apply them.";
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    static DateTime nextTry = DateTime.MinValue;
    public static void AutoStart(DesktopForm sh)
    {
        var t = new System.Windows.Forms.Timer { Interval = 5 * 60000 };
        t.Tick += async (_, _) =>
        {
            if (running || DateTime.Now < nextTry || Cfg.Get("data_auto", "1") != "1" || !VaultBackup.Ready) return;
            try
            {
                if (long.TryParse(Cfg.Get("data_auto_last"), out var last) && last > 0 && (DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)).TotalMinutes < 30) return;   // at most every 30 minutes
                if (Fingerprint() == Cfg.Get("data_auto_fp")) return;                                                                                                         // nothing new
                if ((DateTime.UtcNow - new DateTime(NewestTicks(), DateTimeKind.Utc)).TotalSeconds < 120) return;                                                               // wait until changes settle
                await Backup(_ => { }, CancellationToken.None);
                sh.Toast("💾 Data backed up automatically ✅");
            }
            catch (Exception ex)
            {
                nextTry = DateTime.Now.AddMinutes(60);
                var m = ex.Message.Length > 110 ? ex.Message[..110] + "…" : ex.Message; sh.Toast("💾 Data auto-backup failed ❌ " + m); PhoneLink.Alert("💾 Data backup failed", m);
            }
        };
        t.Start();
    }
}
