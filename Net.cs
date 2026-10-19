using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

static class Net
{
    static readonly HttpClient http = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(15), AllowAutoRedirect = true }) { Timeout = Timeout.InfiniteTimeSpan };
    public static HttpClient Http => http;
    static Net() { http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0) UsbDesktop/2"); }

    public static async Task<string> FirstModel(string baseUrl, string key)
    {
        var url = baseUrl.Trim().TrimEnd('/'); if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        using var req = new HttpRequestMessage(HttpMethod.Get, url + "/v1/models"); req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var resp = await http.SendAsync(req, cts.Token); var body = await resp.Content.ReadAsStringAsync(cts.Token);
        if (!resp.IsSuccessStatusCode) throw new Exception(resp.StatusCode == HttpStatusCode.Unauthorized ? "Invalid RelayAPI key" : "RelayAPI said " + (int)resp.StatusCode);
        var d = JsonDocument.Parse(body).RootElement.GetProperty("data"); return d.GetArrayLength() > 0 ? d[0].GetProperty("id").GetString() ?? "" : "";
    }

    // OpenAI-style chat call (used for RelayAPI)
    public static async Task<string> Chat(string baseUrl, string key, string model, object[] messages, CancellationToken ct)
    {
        var url = baseUrl.Trim().TrimEnd('/'); if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        using var req = new HttpRequestMessage(HttpMethod.Post, url + "/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = new StringContent(JsonSerializer.Serialize(new { model, messages }), Encoding.UTF8, "application/json");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(150));
        using var resp = await http.SendAsync(req, cts.Token);
        var body = await resp.Content.ReadAsStringAsync(cts.Token);
        if (!resp.IsSuccessStatusCode)
        {
            string m = body.Length > 200 ? body[..200] : body;
            try { m = JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("message").GetString() ?? m; } catch { }
            throw new Exception(m);
        }
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    public static async Task<string> Text(string url)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        return await http.GetStringAsync(url, cts.Token);
    }

    // Resumable download with stall detection, retries and live progress (got, total, bytes/sec; speed -1 = reconnecting)
    public static async Task Download(string url, string dest, Action<long, long, double> progress, CancellationToken ct, int retries = 6)
    {
        var part = dest + ".part";
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                long have = File.Exists(part) ? new FileInfo(part).Length : 0;
                using var req = new HttpRequestMessage(HttpMethod.Get, url); if (have > 0) req.Headers.Range = new RangeHeaderValue(have, null);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(25));
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable) { File.Move(part, dest, true); progress(have, have, 0); return; }
                resp.EnsureSuccessStatusCode();
                bool partial = resp.StatusCode == HttpStatusCode.PartialContent; if (!partial) have = 0;
                long total = resp.Content.Headers.ContentLength ?? -1; if (total >= 0) total += have;
                long got = have;
                using (var s = await resp.Content.ReadAsStreamAsync(cts.Token))
                using (var f = new FileStream(part, partial ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, true))
                {
                    var buf = new byte[1 << 16]; var sw = Stopwatch.StartNew(); long lastBytes = got, lastTick = 0; progress(got, total, 0);
                    while (true)
                    {
                        cts.CancelAfter(TimeSpan.FromSeconds(25));
                        int n = await s.ReadAsync(buf, cts.Token); if (n == 0) break;
                        await f.WriteAsync(buf.AsMemory(0, n), ct); got += n;
                        if (sw.ElapsedMilliseconds - lastTick >= 250)
                        {
                            double secs = (sw.ElapsedMilliseconds - lastTick) / 1000.0; progress(got, total, (got - lastBytes) / secs);
                            lastBytes = got; lastTick = sw.ElapsedMilliseconds;
                        }
                    }
                }
                File.Move(part, dest, true); progress(got, got, 0); return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { try { File.Delete(part); } catch { } throw; }
            catch (Exception) when (attempt < retries) { progress(-1, -1, -1); await Task.Delay(1500 * (attempt + 1), ct); }
        }
    }
}
