using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// Backs up the (already encrypted) local vault files to your TeraBox vault project, and restores them.
static class VaultBackup
{
    const string Pre = "usbdesktop-vault-";
    static string Dir => Path.Combine(Paths.Data, "vault");
    static string Base => Cfg.Get("vault_url", "https://terabox-vault-backend-1.onrender.com").Trim().TrimEnd('/');
    public static bool Ready => Base != "" && Cfg.Get("vault_key").Trim() != "";

    static HttpRequestMessage Rq(HttpMethod m, string path, string? json = null)
    {
        var u = Base; if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase)) u = "https://" + u;
        var r = new HttpRequestMessage(m, u + path); r.Headers.Add("X-Api-Key", Cfg.Get("vault_key").Trim());
        if (json != null) r.Content = new StringContent(json, Encoding.UTF8, "application/json"); return r;
    }
    static async Task<JsonElement> Json(HttpRequestMessage r, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(90));
        using var resp = await Net.Http.SendAsync(r, cts.Token); var b = await resp.Content.ReadAsStringAsync(cts.Token);
        if (!resp.IsSuccessStatusCode) { string m = b.Length > 200 ? b[..200] : b; try { var e = JsonDocument.Parse(b).RootElement; if (e.TryGetProperty("error", out var x)) m = x.ValueKind == JsonValueKind.String ? x.GetString()! : x.ToString(); } catch { } throw new Exception($"Vault said {(int)resp.StatusCode}: {m}"); }
        if (b.Trim() == "") return JsonDocument.Parse("{}").RootElement.Clone();
        return JsonDocument.Parse(b).RootElement.Clone();
    }

    // name -> size in bytes (-1 when the vault does not report sizes)
    static async Task<Dictionary<string, long>> Listing(CancellationToken ct)
    {
        var j = await Json(Rq(HttpMethod.Get, "/v1/fs/list?dir="), ct); var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase); var arr = j;
        if (j.ValueKind == JsonValueKind.Object) foreach (var p in j.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Array) { arr = p.Value; break; }
        if (arr.ValueKind != JsonValueKind.Array) return map;
        foreach (var e in arr.EnumerateArray())
        {
            string? n = null; long size = -1;
            if (e.ValueKind == JsonValueKind.String) n = e.GetString();
            else if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var k in new[] { "name", "filename", "server_filename", "path" }) if (e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String) { n = v.GetString(); break; }
                foreach (var k in new[] { "size", "bytes", "length" }) if (e.TryGetProperty(k, out var v2) && v2.ValueKind == JsonValueKind.Number && v2.TryGetInt64(out var sz)) { size = sz; break; }
            }
            if (!string.IsNullOrEmpty(n)) map[Path.GetFileName(n.TrimEnd('/'))] = size;
        }
        return map;
    }
    static async Task<HashSet<string>> Names(CancellationToken ct) => new HashSet<string>((await Listing(ct)).Keys, StringComparer.OrdinalIgnoreCase);
    static async Task TryDelete(string rn, CancellationToken ct) { try { using var d = await Net.Http.SendAsync(Rq(HttpMethod.Post, "/v1/fs/delete", JsonSerializer.Serialize(new { path = rn })), ct); } catch { } }

    static async Task Upload(string local, string remote, CancellationToken ct)
    {
        var fi = new FileInfo(local);
        var init = await Json(Rq(HttpMethod.Post, "/v1/fs/uploads", JsonSerializer.Serialize(new { filename = remote, size = fi.Length })), ct);
        var id = init.GetProperty("uploadId").GetString()!; int total = init.GetProperty("totalChunks").GetInt32(); int cs = (int)init.GetProperty("chunkSize").GetInt64();
        using (var fs = File.OpenRead(local))
        {
            var buf = new byte[cs];
            for (int i = 0; i < total; i++)
            {
                int got = 0; while (got < cs) { int n = await fs.ReadAsync(buf.AsMemory(got, cs - got), ct); if (n == 0) break; got += n; }
                using var r = Rq(HttpMethod.Put, $"/v1/fs/uploads/{id}/{i}"); r.Content = new ByteArrayContent(buf, 0, got);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromMinutes(5));
                using var resp = await Net.Http.SendAsync(r, cts.Token); resp.EnsureSuccessStatusCode();
            }
        }
        await Json(Rq(HttpMethod.Post, $"/v1/fs/uploads/{id}/complete"), ct);
        for (int t = 0; t < 90; t++)
        {
            await Task.Delay(2000, ct); var s = await Json(Rq(HttpMethod.Get, $"/v1/fs/uploads/{id}"), ct);
            var st = s.TryGetProperty("state", out var sv) ? sv.GetString() : "done";
            if (st == "done") return; if (st != "processing") throw new Exception("Upload of " + Path.GetFileName(local) + " ended as: " + st);
        }
        throw new Exception("Upload took too long: " + Path.GetFileName(local));
    }

    public static async Task Backup(Action<string> say, CancellationToken ct)
    {
        var files = Directory.GetFiles(Dir).Where(f => f.EndsWith(".enc") || Path.GetFileName(f) == "salt.bin").ToList();
        if (files.Count == 0) throw new Exception("Nothing in the vault to back up yet.");
        say("Checking your TeraBox vault…"); var have = await Listing(ct); int done = 0, sent = 0, kept = 0;
        foreach (var f in files.OrderBy(f => Path.GetFileName(f) is "salt.bin" or "index.enc" ? 1 : 0))
        {
            var n = Path.GetFileName(f); var rn = Pre + n; bool small = n is "salt.bin" or "index.enc"; long len = new FileInfo(f).Length; done++;
            say($"Backing up {done}/{files.Count}: {n}…");
            bool there = have.TryGetValue(rn, out var rs);
            if (there && !small && (rs < 0 || rs == len)) { kept++; continue; }   // already there (and the same size)
            if (there) await TryDelete(rn, ct);
            await Upload(f, rn, ct); sent++;
        }
        say("Checking that everything really arrived…");
        var after = await Listing(ct); var bad = new List<string>();
        foreach (var f in files)
        {
            var n = Path.GetFileName(f); long len = new FileInfo(f).Length;
            if (!after.TryGetValue(Pre + n, out var sz)) bad.Add(n + " (missing in the vault)"); else if (sz >= 0 && sz != len) bad.Add(n + $" (wrong size: {sz} of {len} bytes)");
        }
        if (bad.Count > 0) throw new Exception("NOT fully backed up: " + string.Join(", ", bad) + ". Your USB files are untouched. Press Backup again, or press Test backup to find the problem.");
        say($"Backup verified: all {files.Count} file(s) are in your TeraBox project ({sent} uploaded, {kept} already there).");
    }

    // Proves the whole path works: reach the vault, upload a tiny file, see it in the list, download it back and compare.
    public static async Task<string> Test(Action<string> say, CancellationToken ct)
    {
        var sb = new StringBuilder(); const string rn = "usbdesktop-test.txt"; var tmp = Path.Combine(Path.GetTempPath(), "UsbDesktop-test.txt"); var back = Path.Combine(Path.GetTempPath(), "UsbDesktop-test-back.txt");
        void Ok(string t) => sb.Append("✅ ").Append(t).Append('\n'); void No(string t) => sb.Append("❌ ").Append(t).Append('\n');
        try
        {
            say("Test 1/4: reaching your vault…"); var l = await Listing(ct); Ok($"Connected and the key works ({l.Count} file(s) already in the project)");
            var text = "usb desktop test " + DateTime.Now.Ticks; File.WriteAllText(tmp, text);
            say("Test 2/4: uploading a tiny test file…"); if (l.ContainsKey(rn)) await TryDelete(rn, ct); await Upload(tmp, rn, ct); Ok("Test file uploaded");
            say("Test 3/4: checking it is really there…"); var l2 = await Listing(ct);
            if (!l2.ContainsKey(rn)) { No("The upload said done, but the file is not in the project's list. The key may belong to a different project, or the vault is not saving files."); return sb.ToString(); }
            Ok("Test file shows up in your project");
            say("Test 4/4: downloading it back…"); var sg = await Json(Rq(HttpMethod.Post, "/v1/fs/sign", JsonSerializer.Serialize(new { path = rn, ttl = 900 })), ct);
            await Net.Download(sg.GetProperty("url").GetString()!, back, (_, _, _) => { }, ct);
            if (File.ReadAllText(back) == text) Ok("Downloaded it back and it matches. Backups will arrive."); else No("It came back different from what was sent. Do not trust backups yet.");
        }
        catch (Exception ex) { No(ex.Message); }
        finally { await TryDelete(rn, CancellationToken.None); try { File.Delete(tmp); } catch { } try { File.Delete(back); } catch { } }
        return sb.ToString();
    }

    public static async Task Restore(Action<string> say, CancellationToken ct)
    {
        var names = (await Names(ct)).Where(n => n.StartsWith(Pre, StringComparison.OrdinalIgnoreCase)).ToList();
        if (names.Count == 0) throw new Exception("No vault backup found in your TeraBox project.");
        Directory.CreateDirectory(Dir); int i = 0;
        foreach (var rn in names.OrderBy(n => n.EndsWith("salt.bin") || n.EndsWith("index.enc") ? 1 : 0))
        {
            i++; say($"Restoring {i}/{names.Count}…");
            var s = await Json(Rq(HttpMethod.Post, "/v1/fs/sign", JsonSerializer.Serialize(new { path = rn, ttl = 900 })), ct);
            await Net.Download(s.GetProperty("url").GetString()!, Path.Combine(Dir, rn[Pre.Length..]), (_, _, _) => { }, ct);
        }
        say($"Restored {names.Count} file(s)");
    }

    public static async Task BackupSettings(Action<string> say, CancellationToken ct)
    {
        var f = Path.Combine(Paths.Data, "settings.ini"); if (!File.Exists(f)) throw new Exception("No settings saved yet.");
        var lines = File.ReadAllLines(f).Where(l => !l.Split('=')[0].EndsWith("_key")).ToArray();
        var tmp = Path.Combine(Path.GetTempPath(), "UsbDesktop-settings.txt"); File.WriteAllLines(tmp, lines);
        const string rn = "usbdesktop-settings.ini"; var have = await Names(ct); say("Saving settings…");
        if (have.Contains(rn)) { try { using var d = await Net.Http.SendAsync(Rq(HttpMethod.Post, "/v1/fs/delete", JsonSerializer.Serialize(new { path = rn })), ct); } catch { } }
        await Upload(tmp, rn, ct); try { File.Delete(tmp); } catch { } say("Settings saved to your TeraBox vault");
    }

    public static async Task RestoreSettings(Action<string> say, CancellationToken ct)
    {
        const string rn = "usbdesktop-settings.ini"; if (!(await Names(ct)).Contains(rn)) throw new Exception("No settings backup found in your TeraBox project.");
        say("Loading settings…"); var s = await Json(Rq(HttpMethod.Post, "/v1/fs/sign", JsonSerializer.Serialize(new { path = rn, ttl = 900 })), ct);
        var tmp = Path.Combine(Path.GetTempPath(), "UsbDesktop-settings-in.txt"); await Net.Download(s.GetProperty("url").GetString()!, tmp, (_, _, _) => { }, ct);
        foreach (var l in File.ReadAllLines(tmp)) { var i = l.IndexOf('='); if (i > 0 && !l[..i].EndsWith("_key")) Cfg.Set(l[..i], l[(i + 1)..]); }
        try { File.Delete(tmp); } catch { } say("Settings loaded. Restart USB Desktop to apply them.");
    }

    public static void Settings()
    {
        using var f = new Form { Text = "Vault backup settings", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(480, 200), MaximizeBox = false, MinimizeBox = false, TopMost = true };
        var l1 = new Label { Text = "TeraBox vault address", Left = 16, Top = 14, AutoSize = true };
        var u = new TextBox { Left = 16, Top = 36, Width = 448, Text = Cfg.Get("vault_url", "https://terabox-vault-backend-1.onrender.com") };
        var l2 = new Label { Text = "API key (for the project that holds your backups)", Left = 16, Top = 72, AutoSize = true };
        var k = new TextBox { Left = 16, Top = 94, Width = 448, UseSystemPasswordChar = true, Text = Cfg.Get("vault_key") };
        var ok = new Button { Text = "Save", Left = 16, Top = 140, Width = 110, Height = 32, DialogResult = DialogResult.OK };
        f.Controls.AddRange(new Control[] { l1, u, l2, k, ok }); f.AcceptButton = ok;
        if (f.ShowDialog() == DialogResult.OK) { Cfg.Set("vault_url", u.Text.Trim()); Cfg.Set("vault_key", k.Text.Trim()); }
    }
}
