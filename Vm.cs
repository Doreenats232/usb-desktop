using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
namespace UsbDesktop;

static class VmApp
{
    static Process? run;
    const int MonPort = 45517;
    static string FlagFile => Path.Combine(Paths.Vm, "frozen.txt");
    public static bool Running => run is { HasExited: false };
    public static bool Frozen => File.Exists(FlagFile);

    // Saves the whole running Linux (memory + devices) into the disk image, then closes it. Returns an error text, or null on success.
    public static string? Freeze()
    {
        if (!Running) return null;
        try
        {
            using var c = new System.Net.Sockets.TcpClient(); c.Connect("127.0.0.1", MonPort); var ns = c.GetStream(); var buf = new byte[8192];
            string Read(int ms) { var sb = new StringBuilder(); var end = DateTime.Now.AddMilliseconds(ms); ns.ReadTimeout = 1000; while (DateTime.Now < end) { try { int n = ns.Read(buf, 0, buf.Length); if (n <= 0) break; sb.Append(Encoding.ASCII.GetString(buf, 0, n)); if (sb.ToString().Contains("(qemu)")) break; } catch (IOException) { } } return sb.ToString(); }
            Read(5000); var w = Encoding.ASCII.GetBytes("savevm sleep\n"); ns.Write(w, 0, w.Length);
            var res = Read(240000);
            if (res.Contains("rror") || res.Contains("annot")) { var pd = Encoding.ASCII.GetBytes("system_powerdown\n"); ns.Write(pd, 0, pd.Length); return res.Replace("(qemu)", "").Trim(); }
            var q = Encoding.ASCII.GetBytes("quit\n"); ns.Write(q, 0, q.Length); run!.WaitForExit(20000);
            File.WriteAllText(FlagFile, "1"); return null;
        }
        catch (Exception ex) { return ex.Message; }
    }
    static string QemuDir => Path.Combine(Paths.Vm, "qemu");
    static string Disk => Path.Combine(Paths.Vm, "linux.qcow2");
    static string? Exe(string n) { foreach (var d in new[] { QemuDir, @"C:\Program Files\qemu" }) { var p = Path.Combine(d, n); if (File.Exists(p)) return p; } return null; }
    static string? Iso() => Directory.GetFiles(Paths.Vm, "*.iso").FirstOrDefault();

    static async Task<string> Latest(string dir, string pattern)
    {
        var html = await Net.Text(dir);
        var m = Regex.Matches(html, pattern).Select(x => x.Value).Distinct().OrderBy(x => x).ToList();
        if (m.Count == 0) throw new Exception("Couldn't find the download. Check your internet.");
        return dir + m.Last();
    }
    static async Task<bool> Dl(DesktopForm sh, string url, string dest, string title) => await Installer.Get(sh, url, dest, title, false, "vm");

