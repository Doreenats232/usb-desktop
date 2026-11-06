using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Text.Json;
namespace UsbDesktop;

// Real-time link to the phone through the relay's WebSocket: one open connection, no request per message.
// Phone -> PC: mouse/keyboard events and "start/stop the live screen". PC -> phone: live screen frames (small JPEGs, only when something changed).
static class Realtime
{
    static CancellationTokenSource? liveCts;

    public static async Task Loop(DesktopForm sh, string url, string code, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ub = new UriBuilder(url) { Scheme = url.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws", Path = "/ws", Port = -1 };
                using var sock = new ClientWebSocket();
                sock.Options.AddSubProtocol("pc." + code); sock.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                await sock.ConnectAsync(ub.Uri, ct);
                await Receive(sock, sh, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch { }
            StopLive();
            try { await Task.Delay(3000, ct); } catch { break; }
        }
        StopLive();
    }

    static async Task Receive(ClientWebSocket sock, DesktopForm sh, CancellationToken ct)
    {
        var buf = new byte[16384]; var ms = new MemoryStream();
        while (sock.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            ms.SetLength(0); WebSocketReceiveResult r;
            do
            {
                r = await sock.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (r.MessageType == WebSocketMessageType.Close) return;
                if (r.MessageType == WebSocketMessageType.Text && ms.Length < 65536) ms.Write(buf, 0, r.Count);
            } while (!r.EndOfMessage);
            if (ms.Length == 0) continue;
            try
            {
                using var doc = JsonDocument.Parse(ms.ToArray()); var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array) RemoteInput.Batch(sh, root);
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("t", out var t) && t.GetString() == "live")
                {
                    bool on = root.TryGetProperty("on", out var o) && o.ValueKind == JsonValueKind.True;
                    int Get(string k, int d) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : d;
                    StopLive();
                    if (on) { liveCts = new CancellationTokenSource(); var lct = liveCts.Token; int w = Math.Clamp(Get("w", 900), 320, 1600), q = Math.Clamp(Get("q", 55), 25, 85), fps = Math.Clamp(Get("fps", 8), 1, 15); _ = Task.Run(() => Stream(sock, w, q, fps, lct)); }
                }
            }
            catch { }
        }
    }

    static void StopLive() { try { liveCts?.Cancel(); } catch { } liveCts = null; }

    // grey 24x14 sample of a frame, used to tell whether anything changed
    static byte[] Thumb(Bitmap bm)
    {
        var t = new byte[24 * 14];
        for (int y = 0; y < 14; y++) for (int x = 0; x < 24; x++) { var c = bm.GetPixel(Math.Min(bm.Width - 1, (x * bm.Width + bm.Width / 2) / 24), Math.Min(bm.Height - 1, (y * bm.Height + bm.Height / 2) / 14)); t[y * 24 + x] = (byte)((c.R + c.G + c.B) / 3); }
        return t;
    }
    static bool Same(byte[] a, byte[] b) { int d = 0; for (int i = 0; i < a.Length; i++) d += Math.Abs(a[i] - b[i]); return d < 24; }

    static async Task Stream(ClientWebSocket sock, int w, int q, int fps, CancellationToken ct)
    {
        var enc = ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");
        var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)q);
        int interval = 1000 / fps; byte[]? last = null; Point lastCur = new(-9999, 0); long lastSent = 0;
        while (!ct.IsCancellationRequested && sock.State == WebSocketState.Open)
        {
            long t0 = Environment.TickCount64;
            try
            {
                var b = Screen.PrimaryScreen!.Bounds;
                using var full = new Bitmap(b.Width, b.Height, PixelFormat.Format24bppRgb);
                var cur = Cursor.Position;
                using (var g = Graphics.FromImage(full))
                {
                    g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                    try { Cursors.Default.Draw(g, new Rectangle(cur.X - b.X, cur.Y - b.Y, 32, 32)); } catch { }   // show the mouse pointer too
                }
                int ow = Math.Min(w, b.Width), oh = Math.Max(1, (int)((long)b.Height * ow / b.Width));
                using var small = new Bitmap(ow, oh, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(full, 0, 0, ow, oh); }
                var th = Thumb(small); bool moved = Math.Abs(cur.X - lastCur.X) + Math.Abs(cur.Y - lastCur.Y) > 2;
                if (last != null && !moved && Same(last, th) && Environment.TickCount64 - lastSent < 2000) { await Task.Delay(interval, ct); continue; }   // nothing changed: send nothing (a frame every 2 s keeps it alive)
                last = th; lastCur = cur;
                using var ms = new MemoryStream(); small.Save(ms, enc, ep);
                await sock.SendAsync(new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length), WebSocketMessageType.Binary, true, ct);
                lastSent = Environment.TickCount64;
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException) { break; }
            catch { }   // e.g. the lock screen: try again
            int wait = interval - (int)(Environment.TickCount64 - t0);
            if (wait > 0) { try { await Task.Delay(wait, ct); } catch { break; } }
        }
    }
}
