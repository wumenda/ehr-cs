using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EhrOvertimeTray;

public static class AppPaths
{
    public static readonly string AppDir =
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>可写数据目录（%LOCALAPPDATA%\EhrOvertimeTray）：日志/历史等运行数据存放于此。
    /// 程序目录可能不可写（如 Program Files）或被同步盘打包带走，用户数据不应落在那里。</summary>
    public static readonly string DataDir = CreateDataDir();

    public static readonly string ConfigPath = Path.Combine(AppDir, "config.json");
    public static readonly string LogPath = Path.Combine(DataDir, "tray.log");
    public static readonly string HistoryPath = Path.Combine(DataDir, "history.csv");

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string CreateDataDir()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EhrOvertimeTray");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            return AppDir; // LocalAppData 不可用时回退到程序目录
        }
    }

    /// <summary>旧版本把日志/历史放在程序目录，首次启动时一次性迁移到 DataDir</summary>
    public static void MigrateLegacyData()
    {
        foreach (var name in new[] { "history.csv", "tray.log", "tray.log.1", "tray.log.2", "tray.1.log", "tray.2.log" })
        {
            try
            {
                var src = Path.Combine(AppDir, name);
                var dst = Path.Combine(DataDir, name);
                if (File.Exists(src) && !File.Exists(dst))
                    File.Move(src, dst);
            }
            catch { }
        }
    }
}

// ---------------- 凭证加密（DPAPI，当前用户范围，无需外部依赖） ----------------
public static class SecureText
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int CbData;
        public IntPtr PbData;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private const int CrptProtectUiForbidden = 0x1;

    private static byte[]? Protect(byte[] data)
    {
        var input = new DataBlob { CbData = data.Length, PbData = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.PbData, data.Length);
            if (!CryptProtectData(ref input, "EHR password", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CrptProtectUiForbidden, out var output))
                return null;
            try
            {
                var buf = new byte[output.CbData];
                Marshal.Copy(output.PbData, buf, 0, output.CbData);
                return buf;
            }
            finally
            {
                LocalFree(output.PbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.PbData);
        }
    }

    /// <summary>加密明文为 base64；失败返回空字符串</summary>
    public static string Encrypt(string plain)
    {
        try
        {
            var buf = Protect(Encoding.UTF8.GetBytes(plain));
            return buf is null ? "" : Convert.ToBase64String(buf);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>解密 base64；失败返回空字符串</summary>
    public static string Decrypt(string stored)
    {
        try
        {
            var data = Convert.FromBase64String(stored);
            var input = new DataBlob { CbData = data.Length, PbData = Marshal.AllocHGlobal(data.Length) };
            try
            {
                Marshal.Copy(data, 0, input.PbData, data.Length);
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        CrptProtectUiForbidden, out var output))
                    return "";
                try
                {
                    var buf = new byte[output.CbData];
                    Marshal.Copy(output.PbData, buf, 0, output.CbData);
                    return Encoding.UTF8.GetString(buf);
                }
                finally
                {
                    LocalFree(output.PbData);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(input.PbData);
            }
        }
        catch
        {
            return "";
        }
    }
}

// ---------------- 配置 ----------------
public sealed class AppConfig
{
    private static readonly object SaveGate = new();

    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = ""; // DPAPI base64
    [JsonPropertyName("refresh_interval_sec")] public int RefreshIntervalSec { get; set; } = 900;
    [JsonPropertyName("webui_port")] public int WebUiPort { get; set; } = 9877;
    [JsonPropertyName("icon_show_hours")] public bool IconShowHours { get; set; } = true; // 图标内显示小时数字
    [JsonPropertyName("notify_daily_hours")] public double NotifyDailyHours { get; set; } = 2; // 每日加班达此值时通知（0=关闭）

    /// <summary>解密后的真实口令（无则返回空串）</summary>
    [JsonIgnore]
    public string PasswordPlain
    {
        get => string.IsNullOrEmpty(Password) ? "" : SecureText.Decrypt(Password);
    }

    public void SetCredentials(string username, string plainPassword)
    {
        Username = username;
        Password = SecureText.Encrypt(plainPassword);
    }

