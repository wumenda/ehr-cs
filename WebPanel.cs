using System.Net;
using System.Text;
using System.Text.Json;

namespace EhrOvertimeTray;

/// <summary>本地 Web 面板（仅监听 localhost），展示本月加班汇总、明细与 30 天曲线</summary>
public sealed class WebPanel
{
    private readonly TrayApp _app;
    private readonly int _port;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();

    public string Url { get; }

    public WebPanel(TrayApp app, int port)
    {
        _app = app;
        _port = port;
        Url = $"http://localhost:{port}/";
        _listener.Prefixes.Add(Url);
    }

    /// <summary>启动监听；失败（如端口被占用）返回 false，调用方降级为仅托盘模式</summary>
    public bool Start()
    {
        try
        {
            _listener.Start();
            _ = Task.Run(ServeLoopAsync);
            Log.Info("Web 面板已启动: {0}", Url);
            return true;
        }
        catch (Exception e)
        {
            Log.Error("Web 面板启动失败（端口 {0} 可能被占用），已降级为仅托盘模式: {1}", _port, e.Message);
            return false;
        }
    }

    public void Stop()
    {
        try { _cts.Cancel(); _listener.Stop(); } catch { }
    }

    private async Task ServeLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch when (_cts.IsCancellationRequested || !_listener.IsListening)
            {
                break; // Stop() 引发的正常退出
            }
            catch (Exception e)
            {
                Log.Warn("Web 监听异常退出: {0}", e.Message);
                break;
            }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            // 防 DNS rebinding / 非本机域名访问：Host 必须是 localhost 或 127.0.0.1
            //（http.sys 前缀匹配之外的第二道防线）
            var host = req.Url?.Host ?? "";
            if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                && !host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                resp.Abort();
                return;
            }

            // 防 CSRF：写操作要求显式 application/json Content-Type，
            // 阻止恶意网页用 fetch text/plain"简单请求"绕过预检直接改配置
            if (req.HttpMethod == "POST"
                && !(req.Headers["Content-Type"] ?? "")
                    .StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                await SendTextAsync(resp, 415, "text/plain; charset=utf-8", "unsupported media type");
                return;
            }

            switch (req.HttpMethod)
            {
                case "GET" when req.Url!.AbsolutePath is "/" or "/index.html":
                    resp.Headers["Content-Security-Policy"] =
                        "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; "
                        + "connect-src 'self'; form-action 'none'; base-uri 'none'";
                    await SendTextAsync(resp, 200, "text/html; charset=utf-8", HtmlTemplate.Html);
                    break;
                case "GET" when req.Url!.AbsolutePath == "/api/state":
                    await SendJsonAsync(resp, BuildState());
                    break;
                case "POST" when req.Url!.AbsolutePath == "/api/refresh":
                    _ = Task.Run(_app.RefreshAsync);
                    await SendJsonAsync(resp, """{"ok":true}""");
                    break;
                case "POST" when req.Url!.AbsolutePath == "/api/config":
                    await SaveConfigAsync(req, resp);
                    break;
                default:
                    await SendTextAsync(resp, 404, "text/plain; charset=utf-8", "not found");
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Warn("Web 请求处理异常: {0}", e.Message);
        }
    }

    private static async Task SendTextAsync(HttpListenerResponse resp, int code, string ctype, string body)
    {
        resp.StatusCode = code;
        resp.ContentType = ctype;
        resp.Headers["Cache-Control"] = "no-store";
        resp.Headers["X-Content-Type-Options"] = "nosniff";
        var bytes = Encoding.UTF8.GetBytes(body);
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
        resp.Close();
    }

    private static Task SendJsonAsync(HttpListenerResponse resp, string json)
        => SendTextAsync(resp, 200, "application/json; charset=utf-8", json);

    private static async Task<string> ReadBodyAsync(HttpListenerRequest req)
    {
        using var sr = new StreamReader(req.InputStream, System.Text.Encoding.UTF8);
        return await sr.ReadToEndAsync();
    }

    private async Task SaveConfigAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        try
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await ReadBodyAsync(req));
            var hasCred = body.TryGetProperty("username", out var u) && u.ValueKind == JsonValueKind.String;
            var hasPwd = body.TryGetProperty("password", out var p) && p.ValueKind == JsonValueKind.String;
            if (hasCred || hasPwd)
            {
                // 仅传用户名（未传密码）时保留原密码；仅传密码时沿用原用户名
                var username = hasCred ? (u.GetString()?.Trim() ?? "") : _app.UsernameForPanel ?? "";
                var password = hasPwd ? (p.GetString() ?? "") : "";
                if (string.IsNullOrEmpty(username))
                    throw new Exception("用户名不能为空");
                if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(_app.UsernameForPanel))
                    throw new Exception("首次设置必须填写密码");
                _app.SaveAccount(username, password);
            }
            if (body.TryGetProperty("notify_daily_hours", out var th) && th.ValueKind == JsonValueKind.Number)
                _app.SetNotifyDailyHours(th.GetDouble());
            await SendJsonAsync(resp, """{"ok":true}""");
        }
        catch (Exception e)
        {
            Log.Error("面板保存设置失败: {0}", e.Message);
            await SendJsonAsync(resp, JsonSerializer.Serialize(new { ok = false, error = e.Message }));
        }
    }

    private string BuildState()
    {
        var s = _app.State;
        return JsonSerializer.Serialize(new
        {
            user = _app.UsernameForPanel,
            notify_daily_hours = _app.NotifyDailyHours,
            error = s.Error,
            updated = s.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            begin = s.BeginDate,
            end = s.EndDate,
            total = Math.Round(s.TotalHours, 2),
            workday = Math.Round(s.WorkDayHours, 2),
            holiday = Math.Round(s.HolidayHours, 2),
            times = s.WorkDayTimes,
            eat_times = s.EatTimes,
            meal = Math.Round(s.MealAllowance, 2),
            details = s.Details.OrderByDescending(d => d.Date).Select(d => new object[]
            {
                d.Date, d.Type, Math.Round(d.Hours, 2), d.FirstKq, d.LastKq, d.Meal,
            }).ToArray(),
            history = _app.History30.Select(p => new object[] { p.Date, Math.Round(p.Hours, 2) }).ToArray(),
        });
    }
}
