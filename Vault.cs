using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

static class VaultApp
{
    static string Dir => Path.Combine(Paths.Data, "vault");
    static byte[]? key; static Dictionary<string, string> idx = new();

    static byte[] Derive(string pw)
    {
        Directory.CreateDirectory(Dir); var sp = Path.Combine(Dir, "salt.bin");
        if (!File.Exists(sp)) File.WriteAllBytes(sp, RandomNumberGenerator.GetBytes(16));
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pw), File.ReadAllBytes(sp), 300000, HashAlgorithmName.SHA256, 32);
    }
    static byte[] Enc(byte[] plain)
    {
        var n = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var ct = new byte[plain.Length];
        using (var a = new AesGcm(key!, 16)) a.Encrypt(n, plain, ct, tag);
        var o = new byte[28 + ct.Length]; n.CopyTo(o, 0); tag.CopyTo(o, 12); ct.CopyTo(o, 28); return o;
    }
    static byte[] Dec(byte[] d)
    {
        var pt = new byte[d.Length - 28];
        using (var a = new AesGcm(key!, 16)) a.Decrypt(d.AsSpan(0, 12), d.AsSpan(28), d.AsSpan(12, 16), pt);
        return pt;
    }
    static void LoadIndex() { idx = JsonSerializer.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(Dec(File.ReadAllBytes(Path.Combine(Dir, "index.enc"))))) ?? new(); }
    static void SaveIndex() { File.WriteAllBytes(Path.Combine(Dir, "index.enc"), Enc(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(idx)))); }
    static void Clear() { if (key != null) Array.Clear(key); key = null; idx = new(); }

    public static Control View(DesktopForm sh)
    {
        var p = new Panel { BackColor = Color.White };
        p.Disposed += (_, _) => Clear();
        void Build() { p.Controls.Clear(); if (key == null) Locked(); else Unlocked(); }

        void Locked()
        {
            bool exists = File.Exists(Path.Combine(Dir, "index.enc"));
            Label L(string t, int y, float sz = 10, bool b = false) { var l = new Label { Text = t, Location = new Point(24, y), AutoSize = true, MaximumSize = new Size(560, 0), Font = new Font("Segoe UI", sz, b ? FontStyle.Bold : FontStyle.Regular) }; p.Controls.Add(l); return l; }
            L("🔐  Private Vault", 18, 18, true);
            L(exists ? "Enter your vault password." : "Create a vault password. It's separate from your login. If you forget it, the files can't be recovered.", 70);
            var pw = new TextBox { Bounds = new Rectangle(24, 130, 300, 30), UseSystemPasswordChar = true, Font = new Font("Segoe UI", 11) }; p.Controls.Add(pw);
            TextBox? c2 = null;
            if (!exists) { c2 = new TextBox { Bounds = new Rectangle(24, 170, 300, 30), UseSystemPasswordChar = true, Font = new Font("Segoe UI", 11), PlaceholderText = "Confirm password" }; p.Controls.Add(c2); }
            var msg = L("", exists ? 224 : 270);
            var go = new RoundButton { Text = exists ? "Unlock" : "Create vault", Bounds = new Rectangle(24, exists ? 174 : 214, 150, 36), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White };
            go.Click += (_, _) =>
            {
                try
                {
                    if (pw.Text.Length < 4) { msg.Text = "Use at least 4 characters"; return; }
                    if (!exists && c2!.Text != pw.Text) { msg.Text = "Passwords don't match"; return; }
                    key = Derive(pw.Text); if (exists) LoadIndex(); else { idx = new(); SaveIndex(); }
                    Build();
                }
                catch (CryptographicException) { Clear(); msg.Text = "Wrong password"; }
                catch (Exception ex) { Clear(); msg.Text = ex.Message; }
            };
            pw.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { go.PerformClick(); e.SuppressKeyPress = true; } };
            p.Controls.Add(go);
        }

        void Unlocked()
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(240, 240, 245), Padding = new Padding(6) };
            var lv = new ListView { Dock = DockStyle.Fill, View = System.Windows.Forms.View.Details, FullRowSelect = true, MultiSelect = true, Font = new Font("Segoe UI", 10) };
            lv.Columns.Add("Name", 380); lv.Columns.Add("Size", 120);
            void Fill()
            {
                lv.Items.Clear();
                foreach (var kv in idx.OrderBy(x => x.Key))
                {
                    long sz = 0; try { sz = Math.Max(0, new FileInfo(Path.Combine(Dir, kv.Value + ".enc")).Length - 28); } catch { }
                    lv.Items.Add(new ListViewItem(new[] { kv.Key, sz > 1048576 ? sz / 1048576 + " MB" : sz / 1024 + " KB" }) { Tag = kv.Value });
                }
            }
            Button B(string t, Action a) { var b = new RoundButton { Text = t, AutoSize = true, Height = 30 }; b.Click += (_, _) => a(); top.Controls.Add(b); return b; }
            B("➕ Add files", () =>
            {
                using var d = new OpenFileDialog { Multiselect = true }; if (d.ShowDialog() != DialogResult.OK) return;
                foreach (var f in d.FileNames)
                {
                    try
                    {
                        if (new FileInfo(f).Length > 1_500_000_000) { sh.Toast("Too big for the vault: " + Path.GetFileName(f)); continue; }
                        var g = Guid.NewGuid().ToString("N"); File.WriteAllBytes(Path.Combine(Dir, g + ".enc"), Enc(File.ReadAllBytes(f)));
                        var n = Path.GetFileName(f); int i = 2; while (idx.ContainsKey(n)) n = Path.GetFileNameWithoutExtension(f) + $" ({i++})" + Path.GetExtension(f);
                        idx[n] = g;
                    }
                    catch (Exception ex) { sh.Toast(ex.Message); }
                }
                SaveIndex(); Fill();
                if (MessageBox.Show("Delete the original files now?", "Vault", MessageBoxButtons.YesNo) == DialogResult.Yes) foreach (var f in d.FileNames) try { File.Delete(f); } catch { }
            });
            B("⬇ Export", () =>
            {
                if (lv.SelectedItems.Count == 0) return;
                using var d = new FolderBrowserDialog { SelectedPath = Paths.Desktop }; if (d.ShowDialog() != DialogResult.OK) return;
                foreach (ListViewItem it in lv.SelectedItems)
                    try { File.WriteAllBytes(U.Unique(d.SelectedPath, it.Text), Dec(File.ReadAllBytes(Path.Combine(Dir, (string)it.Tag! + ".enc")))); } catch (Exception ex) { sh.Toast(ex.Message); }
                sh.Reload(); sh.Toast("Exported " + lv.SelectedItems.Count + " file(s)");
            });
            B("🗑 Delete", () =>
            {
                if (lv.SelectedItems.Count == 0 || MessageBox.Show("Delete from the vault for good?", "Vault", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                foreach (ListViewItem it in lv.SelectedItems) { try { File.Delete(Path.Combine(Dir, (string)it.Tag! + ".enc")); } catch { } idx.Remove(it.Text); }
                SaveIndex(); Fill();
            });
            B("🔒 Lock", () => { Clear(); Build(); });
            p.Controls.Add(lv); p.Controls.Add(top); Fill();
        }
        Build(); return p;
    }
}
