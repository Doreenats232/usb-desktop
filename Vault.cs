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
        return U.Pbkdf2(Encoding.UTF8.GetBytes(pw), File.ReadAllBytes(sp), 300000);
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
        var p = new Panel { BackColor = Ui.Surface };
        p.Disposed += (_, _) => Clear();
        void Build() { foreach (Control c in p.Controls.Cast<Control>().ToList()) { p.Controls.Remove(c); c.Dispose(); } if (key == null) Locked(); else Unlocked(); Ui.Skin(p); Ui.Fit(p); }

        void Locked()
        {
            bool exists = File.Exists(Path.Combine(Dir, "index.enc"));
            var pg = new PagePanel(); pg.H("Private Vault");
            pg.P(exists ? "Enter your vault password." : "Create a vault password. It's separate from your login. If you forget it, the files can't be recovered.", true);
            var r1 = pg.Row(10); var (_, pw) = pg.Field(r1, "Vault password", 320, true);
            TextBox? c2 = null; if (!exists) { var r2 = pg.Row(2); var (_, t2) = pg.Field(r2, "Confirm password", 320, true); c2 = t2; }
            var msg = pg.P("", false, false, 10); msg.ForeColor = Color.FromArgb(255, 150, 150);
            var r3 = pg.Row(4);
            var go = pg.Btn(r3, exists ? "Unlock" : "Create vault", () =>
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
            });
            pw.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { go.PerformClick(); e.SuppressKeyPress = true; } };
            p.Controls.Add(pg);
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
            B("Add files", () =>
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
            B("Export", () =>
            {
                if (lv.SelectedItems.Count == 0) return;
                using var d = new FolderBrowserDialog { SelectedPath = Paths.Desktop }; if (d.ShowDialog() != DialogResult.OK) return;
                foreach (ListViewItem it in lv.SelectedItems)
                    try { File.WriteAllBytes(U.Unique(d.SelectedPath, it.Text), Dec(File.ReadAllBytes(Path.Combine(Dir, (string)it.Tag! + ".enc")))); } catch (Exception ex) { sh.Toast(ex.Message); }
                sh.Reload(); sh.Toast("Exported " + lv.SelectedItems.Count + " file(s)");
            });
            B("Delete", () =>
            {
                if (lv.SelectedItems.Count == 0 || MessageBox.Show("Delete from the vault for good?", "Vault", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                foreach (ListViewItem it in lv.SelectedItems) { try { File.Delete(Path.Combine(Dir, (string)it.Tag! + ".enc")); } catch { } idx.Remove(it.Text); }
                SaveIndex(); Fill();
            });
            B("Lock", () => { Clear(); Build(); });
            p.Controls.Add(lv); p.Controls.Add(top); Fill();
        }
        Action panic = () => { Clear(); if (!p.IsDisposed) p.BeginInvoke(new Action(Build)); };
        DesktopForm.PanicLock += panic; p.Disposed += (_, _) => DesktopForm.PanicLock -= panic;
        var lt = new System.Windows.Forms.Timer { Interval = 10000 };
        lt.Tick += (_, _) => { int m = Cfg.Int("vaultlock", 3); if (m > 0 && key != null && DateTime.Now - DesktopForm.LastInputAt > TimeSpan.FromMinutes(m)) { Clear(); Build(); sh.Toast("Vault locked after being idle"); } };
        lt.Start(); p.Disposed += (_, _) => lt.Dispose();
        Build(); return p;
    }
}