    /// <summary>加载配置；首次运行或文件损坏时生成默认配置（损坏文件备份为 .bad），绝不崩溃</summary>
    public static AppConfig LoadOrCreate()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(AppPaths.ConfigPath), AppPaths.JsonOpts);
                if (cfg is not null)
                {
                    // 钳制非法配置，防止手改文件导致启动失败或秒级轮询压测服务端
                    cfg.RefreshIntervalSec = Math.Clamp(cfg.RefreshIntervalSec, 60, 86400);
                    cfg.WebUiPort = Math.Clamp(cfg.WebUiPort, 1024, 65535);
                    cfg.NotifyDailyHours = Math.Max(0, cfg.NotifyDailyHours);
                    return cfg;
                }
            }
        }
        catch (Exception e)
        {
            try { File.Copy(AppPaths.ConfigPath, AppPaths.ConfigPath + ".bad", overwrite: true); } catch { }
            Log.Warn("config.json 解析失败（已备份为 config.json.bad），将生成默认配置: {0}", e.Message);
        }

        var def = new AppConfig();
        def.Save();
        Log.Info("已生成默认配置 config.json");
        return def;
    }

    /// <summary>原子写入（临时文件 + 替换）并加锁，避免并发/中断写坏配置导致丢账号</summary>
    public void Save()
    {
        lock (SaveGate)
        {
            var tmp = AppPaths.ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, AppPaths.JsonOpts));
            File.Move(tmp, AppPaths.ConfigPath, overwrite: true);
        }
    }
}

// ---------------- 加班状态与明细 ----------------
public sealed class OvertimeDetail
{
    public string Date { get; set; } = "";
    public string Type { get; set; } = "";      // 工作日加班/节假日加班
    public double Hours { get; set; }
    public string FirstKq { get; set; } = "";
    public string LastKq { get; set; } = "";
    public bool Abnormal { get; set; }
    public bool Meal { get; set; }
}

public sealed class OvertimeState
{
    public string? Error { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string BeginDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public double WorkDayHours { get; set; }
    public double HolidayHours { get; set; }
    public double TotalHours { get; set; }
    public int WorkDayTimes { get; set; }
    public int EatTimes { get; set; }
    public double MealAllowance { get; set; }
    public List<OvertimeDetail> Details { get; set; } = new();
}

// ---------------- 历史快照（每日加班时长，供面板 30 天曲线） ----------------
public sealed class History
{
    private readonly string _path;
    private readonly object _gate = new(); // Upsert/Save（刷新线程）与 Series（Web 线程）并发访问保护
    private readonly Dictionary<(string Date, string User), double> _rows = new();

    public History(string path)
    {
        _path = path;
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            foreach (var line in File.ReadLines(_path))
            {
                var parts = line.Split(',');
                if (parts.Length == 3 && parts[0] != "date"
                    && double.TryParse(parts[2], out var h))
                    _rows[(parts[0], parts[1])] = h;
            }
        }
        catch (Exception e)
        {
            Log.Warn("历史 CSV 读取失败: {0}", e.Message);
        }
    }

    public void Upsert(string date, string user, double hours)
    {
        lock (_gate)
        {
            _rows[(date, user)] = hours;
        }
    }

    public List<(string Date, double Hours)> Series(string user, int days = 30)
    {
        lock (_gate)
        {
            var list = new List<(string, double)>();
            for (var i = days - 1; i >= 0; i--)
            {
                var d = DateOnly.FromDateTime(DateTime.Now).AddDays(-i).ToString("yyyy-MM-dd");
                if (_rows.TryGetValue((d, user), out var h)) list.Add((d, h));
            }
            return list;
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                var tmp = _path + ".tmp";
                using (var w = new StreamWriter(tmp))
                {
                    w.WriteLine("date,user,hours");
                    foreach (var key in _rows.Keys.OrderBy(k => k))
                        w.WriteLine($"{key.Item1},{key.Item2},{_rows[key]}");
                }
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception e)
            {
                Log.Warn("历史 CSV 写入失败: {0}", e.Message);
            }
        }
    }
}

// ---------------- 图标（圆环 + 小时数字） ----------------
/// <summary>托盘图标句柄封装：Icon 由 GetHicon 句柄构建，Dispose 时调用 DestroyIcon 归还系统句柄，
/// 防止长期运行 GDI HICON 泄漏（Icon.FromHandle 拥有的句柄不会随 Icon.Dispose 释放）。</summary>
public sealed class TrayIconHandle : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr h);

    public Icon Icon { get; }

    private readonly IntPtr _hicon;

    public TrayIconHandle(IntPtr hicon)
    {
        _hicon = hicon;
        Icon = Icon.FromHandle(hicon);
    }

    public void Dispose()
    {
        try { DestroyIcon(_hicon); } catch { }
        Icon.Dispose();
    }
}

