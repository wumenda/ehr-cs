using System.Diagnostics;

namespace EhrOvertimeTray;

internal static class Program
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "EhrOvertimeTray";

    [STAThread]
    private static void Main(string[] args)
    {
        // 自检模式：验证 OvertimeCalculator 与 ehr-mcp 行为等价
        if (args.Length > 0 && args[0] == "--selftest")
        {
            Environment.Exit(Selftest.Run() ? 0 : 1);
        }

        // 单实例互斥：重复启动时提示并打开已运行实例的面板（端口按配置读取）
        using var mutex = new Mutex(true, @"Local\EhrOvertimeTray", out var createdNew);
        if (!createdNew)
        {
            try
            {
                var port = AppConfig.LoadOrCreate().WebUiPort;
                Process.Start(new ProcessStartInfo($"http://localhost:{port}/") { UseShellExecute = true });
            }
            catch { }
            MessageBox.Show("加班时长指示器已在运行，已为你打开详情面板。", "加班时长",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            // 在任何控件创建之前安装 WinForms 同步上下文：
            // 保证后台线程 _ui.Post 的回调被派发到 UI 消息循环（否则会落到线程池，跨线程操作控件，
            // 气泡通知/图标刷新可能失效）。必须在 SetCompatibleTextRenderingDefault 之后调用。
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            AppPaths.MigrateLegacyData();

            var cfg = AppConfig.LoadOrCreate();
            using var app = new TrayApp(cfg);
            Application.Run(new ApplicationContext());
        }
        catch (Exception e)
        {
            // 启动路径兜底：端口占用/配置异常等不再无提示崩溃
            Log.Error("启动失败: {0}", e.Message);
            try
            {
                MessageBox.Show("启动失败: " + e.Message, "加班时长",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    // ---- 开机自启（HKCU Run 键，无需管理员） ----
    public static bool AutoStartEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string p
               && p.Contains(Environment.ProcessPath ?? "\0", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetAutoStart(bool on)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null) return;
        if (on && Environment.ProcessPath is { } exe)
            key.SetValue(RunValueName, $"\"{exe}\"");
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }
}
