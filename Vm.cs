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
    static void StartSeed(string pw, bool desktop, string distro)
    {
        string user = "user"; var ud = new StringBuilder("#cloud-config\nhostname: usb-linux\nusers:\n  - name: " + user + "\n    gecos: USB Desktop user\n    sudo: ALL=(ALL) NOPASSWD:ALL\n    shell: /bin/bash\n    groups: [" + (distro == "alpine" ? "wheel" : "sudo") + "]\n    lock_passwd: false\n");
        ud.Append("chpasswd:\n  expire: false\n  list: |\n    " + user + ":" + pw.Replace("\n", "") + "\nssh_pwauth: true\n");
        if (desktop)
        {
            ud.Append("package_update: true\npackages:\n  - xorg\n  - xfce4\n  - xfce4-terminal\n  - lightdm\n  - lightdm-gtk-greeter\n  - " + (distro == "ubuntu" ? "falkon" : "firefox-esr") + "\n  - dbus-x11\n");
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
        card.Stage($"Step {step}/5 · QEMU not found, installing it…", 10);
        var url = await Latest("https://qemu.weilnetz.de/w64/", @"qemu-w64-setup-\d+\.exe(?![\w.])"); var inst = Path.Combine(Paths.Vm, "qemu-setup.exe");
        await Net.Download(url, inst, (g, t, s) => card.Stage($"Step {step}/5 · Downloading QEMU  {Ui.Bytes(g)}" + (t > 0 ? " of " + Ui.Bytes(t) : ""), t > 0 ? 10 + 14.0 * g / t : -1), ct);
        card.Stage($"Step {step}/5 · Installing QEMU… press Yes on the Windows prompt", 26); Directory.CreateDirectory(QemuDir);
        using var pr = Process.Start(new ProcessStartInfo(inst, "/S /D=" + QemuDir) { UseShellExecute = true, Verb = "runas" })!; await pr.WaitForExitAsync();
        try { File.Delete(inst); } catch { }
        Rescan(); if (Exe("qemu-system-x86_64.exe") == null) throw new Exception("QEMU didn't install. Run the setup again and press Yes on the Windows prompt.");
    }
    static string QemuDir => Path.Combine(Paths.Vm, "qemu");
    static string CurId => Cfg.Get("vm_distro", "debian");
    static string DiskFor(string id) { var p = Path.Combine(Paths.Vm, $"linux-{id}.qcow2"); var legacy = Path.Combine(Paths.Vm, "linux.qcow2"); return id == "debian" && !File.Exists(p) && File.Exists(legacy) ? legacy : p; }
    static string DiskDefault => DiskFor(CurId);
    static string Disk => DiskDefault;

    class Distro { public string Id = "", Name = "", Note = "", Kind = "cloud", Url = "", Dir = "", Pattern = ""; public int NeedGb; public bool Desktop; }
    static readonly Distro[] Distros =
    {
        new() { Id = "debian", Name = "Debian 12", Note = "stable and simple, desktop or terminal", Kind = "cloud", Url = "https://cloud.debian.org/images/cloud/bookworm/latest/debian-12-genericcloud-amd64.qcow2", NeedGb = 6, Desktop = true },
        new() { Id = "ubuntu", Name = "Ubuntu 24.04", Note = "the most popular, desktop or terminal", Kind = "cloud", Url = "https://cloud-images.ubuntu.com/noble/current/noble-server-cloudimg-amd64.img", NeedGb = 8, Desktop = true },
        new() { Id = "alpine", Name = "Alpine Linux", Note = "tiny and very fast, terminal only", Kind = "cloud", Dir = "https://dl-cdn.alpinelinux.org/alpine/latest-stable/releases/cloud/", Pattern = @"nocloud_alpine-\d+\.\d+\.\d+-x86_64-bios-cloudinit-r\d+\.qcow2", NeedGb = 3, Desktop = false },
        new() { Id = "kali", Name = "Kali Linux", Note = "security tools with a ready desktop, big download (about 25 GB of space)", Kind = "kali", Dir = "https://cdimage.kali.org/current/", Pattern = @"kali-linux-\d{4}\.\d+-qemu-amd64\.7z", NeedGb = 28, Desktop = true },
        new() { Id = "android", Name = "Android (Android-x86 9)", Note = "Android on your PC for apps and games, you click through its installer once, slow without virtualization", Kind = "android", Url = "https://sourceforge.net/projects/android-x86/files/Release%209.0/android-x86_64-9.0-r2.iso/download", NeedGb = 10, Desktop = false },
    };
    static string pendingDistro = "debian";

    static void EnsureSpace(int gb)
    {
        try { var d = new DriveInfo(Path.GetPathRoot(Paths.Root)!); if (d.AvailableFreeSpace < (long)gb * 1073741824) throw new Exception($"Not enough space on the USB. This Linux needs about {gb} GB free, and only {d.AvailableFreeSpace / 1073741824} GB is free."); }
        catch (IOException) { }
    }

    static async Task GetDistro(DlCard card, Distro d, int gb, CancellationToken ct)
    {
        var url = d.Url != "" ? d.Url : await Latest(d.Dir, d.Pattern); var target = DiskFor(d.Id);
        if (d.Kind == "android")
        {
            var iso = Path.Combine(Paths.Vm, "android-x86.iso");
            if (!File.Exists(iso) || new FileInfo(iso).Length < 500_000_000)
            {
                var ip = iso + ".part";
                await Net.Download(url, ip, (g, t, sp) => card.Stage($"Step 4/5 · Downloading Android  {Ui.Bytes(g)}" + (t > 0 ? " of " + Ui.Bytes(t) : "") + $"  ·  {Ui.Bytes((long)sp)}/s", t > 0 ? 28 + 55.0 * g / t : -1), ct);
                if (new FileInfo(ip).Length < 500_000_000) { try { File.Delete(ip); } catch { } throw new Exception("The Android download didn't come through (wrong file). Try again later."); }
                File.Move(ip, iso, true);
            }
            card.Stage("Step 4/5 · Preparing the Android disk…", 90);
            var qi = Exe("qemu-img.exe") ?? throw new Exception("qemu-img is missing. Reinstall QEMU."); if (File.Exists(target)) File.Delete(target);
            using var cp = Process.Start(new ProcessStartInfo(qi) { ArgumentList = { "create", "-f", "qcow2", target, gb + "G" }, CreateNoWindow = true, UseShellExecute = false })!; await cp.WaitForExitAsync();
            return;
        }
        if (d.Kind == "kali")
        {
            var arc = Path.Combine(Paths.Vm, "kali.7z"); 
            await Net.Download(url, arc, (g, t, s) => card.Stage($"Step 4/5 · Downloading Kali  {Ui.Bytes(g)}" + (t > 0 ? " of " + Ui.Bytes(t) : "") + $"  ·  {Ui.Bytes((long)s)}/s", t > 0 ? 28 + 40.0 * g / t : -1), ct);
            var z = Path.Combine(Paths.Vm, "7zr.exe"); if (!File.Exists(z)) await Net.Download("https://www.7-zip.org/a/7zr.exe", z, (g, t, s) => { }, ct);
            var tmp = Path.Combine(Paths.Vm, "kali-unpack"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var psi = new ProcessStartInfo(z) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                foreach (var x in attempt == 0 ? new[] { "x", arc, "-o" + tmp, "-y", "-bsp1" } : new[] { "x", arc, "-o" + tmp, "-y" }) psi.ArgumentList.Add(x);
                using var pr = Process.Start(psi)!; var buf = new char[256]; int last = 0;
                while (true) { int n = await pr.StandardOutput.ReadAsync(buf, 0, buf.Length); if (n <= 0) break; var m = Regex.Matches(new string(buf, 0, n), @"(\d+)%"); if (m.Count > 0) last = int.Parse(m[^1].Groups[1].Value); card.Stage(attempt == 0 ? $"Step 4/5 · Unpacking Kali… {last}%  (a few minutes)" : "Step 4/5 · Unpacking Kali… (a few minutes)", attempt == 0 ? 68 + 20.0 * last / 100 : -1); ct.ThrowIfCancellationRequested(); }
                await pr.WaitForExitAsync(); if (pr.ExitCode == 0) break; if (attempt == 1) throw new Exception("Couldn't unpack Kali. The download may be damaged. Try again.");
            }
            var q = Directory.GetFiles(tmp, "*.qcow2", SearchOption.AllDirectories).FirstOrDefault() ?? throw new Exception("Kali unpacked but no disk file was found inside.");
            if (File.Exists(target)) File.Delete(target); File.Move(q, target); try { Directory.Delete(tmp, true); File.Delete(arc); } catch { }
            return;
        }
        var part = target + ".part";
        await Net.Download(url, part, (g, t, s) => card.Stage($"Step 4/5 · Downloading {d.Name}  {Ui.Bytes(g)}" + (t > 0 ? " of " + Ui.Bytes(t) : "") + $"  ·  {Ui.Bytes((long)s)}/s", t > 0 ? 28 + 58.0 * g / t : -1), ct);
        card.Stage("Step 4/5 · Preparing your disk…", 88);
        if (File.Exists(target)) File.Delete(target); File.Move(part, target); try { File.Delete(FlagFile); } catch { }
        var img = Exe("qemu-img.exe") ?? throw new Exception("qemu-img is missing. Reinstall QEMU.");
        using var rp = Process.Start(new ProcessStartInfo(img) { ArgumentList = { "resize", target, gb + "G" }, CreateNoWindow = true, UseShellExecute = false })!; await rp.WaitForExitAsync();
    }

    static string? usbQemuDir; static bool usbScanned;
    public static void Rescan() { usbQemuDir = null; usbScanned = false; }
    // Finds a file anywhere on the USB (a few folders deep), skipping huge or irrelevant folders.
    static string? FindOnUsb(string file, int depth)
    {
        var skip = new[] { "webview", "$RECYCLE.BIN", "System Volume Information", "Downloads", "runtime" };
        string? Walk(string dir, int d)
        {
            try
            {
                var f = Path.Combine(dir, file); if (File.Exists(f)) return f; if (d <= 0) return null;
                foreach (var sub in Directory.EnumerateDirectories(dir)) { if (skip.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase)) continue; var r = Walk(sub, d - 1); if (r != null) return r; }
            }
            catch { }
            return null;
        }
        var root = Path.GetPathRoot(Paths.Root) ?? Paths.Root; var hit = Walk(Paths.Root, depth); if (hit == null && root != Paths.Root) hit = Walk(root, depth); return hit;
    }
    static string? Exe(string n)
    {
        foreach (var d in new[] { QemuDir, usbQemuDir ?? "", @"C:\Program Files\qemu", @"C:\Program Files (x86)\qemu" }) { if (d == "") continue; var p = Path.Combine(d, n); if (File.Exists(p)) return p; }
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) { try { if (d.Trim() != "") { var p = Path.Combine(d.Trim(), n); if (File.Exists(p)) return p; } } catch { } }
        if (!usbScanned) { usbScanned = true; var hit = FindOnUsb("qemu-system-x86_64.exe", 5); if (hit != null) { usbQemuDir = Path.GetDirectoryName(hit); return Path.Combine(usbQemuDir!, n) is var q && File.Exists(q) ? q : hit; } }
        else if (usbQemuDir != null) { var p = Path.Combine(usbQemuDir, n); if (File.Exists(p)) return p; }
        return null;
    }
    static string? Iso() { try { var ai = Path.Combine(Paths.Vm, "android-x86.iso"); if (CurId == "android" && File.Exists(ai)) return ai; return Directory.GetFiles(Paths.Vm, "*.iso").FirstOrDefault() ?? Directory.GetFiles(Paths.Root, "*.iso").FirstOrDefault(); } catch { return null; } }

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
        RoundButton start = null!, stop = null!; ComboBox boot = null!; NumericUpDown ram = null!; Label s1 = null!, s2 = null!, s3 = null!;
        RoundButton B(Control parent, string t, Func<Task> a, bool primary = true) { RoundButton? btn = null; btn = pg.Btn(parent, t, async () => { try { await a(); } catch (Exception ex) { st.Text = ex.Message; } Refresh(); }, primary); return btn; }

        pg.H("Full OS (Linux)"); pg.P("Runs a real Linux computer inside a window. Everything is saved on the USB.", true);
        pg.P("Easy setup", false, true, 13, 14); pg.P("One button. It looks for what's already on this USB, downloads and installs whatever is missing, asks you only a few simple questions, and starts Linux ready to log in.", true);
        var rA = pg.Row(6); B(rA, "Set up Full OS automatically", () => Auto());
        pg.P("Manual setup (optional)", false, true, 12, 20);
        pg.P("1  QEMU engine", false, true, 11, 12); s1 = pg.P("", true); var r1 = pg.Row(2);
        B(r1, "Install QEMU", async () =>
        {
            if (Exe("qemu-system-x86_64.exe") != null) { st.Text = "QEMU is already on your USB. Nothing to install."; return; }
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
        pg.Inline(r4, "RAM (MB)"); ram = pg.Num(r4, 512, 32768, Cfg.Int("vm_ram", 2048), 256);
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
            var ifd = CurId == "android" ? "ide" : "virtio";
            if (CurId == "android") foreach (var x in new[] { "-cpu", "max", "-device", "intel-hda", "-device", "hda-duplex" }) psi.ArgumentList.Add(x);
            foreach (var a in new[] { "-m", ram.Value.ToString(), "-smp", cpu.Value.ToString(), "-accel", "whpx,kernel-irqchip=off", "-accel", "tcg", "-drive", $"file={Disk},format=qcow2,if={ifd}", "-nic", "user,model=virtio-net-pci", "-device", "qemu-xhci", "-device", "usb-tablet", "-vga", "std", "-name", "USB Desktop Linux" }) psi.ArgumentList.Add(a);
            if (boot.SelectedIndex == 1 && Iso() != null) { psi.ArgumentList.Add("-cdrom"); psi.ArgumentList.Add(Iso()!); psi.ArgumentList.Add("-boot"); psi.ArgumentList.Add("d"); }
            if (pendingPw != null) { StartSeed(pendingPw, pendingDesk, pendingDistro); pendingPw = null; psi.ArgumentList.Add("-smbios"); psi.ArgumentList.Add("type=1,serial=ds=nocloud-net;s=http://10.0.2.2:" + SeedPort + "/"); }
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
            s1.Text = q ? "Installed" : "Not installed yet"; s2.Text = i ? Path.GetFileName(Iso()!) : "No installer downloaded yet"; s3.Text = d ? $"{Path.GetFileName(Disk)} ({new FileInfo(Disk).Length / 1048576} MB used)" : "No disk yet";
            s1.ForeColor = q ? Color.FromArgb(110, 231, 160) : Ui.Sub; s2.ForeColor = i ? Color.FromArgb(110, 231, 160) : Ui.Sub; s3.ForeColor = d ? Color.FromArgb(110, 231, 160) : Ui.Sub;
            start.Enabled = q && d && !on && (boot.SelectedIndex == 0 || i); stop.Enabled = on; if (on) st.Text = "Linux is running";
        }
        async Task Auto()
        {
            var cts = new CancellationTokenSource(); var card = sh.NewDl("Setting up Full OS", cts, "");
            try
            {
                card.Stage("Step 1/5 · Looking for what's already on your USB…", 2); Rescan();
                var foundQ = await Task.Run(() => Exe("qemu-system-x86_64.exe")); var foundIso = Iso();
                var have = Distros.Where(x => File.Exists(DiskFor(x.Id))).Select(x => x.Name).ToList();
                card.Stage("Step 1/5 · Found on your USB:  QEMU " + (foundQ != null ? "yes" : "no") + "  ·  Linux: " + (have.Count > 0 ? string.Join(", ", have) : "none yet") + (foundIso != null ? "  ·  installer ISO yes" : ""), 6); await Task.Delay(1800);
                card.Stage("Step 2/5 · A few quick questions…", 8);
                int di = Ask.Choose("Choose your Linux", string.Join("\n", Distros.Select(x => x.Name + ": " + x.Note + ".")) + "\n\nOnes already on your USB are marked.", Distros.Select(x => x.Name + (File.Exists(DiskFor(x.Id)) ? "   (on your USB)" : "")).ToArray());
                if (di < 0) { card.Fail("Cancelled"); return; }
                var dist = Distros[di]; Cfg.Set("vm_distro", dist.Id); pendingDistro = dist.Id;
                bool hadDisk = File.Exists(DiskFor(dist.Id)); bool fresh = !hadDisk; bool desk = dist.Desktop && dist.Kind != "kali"; int gb = 16; string pw = "";
                if (hadDisk) { int c = Ask.Choose(dist.Name + " is already on this USB", "Keep using it, or start over with a fresh one?", "Keep using it", "Replace with a fresh one"); if (c < 0) { card.Fail("Cancelled"); return; } fresh = c == 1; }
                if (fresh && dist.Kind == "cloud")
                {
                    if (dist.Desktop) { int style = Ask.Choose("What kind of " + dist.Name + "?", "Desktop looks like a normal PC with windows, icons and a web browser. Terminal is smaller and starts faster.", "Desktop (XFCE)", "Terminal only"); if (style < 0) { card.Fail("Cancelled"); return; } desk = style == 0; }
                    int size = Ask.Choose("How much space?", "This is just a file on your USB and it only grows as you use it.", "8 GB", "16 GB", "32 GB"); if (size < 0) { card.Fail("Cancelled"); return; } gb = new[] { 8, 16, 32 }[size];
                    pw = Ask.Password("Choose a Linux password", "You'll log in as  user  with this password.") ?? ""; if (pw == "") { card.Fail("Cancelled"); return; }
                }
                if (fresh && dist.Kind == "android")
                {
                    int size = Ask.Choose("How much space for Android?", "Games are big. This is one file on your USB and it only grows as you use it.", "16 GB", "32 GB", "64 GB"); if (size < 0) { card.Fail("Cancelled"); return; } gb = new[] { 16, 32, 64 }[size];
                }
                if (fresh) EnsureSpace(dist.NeedGb + (desk ? 3 : 0));
                await EnsureQemu(card, 3, cts.Token);
                if (fresh)
                {
                    await GetDistro(card, dist, gb, cts.Token);
                    if (dist.Kind == "cloud") { pendingPw = pw; pendingDesk = desk; }
                    if ((dist.Kind == "kali" || dist.Kind == "android") && ram.Value < 4096) ram.Value = 4096;
                }
                card.Stage("Step 5/5 · Starting " + dist.Name + "…", 96); int bi = dist.Kind == "android" && fresh ? 1 : 0; Cfg.Set("vm_boot", bi.ToString()); boot.SelectedIndex = bi; Refresh(); await Task.Delay(500);
                if (!start.Enabled) throw new Exception("Everything is ready but Linux couldn't start. Press Start Full OS below.");
                start.PerformClick();
                card.Done(fresh && dist.Kind == "android" ? "Android installer is starting. Pick Installation, make one bootable partition (no GPT), format ext4, install GRUB, then Reboot. After that close the window, set Boot from to Disk and press Start." : fresh ? (dist.Kind == "kali" ? "Kali is starting. Log in with  kali  /  kali." : dist.Name + " is starting. The first start takes several minutes while it sets itself up. Log in as  user.") : dist.Name + " is starting.");
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