public static class IconFactory
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>绘制加班时间图标：圆环 + 中央小时数字（如 12.5）。错误时显示 !。</summary>
    public static TrayIconHandle MakeHours(double hours, bool error, bool showHours = true)
    {
        var n = GetSystemMetrics(49); // SM_CXSMICON
        if (n is < 12 or > 64) n = 16;
        var s = (float)n;
        using var bmp = new Bitmap(n, n);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var stroke = Math.Max(2f, s / 8f);

            if (error)
            {
                using var white = new SolidBrush(Color.White);
                var w = s / 8f;
                g.FillRectangle(white, s * 0.4375f, s * 0.21875f, w, s * 0.375f);
                g.FillEllipse(white, s * 0.4375f, s * 0.671875f, w, w);
            }
            else
            {
                // 圆环颜色随加班时长分级（浅→深），无数据灰色
                var ring = hours <= 0 ? Color.FromArgb(255, 140, 140, 140)
                    : hours < 8 ? Color.FromArgb(255, 76, 175, 80)
                    : hours < 16 ? Color.FromArgb(255, 255, 193, 7)
                    : Color.FromArgb(255, 244, 67, 54);
                var inset = stroke / 2f + s * 0.04f;
                var d = s - inset * 2f;
                using (var trackPen = new Pen(Color.FromArgb(255, 90, 90, 100), stroke))
                    g.DrawArc(trackPen, inset, inset, d, d, 0, 360);
                if (hours > 0)
                {
                    var frac = hours - Math.Floor(hours);
                    using var pen = new Pen(ring, stroke);
                    g.DrawArc(pen, inset, inset, d, d, -90, (float)Math.Clamp(frac, 0, 1) * 360f);
                }
                if (showHours && hours > 0)
                    DrawHoursText(g, s, hours, ring);
            }
        }
        return new TrayIconHandle(bmp.GetHicon());
    }

    /// <summary>圆环内绘制小时数字（一两位小数），硬边文字渲染 + 白色描边</summary>
    private static void DrawHoursText(Graphics g, float s, double hours, Color color)
    {
        var text = hours switch
        {
            >= 100 => ((int)hours).ToString(),
            >= 10 => hours.ToString("0.#"),
            _ => hours.ToString("0.##"),
        };
        var em = text.Length switch
        {
            1 => s * 0.60f,
            2 => s * 0.48f,
            3 => s * 0.40f,
            _ => s * 0.32f,
        };
        using var font = new Font(FontFamily.GenericSansSerif, em, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        var layout = new RectangleF(s * 0.06f, s * 0.10f, s * 0.88f, s * 0.80f);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        using var white = new SolidBrush(Color.White);
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            g.DrawString(text, font, white,
                new RectangleF(layout.X + dx, layout.Y + dy, layout.Width, layout.Height), fmt);
        }
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, layout, fmt);
    }
}

// ---------------- 工具 ----------------
public static class Fmt
{
    public static string Hours(double v) => $"{v:0.##}";

    public static string DateShort(string date)
    {
        return date.Length >= 10 ? date[5..] : date;
    }
}

public static class Log
{
    private static readonly object Gate = new();
    public static event Action<string>? Emitted;

    public static void Info(string fmt, params object[] args) => Write("INFO ", fmt, args);
    public static void Warn(string fmt, params object[] args) => Write("WARN ", fmt, args);
    public static void Error(string fmt, params object[] args) => Write("ERROR", fmt, args);

    private static void Write(string level, string fmt, object[] args)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} " + string.Format(fmt, args);
        lock (Gate)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(AppPaths.LogPath, line + Environment.NewLine);
            }
            catch { }
        }
        Emitted?.Invoke(line);
    }

    /// <summary>日志轮转：tray.log 超 1MB 时顺延为 tray.log.1 / tray.log.2（共 3 份）</summary>
    private static void RotateIfNeeded()
    {
        var path = AppPaths.LogPath;
        if (!File.Exists(path) || new FileInfo(path).Length <= 1_000_000) return;
        var one = path + ".1";
        var two = path + ".2";
        if (File.Exists(two)) File.Delete(two);
        if (File.Exists(one)) File.Move(one, two);
        File.Move(path, one);
    }
}
