using System.Text.Json;

namespace EhrOvertimeTray;

/// <summary>
/// 加班时长计算器 - 从 ehr-mcp(OvertimeCalculator) 精确移植。
/// 精度规则（JS Math.round 四舍五入、向下取整到分钟、工作日 3 位小数中间精度 / 节假日 2 位小数、
/// 30 分钟门槛、>2.5h 加班餐）与原版严格一致，由 --selftest 用例集保证等价性。
/// </summary>
public sealed class OvertimeCalculator
{
    // 加班餐补标准：工作日加班时长 > 2.5h 计一天，每天 20 元（节假日不计）
    public const double MealAllowancePerDay = 20;

    // 标准工时 9h（9:00~18:00）
    private const double WorkDayHours = 9;

    private static readonly string[] KqTimeFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm",
    };

    private double _workDayOverHours;
    private double _holidayOverHours;
    private int _workDayTimes;
    private int _workDayEatTimes;
    private readonly List<OvertimeDetail> _details = new();

    public OvertimeResult Calculate(JsonElement ehrRecords, JsonElement workdayInfos)
    {
        _workDayOverHours = 0;
        _holidayOverHours = 0;
        _workDayTimes = 0;
        _workDayEatTimes = 0;
        _details.Clear();

        var (allWorkDays, allKq) = PrepareData(ehrRecords, workdayInfos);

        foreach (var kv in allKq)
        {
            var bcDate = kv.Key;
            var kqTimes = kv.Value;
            if (kqTimes.Count <= 1) continue; // 当天打卡次数 <= 1，无法计算加班

            var firstKq = kqTimes.Min();
            var lastKq = kqTimes.Max();
            var firstKqStr = firstKq.ToString("HH:mm:ss");
            var lastKqStr = lastKq.ToString("HH:mm:ss");

            if (allWorkDays.ContainsKey(bcDate))
            {
                // ===== 工作日加班 =====
                var overSeconds = Math.Floor((lastKq - firstKq).TotalSeconds) - WorkDayHours * 3600;
                if (overSeconds > 0)
                {
                    var seconds = Math.Floor(overSeconds / 60) * 60; // 向下取整到分钟
                    var hours = JsRound(seconds / 3600 * 1000) / 1000.0; // 3 位小数

                    if (seconds >= 1800) // 加班 >= 30 分钟才计入工时
                    {
                        _workDayOverHours += hours;
                        _details.Add(new OvertimeDetail
                        {
                            Date = bcDate,
                            Type = "工作日加班",
                            Hours = hours,
                            FirstKq = firstKqStr,
                            LastKq = lastKqStr,
                            Abnormal = false,
                            Meal = hours > 2.5,
                        });
                    }

                    // 只要 overSeconds > 0 就计为加班天数（哪怕不到 30 分钟）
                    _workDayTimes += 1;
                    if (hours > 2.5) _workDayEatTimes += 1;
                }
            }
            else
            {
                // ===== 节假日加班（非工作日）=====
                var seconds = Math.Floor((lastKq - firstKq).TotalSeconds);
                if (seconds > 0)
                {
                    seconds = Math.Floor(seconds / 60) * 60; // 向下取整到分钟
                    var hours = JsRound(seconds / 3600 * 100) / 100.0; // 2 位小数
                    _holidayOverHours += hours;
                    _details.Add(new OvertimeDetail
                    {
                        Date = bcDate,
                        Type = "节假日加班",
                        Hours = hours,
                        FirstKq = firstKqStr,
                        LastKq = lastKqStr,
                        Abnormal = false,
                        Meal = false,
                    });
                }
            }
        }

        return GetResult();
    }

    public OvertimeResult GetResult()
    {
        var workTotal = JsRound(_workDayOverHours * 100) / 100.0;
        var holidayTotal = JsRound(_holidayOverHours * 100) / 100.0;
        var total = JsRound((_workDayOverHours + _holidayOverHours) * 100) / 100.0;
        return new OvertimeResult
        {
            WorkDayOverHours = workTotal,
            HolidayOverHours = holidayTotal,
            TotalHours = total,
            WorkDayTimes = _workDayTimes,
            WorkDayEatTimes = _workDayEatTimes,
            MealAllowance = _workDayEatTimes * MealAllowancePerDay,
            Details = _details.ToList(),
        };
    }

    // ---------- 数据预处理 ----------
    private static (Dictionary<string, (DateTime? Begin, DateTime? End)> WorkDays,
                    Dictionary<string, List<DateTime>> Kq) PrepareData(
        JsonElement ehrRecords, JsonElement workdayInfos)
    {
        // ----- A: allWorkDays: work_day -> [beginTime, endTime] -----
        var allWorkDays = new Dictionary<string, (DateTime?, DateTime?)>();
        var resultSet = GetStringArray(workdayInfos, "result", "#result-set-1");
        foreach (var aDay in resultSet)
        {
            var beginTime = GetString(aDay, "begin_time");
            var endTime = GetString(aDay, "end_time");
            // 两者都为空则跳过（与 JS 原版一致）
            if (string.IsNullOrEmpty(beginTime) && string.IsNullOrEmpty(endTime)) continue;
            var workDay = GetString(aDay, "work_day");
            if (string.IsNullOrEmpty(workDay)) continue;
            allWorkDays[workDay] = (ParseDt($"{workDay} {beginTime}"), ParseDt($"{workDay} {endTime}"));
        }

        // ----- B: allKq: bc_date -> [kq_time, ...] -----
        var allKq = new Dictionary<string, List<DateTime>>();
        var jsonList = GetStringArray(ehrRecords, "jsonList");
        foreach (var aRecord in jsonList)
        {
            var bcDate = GetString(aRecord, "bc_date");
            if (string.IsNullOrEmpty(bcDate)) continue;
            if (!allKq.ContainsKey(bcDate)) allKq[bcDate] = new List<DateTime>();

            var status = GetString(aRecord, "status");
            var kqTimeStr = GetString(aRecord, "kq_time");
            var statusIs3 = status == "3";

            // 规则 1: status == '3' 的打卡时间一定加入
            if (statusIs3 && ParseDt(kqTimeStr) is { } dt3)
                allKq[bcDate].Add(dt3);

            // 规则 2: addr_status != '0' 的记录跳过后续处理
            var addrStatus = GetString(aRecord, "addr_status");
            if (!string.IsNullOrEmpty(addrStatus) && addrStatus != "0") continue;

            // 规则 3: 无条件加入打卡时间（status=='3' 会再加入一次，min/max 取极值不受影响）
            if (ParseDt(kqTimeStr) is { } dt)
                allKq[bcDate].Add(dt);
        }

        return (allWorkDays, allKq);
    }

    // ---------- 工具 ----------
    /// <summary>JS Math.round：正数四舍五入（half up），本模块所有舍入均为非负值</summary>
    private static long JsRound(double x) => (long)Math.Floor(x + 0.5);

    private static DateTime? ParseDt(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        foreach (var fmt in KqTimeFormats)
        {
            if (DateTime.TryParseExact(s, fmt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
        }
        return null;
    }

    private static string GetString(JsonElement obj, string name)
    {
        return obj.ValueKind == JsonValueKind.Object
               && obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? ""
            : "";
    }

    private static IEnumerable<JsonElement> GetStringArray(JsonElement root, params string[] path)
    {
        var cur = root;
        for (var i = 0; i < path.Length; i++)
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(path[i], out cur))
                return Enumerable.Empty<JsonElement>();
        }
        return cur.ValueKind == JsonValueKind.Array
            ? cur.EnumerateArray().ToList()
            : Enumerable.Empty<JsonElement>();
    }
}

public sealed class OvertimeResult
{
    public double WorkDayOverHours { get; set; }
    public double HolidayOverHours { get; set; }
    public double TotalHours { get; set; }
    public int WorkDayTimes { get; set; }
    public int WorkDayEatTimes { get; set; }
    public double MealAllowance { get; set; }
    public List<OvertimeDetail> Details { get; set; } = new();
}