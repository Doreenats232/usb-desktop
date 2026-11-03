using System.Text.Json;
namespace UsbDesktop;

// A simple to-do list: a small window, plus commands for the assistant and the phone app. Saved in data\todo.json.
static class Todo
{
    public class Item { public string T { get; set; } = ""; public bool D { get; set; } }
    static string F => Path.Combine(Paths.Data, "todo.json");
    public static event Action? Changed;

    static List<Item> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(F)) ?? new() : new(); } catch { return new(); } }
    static void Save(List<Item> l) { try { Directory.CreateDirectory(Paths.Data); File.WriteAllText(F, JsonSerializer.Serialize(l)); } catch { } Changed?.Invoke(); }

    // add TEXT | list | done N | del N | clear
    public static string Run(string a)
    {
        var i = a.IndexOf(' '); var cmd = (i > 0 ? a[..i] : a).Trim().ToLowerInvariant(); var arg = i > 0 ? a[(i + 1)..].Trim() : "";
        var l = Load();
        switch (cmd)
        {
            case "add": if (arg == "") return "What should I add?"; l.Add(new Item { T = arg }); Save(l); return "Added to your to-do: " + arg;
            case "done": if (int.TryParse(arg, out var n) && n >= 1 && n <= l.Count) { l[n - 1].D = !l[n - 1].D; Save(l); if (l[n - 1].D) Pet.React("✅ Done: " + l[n - 1].T, 5, true); return l[n - 1].D ? "Ticked off." : "Marked as not done."; } return "There is no item " + arg + ".";
            case "del": if (int.TryParse(arg, out var d) && d >= 1 && d <= l.Count) { l.RemoveAt(d - 1); Save(l); return "Removed."; } return "There is no item " + arg + ".";
            case "clear": l.RemoveAll(x => x.D); Save(l); return "Cleared the finished items.";
            default:
                if (l.Count == 0) return "✅ To-do\nNothing on your list. 🎉";
                return "✅ To-do\n" + string.Join("\n", l.Select((x, k) => (k + 1) + ". " + (x.D ? "✔ " : "") + x.T));
        }
    }

    public static Control View(DesktopForm sh)
    {
        var root = new Panel { BackColor = Ui.Surface }; Ui.Own(root);
        var top = new Panel { Dock = DockStyle.Top, Height = Ui.D(52), BackColor = Ui.Surface2, Padding = new Padding(Ui.D(10)) };
        var add = new RoundButton { Text = "Add", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Dock = DockStyle.Right, Width = Ui.D(80), Font = Ui.F(10) };
        var (box, tb) = Ui.Input("Add a task…", false, 32); box.Dock = DockStyle.Fill;
        top.Controls.Add(box); top.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Ui.D(8) }); top.Controls.Add(add);
        var clb = new CheckedListBox { Dock = DockStyle.Fill, BackColor = Ui.Surface, ForeColor = Ui.Text, BorderStyle = BorderStyle.None, Font = Ui.F(11), CheckOnClick = true, IntegralHeight = false };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = Ui.D(46), BackColor = Ui.Surface2, Padding = new Padding(Ui.D(8)) };
        var del = new RoundButton { Text = "Delete selected", Pill = true, BackColor = Ui.Line, ForeColor = Ui.Text, Dock = DockStyle.Left, Width = Ui.D(140), Font = Ui.F(9) };
        var clr = new RoundButton { Text = "Clear finished", Pill = true, BackColor = Ui.Line, ForeColor = Ui.Text, Dock = DockStyle.Right, Width = Ui.D(130), Font = Ui.F(9) };
        bottom.Controls.Add(del); bottom.Controls.Add(clr);
        root.Controls.Add(clb); root.Controls.Add(bottom); root.Controls.Add(top);

        bool loading = false;
        void Refresh()
        {
            loading = true; clb.Items.Clear();
            foreach (var it in Load()) clb.Items.Add(it.T, it.D);
            loading = false;
        }
        clb.ItemCheck += (_, e) =>
        {
            if (loading) return; var l = Load(); if (e.Index < 0 || e.Index >= l.Count) return;
            l[e.Index].D = e.NewValue == CheckState.Checked; Save(l);
            if (l[e.Index].D) Pet.React("✅ Nice, one less!", 4);
        };
        void Add() { var t = tb.Text.Trim(); if (t == "") return; var l = Load(); l.Add(new Item { T = t }); tb.Text = ""; Save(l); }
        add.Click += (_, _) => Add();
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Add(); e.SuppressKeyPress = true; } };
        del.Click += (_, _) => { var i = clb.SelectedIndex; if (i < 0) return; var l = Load(); if (i < l.Count) { l.RemoveAt(i); Save(l); } };
        clr.Click += (_, _) => { var l = Load(); l.RemoveAll(x => x.D); Save(l); };
        Action changed = () => { try { root.BeginInvoke(new Action(() => { if (!root.IsDisposed) Refresh(); })); } catch { } };
        Changed += changed; root.Disposed += (_, _) => Changed -= changed;
        Refresh();
        return root;
    }
}
