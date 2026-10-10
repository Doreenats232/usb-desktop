using System.Diagnostics;
using System.Text.RegularExpressions;
namespace UsbDesktop;

static class VmApp
{
    static Process? run;
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromHours(2) };
    static string QemuDir => Path.Combine(Paths.Vm, "qemu");
    static string Disk => Path.Combine(Paths.Vm, "linux.qcow2");
    static string? Exe(string n) { foreach (var d in new[] { QemuDir, @"C:\Program Files\qemu" }) { var p = Path.Combine(d, n); if (File.Exists(p)) return p; } return null; }
    static string? Iso() => Directory.GetFiles(Paths.Vm, "*.iso").FirstOrDefault();

    static async Task<string> Latest(string dir, string pattern)
    {
        var html = await http.GetStringAsync(dir);
        var m = Regex.Matches(html, pattern).Select(x => x.Value).Distinct().OrderBy(x => x).ToList();
        if (m.Count == 0) throw new Exception("Couldn't find the download. Check your internet.");
        return dir + m.Last();
    }
    static async Task Download(string url, string dest, ProgressBar pb, Label st)
    {
        using var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); r.EnsureSuccessStatusCode();
        long tot = r.Content.Headers.ContentLength ?? -1, got = 0; int n; var buf = new byte[81920]; var sw = Stopwatch.StartNew();
        using (var s = await r.Content.ReadAsStreamAsync())
        using (var f = File.Create(dest + ".part"))
            while ((n = await s.ReadAsync(buf)) > 0)
            {
                await f.WriteAsync(buf.AsMemory(0, n)); got += n;
                if (sw.ElapsedMilliseconds > 250 && !pb.IsDisposed) { sw.Restart(); if (tot > 0) pb.Value = (int)(got * 100 / tot); st.Text = $"Downloading… {got / 1048576} MB" + (tot > 0 ? $" of {tot / 1048576} MB" : ""); }
            }
        File.Move(dest + ".part", dest, true); if (!pb.IsDisposed) pb.Value = 100;
    }

    public static Control View(DesktopForm sh)
    {
        var p = new Panel { BackColor = Color.White, AutoScroll = true };
        Button start = null!, stop = null!; ComboBox boot = null!;
        Label L(string t, int x, int y, float sz = 10, bool b = false) { var l = new Label { Text = t, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", sz, b ? FontStyle.Bold : FontStyle.Regular) }; p.Controls.Add(l); return l; }
        Button B(string t, int x, int y, int w, Func<Task> a) { var b = new Button { Text = t, Bounds = new Rectangle(x, y, w, 34), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderSize = 0; b.Click += async (_, _) => { try { await a(); } catch (Exception ex) { st.Text = "⚠ " + ex.Message; } Refresh(); }; p.Controls.Add(b); return b; }
        NumericUpDown N(int x, int y, int min, int max, int val, int inc) { var n = new NumericUpDown { Bounds = new Rectangle(x, y, 90, 28), Minimum = min, Maximum = max, Value = Math.Clamp(val, min, max), Increment = inc, Font = new Font("Segoe UI", 10.5f) }; p.Controls.Add(n); return n; }

        L("Full OS (Linux)", 24, 16, 18, true);
        L("Runs a real Linux computer inside a window. Everything is saved on the USB.", 24, 54, 10);
        var s1 = L("", 24, 96, 11); var s2 = L("", 24, 156, 11); var s3 = L("", 24, 216, 11);
        st = L("", 24, 420, 10); pb = new ProgressBar { Bounds = new Rectangle(24, 396, 640, 14) }; p.Controls.Add(pb);

        L("1  QEMU engine", 24, 76, 10, true); L("2  Linux installer", 24, 136, 10, true); L("3  Virtual disk", 24, 196, 10, true);
        B("Install QEMU", 420, 90, 150, async () =>
        {
            var url = await Latest("https://qemu.weilnetz.de/w64/", @"qemu-w64-setup-\d+\.exe(?![\w.])"); var inst = Path.Combine(Paths.Vm, "qemu-setup.exe");
            await Download(url, inst, pb, st); st.Text = "Approve the Windows prompt to finish installing…"; Directory.CreateDirectory(QemuDir);
            using var pr = Process.Start(new ProcessStartInfo(inst, "/S /D=" + QemuDir) { UseShellExecute = true, Verb = "runas" })!; await pr.WaitForExitAsync();
            try { File.Delete(inst); } catch { }
            st.Text = Exe("qemu-system-x86_64.exe") != null ? "✅ QEMU installed" : "QEMU didn't install. Try again and press Yes on the prompt.";
        });
        B("Get Alpine (70 MB)", 420, 150, 150, async () =>
        {
            var url = await Latest("https://dl-cdn.alpinelinux.org/alpine/latest-stable/releases/x86_64/", @"alpine-virt-\d+\.\d+\.\d+-x86_64\.iso(?![\w.])");
            await Download(url, Path.Combine(Paths.Vm, Path.GetFileName(url)), pb, st); st.Text = "✅ Alpine downloaded";
        });
        B("Get Debian XFCE (3 GB)", 576, 150, 170, async () =>
        {
            var url = await Latest("https://cdimage.debian.org/debian-cd/current-live/amd64/iso-hybrid/", @"debian-live-\d+\.\d+\.\d+-amd64-xfce\.iso(?![\w.])");
            await Download(url, Path.Combine(Paths.Vm, Path.GetFileName(url)), pb, st); st.Text = "✅ Debian downloaded";
        });
        var gb = N(420, 214, 4, 128, Cfg.Int("vm_gb", 16), 4); L("GB", 516, 218, 10);
        B("Create disk", 560, 210, 120, async () =>
        {
            var img = Exe("qemu-img.exe") ?? throw new Exception("Install QEMU first"); Cfg.Set("vm_gb", ((int)gb.Value).ToString());
            using var pr = Process.Start(new ProcessStartInfo(img) { ArgumentList = { "create", "-f", "qcow2", Disk, gb.Value + "G" }, CreateNoWindow = true, UseShellExecute = false })!; await pr.WaitForExitAsync(); st.Text = "✅ Disk created";
        });

        L("RAM (MB)", 24, 272, 10); var ram = N(110, 268, 512, 32768, Cfg.Int("vm_ram", 2048), 256);
        L("CPUs", 230, 272, 10); var cpu = N(280, 268, 1, 16, Cfg.Int("vm_cpu", 2), 1);
        L("Boot from", 400, 272, 10); boot = new ComboBox { Bounds = new Rectangle(480, 268, 180, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10.5f) };
        boot.Items.AddRange(new object[] { "Disk (installed Linux)", "Installer ISO (first time)" }); boot.SelectedIndex = Math.Clamp(Cfg.Int("vm_boot", 1), 0, 1); p.Controls.Add(boot);
        L("First time: boot from the installer ISO, install Linux onto the disk, then switch to Disk.", 24, 312, 9.5f);
        L("Alpine: log in as root, run  setup-alpine.   Tip: turn on 'Windows Hypervisor Platform' in Windows Features for much faster speed.", 24, 336, 9.5f);

        start = B("▶  Start Full OS", 24, 456, 200, () =>
        {
            Cfg.Set("vm_ram", ((int)ram.Value).ToString()); Cfg.Set("vm_cpu", ((int)cpu.Value).ToString()); Cfg.Set("vm_boot", boot.SelectedIndex.ToString());
            var exe = Exe("qemu-system-x86_64.exe")!; var psi = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false };
            foreach (var a in new[] { "-m", ram.Value.ToString(), "-smp", cpu.Value.ToString(), "-accel", "whpx,kernel-irqchip=off", "-accel", "tcg", "-drive", $"file={Disk},format=qcow2,if=virtio", "-nic", "user,model=virtio-net-pci", "-device", "qemu-xhci", "-device", "usb-tablet", "-vga", "std", "-name", "USB Desktop Linux" }) psi.ArgumentList.Add(a);
            if (boot.SelectedIndex == 1 && Iso() != null) { psi.ArgumentList.Add("-cdrom"); psi.ArgumentList.Add(Iso()!); psi.ArgumentList.Add("-boot"); psi.ArgumentList.Add("d"); }
            run = Process.Start(psi); run!.EnableRaisingEvents = true; run.Exited += (_, _) => { if (!p.IsDisposed) p.BeginInvoke(new Action(Refresh)); };
            st.Text = "Linux is starting. A new window will open."; return Task.CompletedTask;
        });
        stop = B("■  Stop", 236, 456, 100, () => { try { run?.Kill(); } catch { } return Task.CompletedTask; });
        stop.BackColor = Color.FromArgb(150, 60, 60);

        void Refresh()
        {
            if (p.IsDisposed) return;
            bool q = Exe("qemu-system-x86_64.exe") != null, d = File.Exists(Disk), i = Iso() != null, on = run is { HasExited: false };
            s1.Text = q ? "✅ Installed" : "❌ Not installed"; s2.Text = i ? "✅ " + Path.GetFileName(Iso()!) : "❌ No installer downloaded"; s3.Text = d ? $"✅ linux.qcow2 ({new FileInfo(Disk).Length / 1048576} MB used)" : "❌ No disk yet";
            start.Enabled = q && d && !on && (boot.SelectedIndex == 0 || i); stop.Enabled = on; if (on) st.Text = "🟢 Linux is running";
        }
        boot.SelectedIndexChanged += (_, _) => Refresh(); Refresh(); return p;
    }
    static Label st = null!; static ProgressBar pb = null!;
}