    public static Control View(DesktopForm sh)
    {
        var p = new Panel { BackColor = Ui.Surface }; Ui.Own(p); var pg = new PagePanel(); p.Controls.Add(pg);
        RoundButton start = null!, stop = null!; ComboBox boot = null!; Label s1 = null!, s2 = null!, s3 = null!;
        RoundButton B(Control parent, string t, Func<Task> a, bool primary = true) { RoundButton? btn = null; btn = pg.Btn(parent, t, async () => { try { await a(); } catch (Exception ex) { st.Text = ex.Message; } Refresh(); }, primary); return btn; }

        pg.H("Full OS (Linux)"); pg.P("Runs a real Linux computer inside a window. Everything is saved on the USB.", true);
        pg.P("1  QEMU engine", false, true, 11, 12); s1 = pg.P("", true); var r1 = pg.Row(2);
        B(r1, "Install QEMU", async () =>
        {
            if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture != System.Runtime.InteropServices.Architecture.X64) throw new Exception("Full OS needs 64-bit Intel/AMD Windows");
            var url = await Latest("https://qemu.weilnetz.de/w64/", @"qemu-w64-setup-\d+\.exe(?![\w.])"); var inst = Path.Combine(Paths.Vm, "qemu-setup.exe");
            if (!await Dl(sh, url, inst, "QEMU installer")) return; st.Text = "Approve the Windows prompt to finish installing…"; Directory.CreateDirectory(QemuDir);
            using var pr = Process.Start(new ProcessStartInfo(inst, "/S /D=" + QemuDir) { UseShellExecute = true, Verb = "runas" })!; await pr.WaitForExitAsync();
            try { File.Delete(inst); } catch { }
            st.Text = Exe("qemu-system-x86_64.exe") != null ? "QEMU installed" : "QEMU didn't install. Try again and press Yes on the prompt.";
        });
        pg.P("2  Linux installer", false, true, 11, 12); s2 = pg.P("", true); var r2 = pg.Row(2);
        B(r2, "Get Alpine (70 MB)", async () =>
        {
            var url = await Latest("https://dl-cdn.alpinelinux.org/alpine/latest-stable/releases/x86_64/", @"alpine-virt-\d+\.\d+\.\d+-x86_64\.iso(?![\w.])");
            if (await Dl(sh, url, Path.Combine(Paths.Vm, Path.GetFileName(url)), "Alpine Linux")) st.Text = "Alpine downloaded";
        });
        B(r2, "Get Debian XFCE (3 GB)", async () =>
        {
            var url = await Latest("https://cdimage.debian.org/debian-cd/current-live/amd64/iso-hybrid/", @"debian-live-\d+\.\d+\.\d+-amd64-xfce\.iso(?![\w.])");
            if (await Dl(sh, url, Path.Combine(Paths.Vm, Path.GetFileName(url)), "Debian Linux")) st.Text = "Debian downloaded";
        }, false);
        pg.P("3  Virtual disk", false, true, 11, 12); s3 = pg.P("", true); var r3 = pg.Row(2);
        pg.Inline(r3, "Size (GB)"); var gb = pg.Num(r3, 4, 128, Cfg.Int("vm_gb", 16), 4);
        B(r3, "Create disk", async () =>
        {
            var img = Exe("qemu-img.exe") ?? throw new Exception("Install QEMU first"); Cfg.Set("vm_gb", ((int)gb.Value).ToString());
            using var pr = Process.Start(new ProcessStartInfo(img) { ArgumentList = { "create", "-f", "qcow2", Disk, gb.Value + "G" }, CreateNoWindow = true, UseShellExecute = false })!; await pr.WaitForExitAsync(); st.Text = "Disk created";
        });
        pg.P("Computer settings", false, true, 11, 14); var r4 = pg.Row(2);
        pg.Inline(r4, "RAM (MB)"); var ram = pg.Num(r4, 512, 32768, Cfg.Int("vm_ram", 2048), 256);
        pg.Inline(r4, "CPUs"); var cpu = pg.Num(r4, 1, 16, Cfg.Int("vm_cpu", 2), 1);
        var r5 = pg.Row(2); pg.Inline(r5, "Boot from");
        boot = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.F(10.5f), Width = Ui.D(230), BackColor = Ui.Surface2, ForeColor = Ui.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, Ui.D(6), 0, 0) };
        boot.Items.AddRange(new object[] { "Disk (installed Linux)", "Installer ISO (first time)" }); boot.SelectedIndex = Math.Clamp(Cfg.Int("vm_boot", 1), 0, 1); r5.Controls.Add(boot);
        pg.P("First time: boot from the installer ISO, install Linux onto the disk, then switch to Disk.", true, false, 10, 8);
        pg.P("Alpine: log in as root, run  setup-alpine.  Tip: turn on 'Windows Hypervisor Platform' in Windows Features for much faster speed.", true, false, 10);
        var r6 = pg.Row(14);
        start = B(r6, "Start Full OS", () =>
        {
            Cfg.Set("vm_ram", ((int)ram.Value).ToString()); Cfg.Set("vm_cpu", ((int)cpu.Value).ToString()); Cfg.Set("vm_boot", boot.SelectedIndex.ToString());
            var exe = Exe("qemu-system-x86_64.exe")!; var psi = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false };
            foreach (var a in new[] { "-m", ram.Value.ToString(), "-smp", cpu.Value.ToString(), "-accel", "whpx,kernel-irqchip=off", "-accel", "tcg", "-drive", $"file={Disk},format=qcow2,if=virtio", "-nic", "user,model=virtio-net-pci", "-device", "qemu-xhci", "-device", "usb-tablet", "-vga", "std", "-name", "USB Desktop Linux" }) psi.ArgumentList.Add(a);
            if (boot.SelectedIndex == 1 && Iso() != null) { psi.ArgumentList.Add("-cdrom"); psi.ArgumentList.Add(Iso()!); psi.ArgumentList.Add("-boot"); psi.ArgumentList.Add("d"); }
            psi.ArgumentList.Add("-monitor"); psi.ArgumentList.Add($"tcp:127.0.0.1:{MonPort},server,nowait");
            bool resuming = Frozen; var t0 = DateTime.Now; if (resuming) { psi.ArgumentList.Add("-loadvm"); psi.ArgumentList.Add("sleep"); try { File.Delete(FlagFile); } catch { } }
            run = Process.Start(psi); run!.EnableRaisingEvents = true; if (resuming) run.Exited += (_, _) => { if ((DateTime.Now - t0).TotalSeconds < 20 && !p.IsDisposed) p.BeginInvoke(new Action(() => st.Text = "Couldn't resume the saved Linux. Press Start to boot it fresh.")); }; run.Exited += (_, _) => { if (!p.IsDisposed) p.BeginInvoke(new Action(Refresh)); };
            st.Text = resuming ? "Resuming Linux exactly where you left it…" : "Linux is starting. A new window will open."; return Task.CompletedTask;
        });
        stop = B(r6, "Stop", () => { try { run?.Kill(); } catch { } return Task.CompletedTask; }, false); stop.BackColor = Color.FromArgb(160, 60, 70);
        st = pg.P("", true, false, 10.5f, 8);

        void Refresh()
        {
            if (p.IsDisposed) return;
            bool q = Exe("qemu-system-x86_64.exe") != null, d = File.Exists(Disk), i = Iso() != null, on = run is { HasExited: false };
            s1.Text = q ? "Installed" : "Not installed yet"; s2.Text = i ? Path.GetFileName(Iso()!) : "No installer downloaded yet"; s3.Text = d ? $"linux.qcow2 ({new FileInfo(Disk).Length / 1048576} MB used)" : "No disk yet";
            s1.ForeColor = q ? Color.FromArgb(110, 231, 160) : Ui.Sub; s2.ForeColor = i ? Color.FromArgb(110, 231, 160) : Ui.Sub; s3.ForeColor = d ? Color.FromArgb(110, 231, 160) : Ui.Sub;
            start.Enabled = q && d && !on && (boot.SelectedIndex == 0 || i); stop.Enabled = on; if (on) st.Text = "Linux is running";
        }
        boot.SelectedIndexChanged += (_, _) => Refresh(); Refresh();
        p.Tag = new StateApi { Get = () => Frozen ? "frozen" : null, Set = d => { if (d == "frozen") Task.Delay(1800).ContinueWith(_ => { if (!p.IsDisposed) p.BeginInvoke(new Action(() => { if (start.Enabled) start.PerformClick(); })); }); } };
        return p;
    }
    static Label st = null!;
}
