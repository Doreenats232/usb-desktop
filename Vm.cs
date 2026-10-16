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
    static string? pendingPw; static bool pendingDesk; static System.Net.Sockets.TcpListener? seedSrv;
    const int SeedPort = 45518;

    // Tiny local web server that hands Linux its first-boot settings (user, password, desktop) so nothing has to be typed inside the VM.
    static void StartSeed(string pw, bool desktop)
    {
        string user = "user"; var ud = new StringBuilder("#cloud-config\nhostname: usb-linux\nusers:\n  - name: " + user + "\n    gecos: USB Desktop user\n    sudo: ALL=(ALL) NOPASSWD:ALL\n    shell: /bin/bash\n    groups: [sudo]\n    lock_passwd: false\n");
        ud.Append("chpasswd:\n  expire: false\n  list: |\n    " + user + ":" + pw.Replace("\n", "") + "\nssh_pwauth: true\n");
        if (desktop)
        {
            ud.Append("package_update: true\npackages:\n  - xorg\n  - xfce4\n  - xfce4-terminal\n  - lightdm\n  - firefox-esr\n  - dbus-x11\n");
            ud.Append("write_files:\n  - path: /etc/lightdm/lightdm.conf.d/50-autologin.conf\n    content: |\n      [Seat:*]\n      autologin-user=" + user + "\n      autologin-user-timeout=0\n");
            ud.Append("runcmd:\n  - [ systemctl, set-default, graphical.target ]\n  - [ systemctl, enable, lightdm ]\n  - [ systemctl, start, lightdm ]\n");
        }
        var files = new Dictionary<string, string> { ["/user-data"] = ud.ToString(), ["/meta-data"] = "instance-id: usb-desktop-1\nlocal-hostname: usb-linux\n", ["/vendor-data"] = "" };
        try { seedSrv?.Stop(); } catch { }
        var srv = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, SeedPort); srv.Start(); seedSrv = srv; var until = DateTime.Now.AddMinutes(25);
        Task.Run(async () =>
        {
            while (DateTime.Now < until)
            {
                try
                {
                    var c = await srv.AcceptTcpClientAsync(); using (c) { var ns = c.GetStream(); var buf = new byte[4096]; int n = await ns.ReadAsync(buf, 0, buf.Length); var req = Encoding.ASCII.GetString(buf, 0, n); var path = req.Split(' ').Skip(1).FirstOrDefault()?.Split('?')[0] ?? "";
                        byte[] body = Encoding.UTF8.GetBytes(files.TryGetValue(path, out var txt) ? txt : ""); string head = files.ContainsKey(path) ? "HTTP/1.0 200 OK" : "HTTP/1.0 404 Not Found";
                        var hb = Encoding.ASCII.GetBytes(head + "\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n"); await ns.WriteAsync(hb, 0, hb.Length); await ns.WriteAsync(body, 0, body.Length); }
                }
                catch { break; }
            }
            try { srv.Stop(); } catch { }
        });
    }

    static async Task EnsureQemu(DlCard card, int step, CancellationToken ct)
    {
        if (Exe("qemu-system-x86_64.exe") != null) return;
        if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture != System.Runtime.InteropServices.Architecture.X64) throw new Exception("Full OS needs 64-bit Intel/AMD Windows");
        card.Stage($"Step {step}/5 · Finding the latest QEMU…", 4);
        var url = await Latest("https://qemu.weilnetz.de/w64/", @"qemu-w64-setup-\d+\.exe(?![\w.])"); var inst = Path.Combine(Paths.Vm, "qemu-setup.exe");
        await Net.Download(url, inst, (g, t, s) => card.Stage($"Step {step}/5 · Downloading QEMU  {Ui.Bytes(g)}" + (t > 0 ? " of " + Ui.Bytes(t) : ""), t > 0 ? 4 + 20.0 * g / t : -1), ct);
        card.Stage($"Step {step}/5 · Installing QEMU… press Yes on the Windows prompt", 26); Directory.CreateDirectory(QemuDir);
        using var pr = Process.Start(new ProcessStartInfo(inst, "/S /D=" + QemuDir) { UseShellExecute = true, Verb = "runas" })!; await pr.WaitForExitAsync();
        try { File.Delete(inst); } catch { }
        if (Exe("qemu-system-x86_64.exe") == null) throw new Exception("QEMU didn't install. Run the setup again and press Yes on the Windows prompt.");
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
        pg.P("Easy setup", false, true, 13, 14); pg.P("One button. It looks for what's already on this USB, downloads and installs whatever is missing, asks you only a few simple questions, and starts Linux ready to log in.", true);
        var rA = pg.Row(6); B(rA, "Set up Full OS automatically", () => Auto());
        pg.P("Manual setup (optional)", false, true, 12, 20);
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
            if (pendingPw != null) { StartSeed(pendingPw, pendingDesk); pendingPw = null; psi.ArgumentList.Add("-smbios"); psi.ArgumentList.Add("type=1,serial=ds=nocloud-net;s=http://10.0.2.2:" + SeedPort + "/"); }
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
        async Task Auto()
        {
            var cts = new CancellationTokenSource(); var card = sh.NewDl("Setting up Full OS", cts, "");
            try
            {
                card.Stage("Step 1/5 · Looking for what's already on your USB…", 2); await Task.Delay(700);
                bool hadDisk = File.Exists(Disk);
                await EnsureQemu(card, 2, cts.Token);
                bool fresh = !hadDisk;
                if (hadDisk) { int c = Ask.Choose("A Linux disk is already on this USB", "Keep using it, or start over with a fresh Linux?", "Keep using it", "Replace with a fresh Linux"); if (c < 0) { card.Fail("Cancelled"); return; } fresh = c == 1; }
                if (fresh)
                {
                    card.Stage("Step 3/5 · A few quick questions…", 28);
                    int style = Ask.Choose("What kind of Linux?", "Desktop looks like a normal PC with windows, icons and a web browser. Terminal is smaller and starts faster.", "Desktop (XFCE)", "Terminal only"); if (style < 0) { card.Fail("Cancelled"); return; }
                    int size = Ask.Choose("How much space?", "This is just a file on your USB and it only grows as you use it.", "8 GB", "16 GB", "32 GB"); if (size < 0) { card.Fail("Cancelled"); return; }
                    var pw = Ask.Password("Choose a Linux password", "You'll log in as  user  with this password."); if (string.IsNullOrEmpty(pw)) { card.Fail("Cancelled"); return; }
                    var part = Disk + ".part";
                    await Net.Download("https://cloud.debian.org/images/cloud/bookworm/latest/debian-12-genericcloud-amd64.qcow2", part, (g, t, s) => card.Stage($"Step 4/5 · Downloading Linux  {Ui.Bytes(g)}" + (t > 0 ? " of " + Ui.Bytes(t) : "") + $"  ·  {Ui.Bytes((long)s)}/s", t > 0 ? 30 + 56.0 * g / t : -1), cts.Token);
                    card.Stage("Step 4/5 · Preparing your disk…", 88);
                    if (File.Exists(Disk)) File.Delete(Disk); File.Move(part, Disk); try { File.Delete(FlagFile); } catch { }
                    var img = Exe("qemu-img.exe") ?? throw new Exception("qemu-img is missing. Reinstall QEMU.");
                    using (var pr = Process.Start(new ProcessStartInfo(img) { ArgumentList = { "resize", Disk, new[] { 8, 16, 32 }[size] + "G" }, CreateNoWindow = true, UseShellExecute = false })!) await pr.WaitForExitAsync();
                    pendingPw = pw; pendingDesk = style == 0;
                }
                card.Stage("Step 5/5 · Starting Linux…", 96); Cfg.Set("vm_boot", "0"); boot.SelectedIndex = 0; Refresh(); await Task.Delay(500);
                if (!start.Enabled) throw new Exception("Everything downloaded but Linux couldn't start. Press Start Full OS below.");
                start.PerformClick();
                card.Done(fresh ? "Linux is starting. The first start takes several minutes while it sets itself up. Log in as user." : "Linux is starting.");
            }
            catch (OperationCanceledException) { card.Fail("Cancelled"); }
            catch (Exception ex) { card.Fail(ex.Message); }
        }
        boot.SelectedIndexChanged += (_, _) => Refresh(); Refresh();
        p.Tag = new StateApi { Get = () => Frozen ? "frozen" : null, Set = d => { if (d == "frozen") Task.Delay(1800).ContinueWith(_ => { if (!p.IsDisposed) p.BeginInvoke(new Action(() => { if (start.Enabled) start.PerformClick(); })); }); } };
        return p;
    }
    static Label st = null!;
}
