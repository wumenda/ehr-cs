using System.Text.Json;

namespace EhrOvertimeTray;

/// <summary>
/// --selftest 自检：把 ehr-mcp/tests/test_overtime_calculator.py 的关键用例移植为 C# 断言，
/// 验证 C# 移植版计算器与原 Python 版行为等价。运行方式: EhrOvertimeTray.exe --selftest
/// </summary>
public static class Selftest
{
    public static bool Run()
    {
        var failures = new List<string>();
        var passed = 0;

        void Check(string name, bool cond)
        {
            if (cond) passed++;
            else failures.Add(name);
        }

        void TestWorkday()
        {
            // 9:00-20:00 → 2.0h
            var r = Calc("2024-05-06", ["09:00:00", "20:00:00"], workdays: true);
            Check("工作日 2h 汇总", r.WorkDayOverHours == 2.0 && r.WorkDayTimes == 1 && r.Details.Count == 1);
            Check("工作日 2h 类型", r.Details.Count == 1 && r.Details[0].Type == "工作日加班");

            // 加班 29 分钟：无明细但计天数
            r = Calc("2024-05-06", ["09:00:00", "18:29:00"], workdays: true);
            Check("29 分钟无明细", r.Details.Count == 0 && r.WorkDayTimes == 1 && r.WorkDayOverHours == 0.0);

            // 恰好 30 分钟：计入明细
            r = Calc("2024-05-06", ["09:00:00", "18:30:00"], workdays: true);
            Check("30 分钟有明细", r.Details.Count == 1 && Math.Abs(r.Details[0].Hours - 0.5) < 1e-9);

            // 多次打卡取最早/最晚
            r = Calc("2024-05-06", ["09:00:00", "12:00:00", "21:00:00"], workdays: true);
            Check("多次打卡 3h", Math.Abs(r.WorkDayOverHours - 3.0) < 1e-9);

            // 18:00 下班无加班
            r = Calc("2024-05-06", ["09:00:00", "18:00:00"], workdays: true);
            Check("无加班", r.WorkDayOverHours == 0.0 && r.WorkDayTimes == 0 && r.Details.Count == 0);

            // addr_status != '0' 且非补卡 → 排除
            r = Calc("2024-05-06", [("09:00:00", "1", "1"), ("21:00:00", "1", "0")], workdays: true);
            Check("addr_status 排除", r.WorkDayOverHours == 0.0 && r.WorkDayTimes == 0);

            // addr_status != '0' 但 status == '3'（补卡）→ 计入
            r = Calc("2024-05-06", [("09:00:00", "3", "1"), ("21:00:00", "1", "0")], workdays: true);
            Check("补卡计入 3h", Math.Abs(r.WorkDayOverHours - 3.0) < 1e-9);

            // 仅一次打卡无法计算
            r = Calc("2024-05-06", ["09:00:00"], workdays: true);
            Check("单次打卡忽略", r.WorkDayTimes == 0);
        }

        void TestMeal()
        {
            // 恰好 2.5h：不算餐
            var r = Calc("2024-05-06", ["09:00:00", "20:30:00"], workdays: true);
            Check("2.5h 无餐", r.WorkDayEatTimes == 0);

            // 2h31m：算餐
            r = Calc("2024-05-06", ["09:00:00", "20:31:00"], workdays: true);
            Check("2h31m 有餐", r.WorkDayEatTimes == 1);

            // 餐费 = 餐天数 × 20（两天 3h + 一天 1h）
            var r2 = NewCalc(
                BuildRecords([
                    RecordsFor("2024-05-06", [("09:00:00", "1", "0"), ("21:00:00", "1", "0")]),
                    RecordsFor("2024-05-07", [("09:00:00", "1", "0"), ("21:00:00", "1", "0")]),
                    RecordsFor("2024-05-08", [("09:00:00", "1", "0"), ("19:00:00", "1", "0")]),
                ]),
                BuildWorkdays(["2024-05-06", "2024-05-07", "2024-05-08"]));
            Check("餐费金额 40", r2.WorkDayEatTimes == 2 && Math.Abs(r2.MealAllowance - 40) < 1e-9);

            // 节假日加班不计餐补（5-11 不在工作日列表 → 节假日）
            var r3 = Calc("2024-05-11", ["09:00:00", "21:00:00"], workdays: false);
            Check("节假日无餐", r3.WorkDayEatTimes == 0 && Math.Abs(r3.HolidayOverHours - 12.0) < 1e-9 && r3.MealAllowance == 0);

            // 明细 meal 标记
            var r4 = NewCalc(
                BuildRecords([
                    RecordsFor("2024-05-06", [("09:00:00", "1", "0"), ("21:00:00", "1", "0")]),
                    RecordsFor("2024-05-07", [("09:00:00", "1", "0"), ("19:00:00", "1", "0")]),
                    RecordsFor("2024-05-12", [("09:00:00", "1", "0"), ("21:00:00", "1", "0")]),
                ]),
                BuildWorkdays(["2024-05-06", "2024-05-07"]));
            var mealFlags = r4.Details.ToDictionary(d => d.Date, d => d.Meal);
            Check("明细 meal 标记", mealFlags["2024-05-06"] && !mealFlags["2024-05-07"] && !mealFlags["2024-05-12"]);
        }

        void TestHoliday()
        {
            // 节假日无 30 分钟门槛
            var r = Calc("2024-05-11", ["10:00:00", "10:20:00"], workdays: false);
            Check("节假日无门槛", r.Details.Count == 1 && r.Details[0].Type == "节假日加班");

            // 70 分钟 = 1.1666h → 2 位小数 1.17
            r = Calc("2024-05-11", ["10:00:00", "11:10:00"], workdays: false);
            Check("节假日 2 位小数", Math.Abs(r.HolidayOverHours - 1.17) < 1e-9);
        }

        void TestSummary()
        {
            // 汇总保留 2 位小数，total = workday + holiday
            var r = NewCalc(
                BuildRecords([
                    RecordsFor("2024-05-06", [("09:00:00", "1", "0"), ("20:31:00", "1", "0")]),
                    RecordsFor("2024-05-11", [("10:00:00", "1", "0"), ("11:10:00", "1", "0")]),
                ]),
                BuildWorkdays(["2024-05-06"]));
            Check("汇总一致", r.TotalHours == r.WorkDayOverHours + r.HolidayOverHours
                             && r.TotalHours >= 0.1);
        }

        try
        {
            TestWorkday();
            TestMeal();
            TestHoliday();
            TestSummary();
        }
        catch (Exception e)
        {
            failures.Add("自检异常: " + e.Message);
        }

        Console.WriteLine(failures.Count == 0
            ? $"[selftest] PASS: 全部 {passed} 项断言通过，计算器与 ehr-mcp 行为一致"
            : $"[selftest] FAIL: {failures.Count} 项失败 / 通过 {passed} 项\n" + string.Join("\n", failures));
        return failures.Count == 0;
    }

