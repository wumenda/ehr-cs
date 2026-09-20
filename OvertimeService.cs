using System.Diagnostics;
using System.Text.Json;

namespace EhrOvertimeTray;

/// <summary>
/// 加班查询编排：登录 → staffId → 考勤/工作日数据 → 加班计算 → 状态。
/// 与 ehr-mcp 的 query_overtime 工具流程一致。
/// </summary>
public sealed class OvertimeService
{
    public static (string BeginDate, string EndDate) QueryDaysRange(int year, int month)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        return ($"{year:0000}-{month:00}-01", $"{year:0000}-{month:00}-{lastDay:00}");
    }

    public async Task<OvertimeState> FetchAsync(string username, string password)
    {
        var now = DateTime.Now;
        var (begin, end) = QueryDaysRange(now.Year, now.Month);

        OvertimeResult result;
        using var client = new EhrClient(username, password);
        await client.LoginAsync();
        var staffId = await client.GetStaffIdAsync();
        var records = await client.GetEhrRecordsAsync(staffId, begin, end);
        var workdayInfos = await client.GetWorkdayInfosAsync(staffId, begin, end);
        result = new OvertimeCalculator().Calculate(records, workdayInfos);

        var r = result;
        return new OvertimeState
        {
            Error = null,
            UpdatedAt = DateTime.Now,
            BeginDate = begin,
            EndDate = end,
            WorkDayHours = r.WorkDayOverHours,
            HolidayHours = r.HolidayOverHours,
            TotalHours = r.TotalHours,
            WorkDayTimes = r.WorkDayTimes,
            EatTimes = r.WorkDayEatTimes,
            MealAllowance = r.MealAllowance,
            Details = r.Details,
        };
    }
}