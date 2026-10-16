using System.Text.Json;
namespace UsbDesktop;

// Native MLD Store list: reads the store's app list (JSON) and shows installable cards.
// Tries a few common routes; if none answer, the caller falls back to the web store.
static class StoreList
{
    class AppItem { public string Name = "", Desc = "", Ver = "", Url = "", Size = ""; }

    static string? Str(JsonElement e, params string[] keys)
    {
        foreach (var k in keys)
            foreach (var p in e.EnumerateObject())
                if (string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    return p.Value.ToString();
        return null;
    }

    static List<AppItem>? Parse(string json, string baseUrl)
    {
        try
        {
            using var doc = JsonDocument.Parse(json); var root = doc.RootElement; JsonElement arr = root;
            if (root.ValueKind == JsonValueKind.Object)
            {
                bool found = false;
                foreach (var k in new[] { "apps", "data", "items", "results" })
                    foreach (var p in root.EnumerateObject())
                        if (!found && string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array) { arr = p.Value; found = true; }
                if (!found) return null;
            }
            if (arr.ValueKind != JsonValueKind.Array) return null;
            var l = new List<AppItem>();
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var name = Str(e, "name", "title", "appName"); if (string.IsNullOrWhiteSpace(name)) continue;
                var id = Str(e, "id", "_id", "slug");
                var url = Str(e, "downloadUrl", "download_url", "download", "url", "link", "fileUrl");
                if (string.IsNullOrEmpty(url) && id != null) url = "/api/apps/" + id + "/download";
                if (string.IsNullOrEmpty(url)) continue;
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
                l.Add(new AppItem { Name = name!, Desc = Str(e, "description", "desc", "summary", "tagline") ?? "", Ver = Str(e, "version", "ver") ?? "", Url = url, Size = Str(e, "size", "fileSize") ?? "" });
            }
            return l;
        }
        catch { return null; }
    }

    static string Norm(string u) => u.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? u.TrimEnd('/') : "https://" + u.TrimEnd('/');

    // Returns a native panel, or null when the store has no readable app list.
    public static async Task<Control?> TryBuild(DesktopForm sh, string storeUrl, Action openWeb)
    {
        var b = Norm(storeUrl); List<AppItem>? items = null;
        foreach (var path in new[] { "/api/apps", "/api/apps/list", "/apps.json", "/api/store/apps" })
        {
            try { items = Parse(await Net.Text(b + path), b); if (items != null && items.Count > 0) break; items = null; } catch { items = null; }
        }
        if (items == null) return null;

        var pg = new PagePanel(); pg.H("MLD Store");
        var top = pg.Row(2); pg.Btn(top, "Open web store", openWeb, false);
        var (_, q) = pg.Field(top, "Search apps", 260);
        var list = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0, Ui.D(8), 0, 0) };
        pg.Controls.Add(list);

        void Fill()
        {
            list.SuspendLayout(); list.Controls.Clear(); var f = q.Text.Trim();
            foreach (var a in items.Where(x => f == "" || x.Name.Contains(f, StringComparison.OrdinalIgnoreCase) || x.Desc.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                var card = new RoundPanel { Width = Ui.D(640), Height = Ui.D(88), Fill = Ui.Surface2, Radius = Ui.D(14), Margin = new Padding(0, 0, 0, Ui.D(10)) };
                var nm = new Label { Text = a.Name + (a.Ver != "" ? "   v" + a.Ver : ""), Font = Ui.F(12, true), ForeColor = Ui.Text, BackColor = Color.Transparent, AutoSize = false, Bounds = new Rectangle(Ui.D(18), Ui.D(10), Ui.D(440), Ui.D(26)), AutoEllipsis = true };
                var ds = new Label { Text = a.Desc, Font = Ui.F(9.5f), ForeColor = Ui.Sub, BackColor = Color.Transparent, AutoSize = false, Bounds = new Rectangle(Ui.D(18), Ui.D(38), Ui.D(440), Ui.D(40)), AutoEllipsis = true };
                var app = a; var btn = new RoundButton { Text = "Install", BackColor = Ui.Accent, ForeColor = Color.White, Font = Ui.F(10), Bounds = new Rectangle(Ui.D(500), Ui.D(24), Ui.D(120), Ui.D(40)) };
                btn.Click += async (_, _) =>
                {
                    var safe = string.Concat(app.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                    var ext = Path.GetExtension(new Uri(app.Url).AbsolutePath); if (ext == "") ext = ".zip";
                    Directory.CreateDirectory(Paths.Apps);
                    await Installer.Get(sh, app.Url, Path.Combine(Paths.Apps, safe + ext), app.Name, true);
                };
                card.Controls.Add(nm); card.Controls.Add(ds); card.Controls.Add(btn); list.Controls.Add(card);
            }
            if (list.Controls.Count == 0) list.Controls.Add(new Label { Text = "No apps found", AutoSize = true, ForeColor = Ui.Sub, Font = Ui.F(10.5f) });
            list.ResumeLayout();
        }
        q.TextChanged += (_, _) => Fill(); Fill(); return pg;
    }
}