    // ---------- 测试构造工具 ----------
    private static OvertimeResult Calc(string day, string[] punches, bool workdays)
        => NewCalc(BuildRecords([RecordsFor(day, punches.Select(p => (p, "1", "0")).ToArray())]),
            BuildWorkdays(workdays ? [day] : []));

    private static OvertimeResult Calc(string day, (string Kq, string Status, string Addr)[] punches, bool workdays)
        => NewCalc(BuildRecords([RecordsFor(day, punches)]), BuildWorkdays(workdays ? [day] : []));

    private static (string Day, string Kq, string Status, string Addr)[] RecordsFor(
        string day, IEnumerable<(string Kq, string Status, string Addr)> punches)
        => punches.Select(p => (day, p.Kq, p.Status, p.Addr)).ToArray();

    private static JsonElement BuildRecords((string Day, string Kq, string Status, string Addr)[][] groupByDay)
    {
        var list = new List<object>();
        foreach (var day in groupByDay)
        foreach (var p in day)
            list.Add(new { bc_date = p.Day, kq_time = $"{p.Day} {p.Kq}", status = p.Status, addr_status = p.Addr });
        return JsonDocument.Parse(JsonSerializer.Serialize(new { jsonList = list })).RootElement.Clone();
    }

    private static JsonElement BuildWorkdays(string[] days)
    {
        var list = days.Select(d => new { work_day = d, begin_time = "09:00", end_time = "18:00" }).ToList();
        var json = "{\"result\":{\"#result-set-1\":" + JsonSerializer.Serialize(list) + "}}";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static OvertimeResult NewCalc(JsonElement records, JsonElement workdays)
        => new OvertimeCalculator().Calculate(records, workdays);
}