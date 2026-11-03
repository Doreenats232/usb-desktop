using System.Diagnostics;
namespace UsbDesktop;

// Several people on one USB: each profile has its own data (password, settings, AI history) and Desktop folder.
// "main" is the original one; the others live in the profiles folder next to Launcher.exe.
static class Profiles
{
    public static string Current => Paths.Profile == "" ? "main" : Paths.Profile;

    public static List<string> List()
    {
        var l = new List<string> { "main" };
        try { var d = Path.Combine(Paths.Root, "profiles"); if (Directory.Exists(d)) l.AddRange(Directory.GetDirectories(d).Select(x => Path.GetFileName(x)!).Where(x => x != "")); } catch { }
        return l;
    }

    public static string Clean(string s) => new string(s.Trim().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

    public static void SwitchTo(string name)
    {
        try
        {
            name = name == "main" ? "" : Clean(name);
            var psi = new ProcessStartInfo(Environment.ProcessPath!, name == "" ? "" : "--profile " + name) { UseShellExecute = true, WorkingDirectory = Paths.Root };
            Process.Start(psi);
            Application.Exit();
        }
        catch (Exception ex) { MessageBox.Show("Couldn't switch profile: " + ex.Message, "USB Desktop"); }
    }

    static string? Ask()
    {
        using var f = new Form { Text = "New profile", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(360, 130), MaximizeBox = false, MinimizeBox = false, TopMost = true };
        var l = new Label { Text = "Name for the new profile (letters and numbers):", Left = 12, Top = 12, Width = 336 };
        var t = new TextBox { Left = 12, Top = 38, Width = 336 };
        var ok = new Button { Text = "Create", Left = 12, Top = 84, Width = 100, Height = 32, DialogResult = DialogResult.OK };
        var no = new Button { Text = "Cancel", Left = 248, Top = 84, Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, t, ok, no }); f.AcceptButton = ok; f.CancelButton = no;
        return f.ShowDialog() == DialogResult.OK ? Clean(t.Text) : null;
    }

    public static void ShowMenu(DesktopForm sh, Point at)
    {
        var m = new ContextMenuStrip();
        m.Items.Add(new ToolStripLabel("Switch user") { Enabled = false });
        foreach (var p in List())
        {
            var name = p; bool cur = name == Current;
            var it = new ToolStripMenuItem((cur ? "● " : "   ") + (name == "main" ? "Main profile" : name)) { Enabled = !cur };
            it.Click += (_, _) => { if (MessageBox.Show("Switch to \"" + name + "\"? USB Desktop will restart.", "USB Desktop", MessageBoxButtons.YesNo) == DialogResult.Yes) SwitchTo(name); };
            m.Items.Add(it);
        }
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("➕ New profile…", null, (_, _) =>
        {
            var n = Ask(); if (string.IsNullOrEmpty(n)) return;
            if (n.Equals("main", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("\"main\" is the original profile.", "USB Desktop"); return; }
            SwitchTo(n);
        });
        m.Show(at);
    }
}
