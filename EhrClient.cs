using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EhrOvertimeTray;

/// <summary>EHR 客户端业务异常（登录失败/数据格式异常等，消息面向用户可读）</summary>
public sealed class EhrError : Exception
{
    public EhrError(string message) : base(message) { }
}

/// <summary>
/// EHR 系统客户端 - CAS 登录与考勤数据拉取。
/// 从 ehr-mcp(ehr_client.py) 精确移植：固定 UA、GET 登录页取 lt、密码 Base64、
/// 手动处理 302 重定向以兼容 CAS 票据流程、staffId 多模式正则提取。
/// </summary>
public sealed class EhrClient : IDisposable
{
    // 固定 UA（与原版完全一致）
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 6.1; WOW64) "
        + "AppleWebKit/537.36 (KHTML, like Gecko) "
        + "Chrome/63.0.3239.132 Safari/537.36";

    // 服务端点
    private const string CasLoginUrl =
        "https://portal.supcon.com/cas-web/login"
        + "?service=https%3A%2F%2Fehr.supcon.com%2FRedseaPlatform%2F";
    private const string PortalClassicUrl =
        "https://ehr.supcon.com/RedseaPlatform/PtPortal.mc?method=classic";
    private const string EhrRecordsUrl =
        "https://ehr.supcon.com/RedseaPlatform/getList/kq_data_queryByStaffId/CoreRequest.mc";
    private const string WorkdayInfosUrl =
        "https://ehr.supcon.com/RedseaPlatform/getList/kq_count_abnormal_SelectStaffID/CoreRequest.mc";

    private static readonly string[] LoginFailKeywords = { "密码错误", "用户名错误", "账号或密码错误", "登录失败" };

    private static readonly Regex LtRe = new(@"<input[^>]*name=""lt""[^>]*value=""(.*?)""",
        RegexOptions.IgnoreCase);

    private static readonly Regex[] StaffIdPatterns =
    {
        new("staffId:\\s*'(.*?)'"),
        new("staffId:\\s*\"(.*?)\""),
        new("staffId:\\s*([A-Za-z0-9_-]+)"),
    };

    private readonly string _username;
    private readonly string _password;
    private readonly HttpClient _client;
    private string? _portalHtml; // 门户首页 HTML 缓存（登录后填充）

    public EhrClient(string username, string password)
    {
        _username = username;
        _password = password;
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,      // 手动处理 302，与 httpx follow_redirects=False 一致
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public void Dispose() => _client.Dispose();

    // ---------- CAS 登录 ----------
    public async Task LoginAsync()
    {
        // 步骤 1: 获取登录令牌 lt
        using var resp = await _client.GetAsync(CasLoginUrl);
        var lt = LtRe.Match(await resp.Content.ReadAsStringAsync()).Groups[1].Value;
        if (string.IsNullOrEmpty(lt))
            throw new EhrError("无法获取登录令牌(lt)，请检查网络连接");

        // 步骤 2: 密码 Base64 编码（与原版 base64(password) 一致）
        var b64Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(_password));

        // 步骤 3: 提交登录表单
        using var loginResp = await _client.PostAsync(CasLoginUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["portal_username"] = _username,
            ["password"] = b64Password,
            ["bakecookie"] = "on",
            ["lt"] = lt,
            ["_eventId"] = "submit",
            ["username"] = _username,
        }));

        // 步骤 4: 处理登录响应（3xx 视为可能成功）
        var status = (int)loginResp.StatusCode;
        if (status is 301 or 302 or 303 or 307 or 308)
        {
            var location = loginResp.Headers.Location?.OriginalString ?? "";
            if (location.Contains("ticket="))
            {
                // 跟随重定向完成 CAS 票据验证（含后续 302，建立完整会话）
                // 落地页可能是 JS 跳转存根（无 staffId），此时不缓存，由 GetStaffIdAsync 显式请求门户
                var finalResp = await FollowRedirectsAsync(location);
                if (finalResp is not null && finalResp.Contains("staffId:"))
                    _portalHtml = finalResp;
                return;
            }
            // 重定向但无 ticket，继续走兜底验证
        }
        else
        {
            // 检查响应正文判断失败原因
            var text = await loginResp.Content.ReadAsStringAsync();
            if (LoginFailKeywords.Any(text.Contains))
                throw new EhrError("用户名或密码错误");
            if (text.Contains("验证码"))
                throw new EhrError("需要验证码，请稍后重试");
        }

        // 步骤 5: 兜底验证登录状态
        await VerifyLoginAsync();
    }

    /// <summary>手动跟随 302 链（最多 6 跳），返回最终响应的正文；非 3xx 直接返回正文</summary>
    private async Task<string?> FollowRedirectsAsync(string url)
    {
        for (var i = 0; i < 6; i++)
        {
            using var resp = await _client.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            var sc = (int)resp.StatusCode;
            if (sc is not (301 or 302 or 303 or 307 or 308)) return body;
            var loc = resp.Headers.Location;
            if (loc is null) return body;
            url = loc.IsAbsoluteUri ? loc.AbsoluteUri : new Uri(new Uri(url), loc).AbsoluteUri;
        }
        return null;
    }

    private async Task<string> GetPortalHtmlAsync()
    {
        if (_portalHtml is null)
        {
            _portalHtml = await FollowRedirectsAsync(PortalClassicUrl) ?? "";
        }
        return _portalHtml;
    }

    private async Task VerifyLoginAsync()
    {
        if ((await GetPortalHtmlAsync()).Contains("staffId:")) return;
        throw new EhrError("登录失败，请检查用户名密码是否正确");
    }

    // ---------- 员工 ID ----------
    public async Task<string> GetStaffIdAsync()
    {
        var portalHtml = await GetPortalHtmlAsync();
        foreach (var pattern in StaffIdPatterns)
        {
            var m = pattern.Match(portalHtml);
            if (m.Success) return m.Groups[1].Value;
        }
        // 匹配失败：保存页面快照便于诊断真实格式
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(Path.Combine(AppPaths.DataDir, "ehr_debug_page.html"), portalHtml);
        }
        catch { }
        throw new EhrError(
            "无法获取员工ID：EHR 页面结构可能已变更。已保存页面快照到 %LOCALAPPDATA%\\EhrOvertimeTray\\ehr_debug_page.html，请检查其中的 staffId 格式");
    }

    // ---------- 考勤数据 ----------
    public async Task<JsonElement> GetEhrRecordsAsync(string staffId, string beginDate, string endDate)
    {
        using var resp = await _client.PostAsync(EhrRecordsUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["staff_id"] = staffId,
            ["bc_date"] = beginDate,
            ["bc_date_end"] = endDate,
        }));
        return DeserializeObject(await resp.Content.ReadAsStringAsync(), "考勤记录");
    }

    public async Task<JsonElement> GetWorkdayInfosAsync(string staffId, string beginDate, string endDate)
    {
        using var resp = await _client.PostAsync(WorkdayInfosUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["staff_id"] = staffId,
            ["begin"] = beginDate,
            ["end"] = endDate,
        }));
        return DeserializeObject(await resp.Content.ReadAsStringAsync(), "工作日信息");
    }

    private static JsonElement DeserializeObject(string text, string label)
    {
        try
        {
            var el = JsonDocument.Parse(text).RootElement.Clone();
            if (el.ValueKind != JsonValueKind.Object)
                throw new EhrError($"{label}响应格式异常");
            return el;
        }
        catch (JsonException e)
        {
            throw new EhrError($"{label}响应解析失败: {e.Message}");
        }
    }
}