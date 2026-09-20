using System.Diagnostics;

namespace EhrOvertimeTray;

/// <summary>账户设置对话框：输入 EHR 用户名/密码并保存（DPAPI 加密）</summary>
public sealed class AccountDialog : Form
{
    private readonly TextBox _user = new();
    private readonly TextBox _pwd = new();
    private readonly Button _ok = new() { Text = "保存", DialogResult = DialogResult.OK };
    private readonly Button _cancel = new() { Text = "取消", DialogResult = DialogResult.Cancel };

    public string Username => _user.Text.Trim();
    public string Password => _pwd.Text;

    public AccountDialog(string? currentUser = null)
    {
        Text = "设置 EHR 账号";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(320, 130);
        _ok.DialogResult = DialogResult.OK;
        _cancel.DialogResult = DialogResult.Cancel;

        var lblUser = new Label { Text = "用户名:", Location = new Point(12, 15), AutoSize = true };
        var lblPwd = new Label { Text = "密码:", Location = new Point(12, 48), AutoSize = true };
        _user.Location = new Point(75, 12);
        _user.Size = new Size(230, 23);
        _pwd.Location = new Point(75, 45);
        _pwd.Size = new Size(230, 23);
        _pwd.UseSystemPasswordChar = true;
        if (!string.IsNullOrEmpty(currentUser)) _user.Text = currentUser;

        _ok.Location = new Point(150, 90);
        _cancel.Location = new Point(230, 90);
        AcceptButton = _ok;
        CancelButton = _cancel;

        Controls.AddRange(new Control[] { lblUser, lblPwd, _user, _pwd, _ok, _cancel });
    }
}

/// <summary>托盘应用主体：托盘图标、菜单、刷新循环、悬停悬浮窗</summary>
public sealed class TrayApp : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly SynchronizationContext _ui;
    private readonly AppConfig _cfg;
    private readonly OvertimeService _service = new();
    private readonly History _history;
    private readonly CancellationTokenSource _stop = new();
    // 刷新互斥：循环/菜单/面板/账号保存 4 个入口共用，防止并发刷新与共享状态竞争
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly WebPanel _web;
    private readonly bool _webAvailable; // Web 面板启动失败（端口占用）时降级为仅托盘
    private readonly HashSet<string> _dailyNotified = new(); // 已弹通知的日期（重启后当天会再提示一次）
    private TrayIconHandle? _currentIcon;
    private HoverTip? _hover;
    private string? _lastLoggedError; // 同一错误只记录一次，避免日志刷屏

    public OvertimeState State { get; private set; } = new();
    public DateTime? LastOk { get; private set; }
    public int Interval => _cfg.RefreshIntervalSec;
    public string? UsernameForPanel => string.IsNullOrWhiteSpace(_cfg.Username) ? null : _cfg.Username;
    public double NotifyDailyHours => _cfg.NotifyDailyHours;
    public List<(string Date, double Hours)> History30 => _history.Series(_cfg.Username, 30);

    public TrayApp(AppConfig cfg)
    {
        _cfg = cfg;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        _history = new History(AppPaths.HistoryPath);

        _web = new WebPanel(this, _cfg.WebUiPort);
        _webAvailable = _web.Start();

        _currentIcon = IconFactory.MakeHours(0, error: false, _cfg.IconShowHours);
        _icon = new NotifyIcon
        {
            Icon = _currentIcon.Icon,
            Text = "加班时长: 加载中...",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenPanel();
        };
        _icon.MouseMove += (_, _) => ShowHover();

        _ = Task.Run(RunLoopAsync);
    }

    // ---- 菜单 ----
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var setAccount = new ToolStripMenuItem(string.IsNullOrWhiteSpace(_cfg.Username)
            ? "设置账号..."
            : $"账号: {_cfg.Username}...");
        setAccount.Click += (_, _) => PromptAccount();

        var refresh = new ToolStripMenuItem("立即刷新", null, (_, _) => _ = Task.Run(RefreshAsync));
        var showHours = new ToolStripMenuItem("图标显示小时数") { Checked = _cfg.IconShowHours };
        showHours.Click += (_, _) =>
        {
            _cfg.IconShowHours = !_cfg.IconShowHours;
            try { _cfg.Save(); }
            catch (Exception e) { Log.Warn("保存图标设置失败: {0}", e.Message); }
            showHours.Checked = _cfg.IconShowHours;
            UpdateUi();
        };
        var autoStart = new ToolStripMenuItem("开机自启") { Checked = Program.AutoStartEnabled() };
        autoStart.Click += (_, _) =>
        {
            try
            {
                Program.SetAutoStart(autoStart.Checked = !autoStart.Checked);
            }
            catch (Exception e)
            {
                Log.Error("设置开机自启失败: {0}", e.Message);
                autoStart.Checked = Program.AutoStartEnabled();
            }
        };

        var notifyInfo = new ToolStripMenuItem(
            _cfg.NotifyDailyHours <= 0
                ? "每日提醒: 关闭"
                : $"每日提醒: 加班 {_cfg.NotifyDailyHours:0.#}h 时通知")
        { Enabled = false };

        menu.Items.Add("打开面板", null, (_, _) => OpenPanel());
        menu.Items.Add(refresh);
        menu.Items.Add(setAccount);
        menu.Items.Add(showHours);
        menu.Items.Add(notifyInfo);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(autoStart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("打开日志", null, (_, _) => OpenLog());
        menu.Items.Add("退出", null, (_, _) =>
        {
            _stop.Cancel();
            _web.Stop();
            _icon.Visible = false;
            Application.Exit();
        });

        // 每次展开时刷新账号/提醒文案（面板修改配置后菜单保持同步）
        menu.Opening += (_, _) =>
        {
            setAccount.Text = string.IsNullOrWhiteSpace(_cfg.Username)
                ? "设置账号..."
                : $"账号: {_cfg.Username}...";
            notifyInfo.Text = _cfg.NotifyDailyHours <= 0
                ? "每日提醒: 关闭"
                : $"每日提醒: 加班 {_cfg.NotifyDailyHours:0.#}h 时通知";
        };
        return menu;
    }

    private void OpenLog()
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{AppPaths.LogPath}\"");
        }
        catch (Exception e)
        {
            Log.Warn("打开日志失败: {0}", e.Message);
        }
    }

    private void PromptAccount()
    {
        try
        {
            using var dlg = new AccountDialog(_cfg.Username);
            // 密码留空 = 保持原密码不变（与 Web 面板行为一致）
            if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Username))
            {
                SaveAccount(dlg.Username, dlg.Password);
            }
        }
        catch (Exception e)
        {
            Log.Error("设置账号失败: {0}", e.Message);
            MessageBox.Show("保存账号失败: " + e.Message, "加班时长", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>保存账号（密码留空则保持原密码不变），成功后等待进行中的刷新结束并立即用新凭据重查。供面板与右键菜单共用。</summary>
    public void SaveAccount(string username, string password)
    {
        var pwd = string.IsNullOrEmpty(password) ? _cfg.PasswordPlain : password;
        if (string.IsNullOrEmpty(pwd))
            throw new InvalidOperationException("密码不能为空");
        _cfg.SetCredentials(username, pwd);
        _cfg.Save();
        Log.Info("账号已更新: {0}", username);
        _ = Task.Run(ForceRefreshAfterAccountChange);
    }

    private void OpenPanel()
    {
        if (!_webAvailable)
        {
            MessageBox.Show($"Web 面板未能启动（端口 {_cfg.WebUiPort} 可能被占用），已降级为仅托盘模式。"
                + "可修改 config.json 中的 webui_port 后重启程序。", "加班时长",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(_web.Url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error("打开面板失败: {0}", e.Message);
        }
    }

    // ---- 悬停悬浮窗 ----
    private void ShowHover()
    {
        try
        {
            if (_hover is null)
            {
                _hover = new HoverTip();
                _hover.Hidden += RestoreTooltip; // 悬浮窗关闭后恢复系统 tooltip
            }
            if (_hover.Visible) return;
            _icon.Text = "";
            _hover.ShowAt(Cursor.Position, BuildTooltip());
        }
        catch (Exception e)
        {
            Log.Warn("悬停显示失败: {0}", e.Message);
        }
    }

    private void RestoreTooltip()
    {
        _ui.Post(_ =>
        {
            try { _icon.Text = ClampTooltip(); } catch { }
        }, null);
    }

    private string BuildTooltip()
    {
        var ls = new List<string>();
        var u = _cfg.Username;
        ls.Add($"本月加班 · {(string.IsNullOrEmpty(u) ? "(未设置账号)" : u)}");
        if (State.Error is not null)
        {
            ls.Add($"出错: {State.Error}");
        }
        else if (State.UpdatedAt is null)
        {
            ls.Add("尚未刷新，右键菜单设置账号后查询");
        }
        else
        {
            ls.Add($"工作日加班: {Fmt.Hours(State.WorkDayHours)}h");
            ls.Add($"节假日加班: {Fmt.Hours(State.HolidayHours)}h");
            ls.Add($"总加班时长: {Fmt.Hours(State.TotalHours)}h");
            ls.Add($"加班天数: {State.WorkDayTimes} / 餐补: {State.MealAllowance:0} 元");
            var recent = State.Details.Where(d => d.Date.CompareTo(DateTime.Now.AddDays(-14).ToString("yyyy-MM-dd")) >= 0)
                .OrderBy(d => d.Date).Take(7).ToList();
            if (recent.Count > 0)
            {
                ls.Add("---- 近 14 天 ----");
                foreach (var d in recent)
                    ls.Add($"{Fmt.DateShort(d.Date)} {d.Type} {Fmt.Hours(d.Hours)}h"
                           + (d.Meal ? " [餐]" : ""));
            }
        }
        if (LastOk is { } t)
        {
            ls.Add("");
            ls.Add("更新于 " + t.ToString("MM-dd HH:mm"));
        }
        return string.Join("\n", ls);
    }

    // ---- 刷新循环 ----
    private async Task RunLoopAsync()
    {
        // 启动后立即刷新一次；未配置账号时快速重试引导；连续失败时指数退避（15min→30min→1h 封顶），
        // 避免凭据失效后仍高频提交错误登录触发账号锁定/风控
        var first = true;
        var failures = 0;
        while (!_stop.IsCancellationRequested)
        {
            var ok = await RefreshAsync();
            if (ok || State.Error == "未设置账号") failures = 0;
            else failures++;

            try
            {
                var delay = first && string.IsNullOrWhiteSpace(_cfg.Username) ? 5 : _cfg.RefreshIntervalSec;
                if (failures > 0)
                    delay = (int)Math.Min(delay * Math.Pow(2, Math.Min(failures, 3)), 3600);
                await Task.Delay(delay * 1000, _stop.Token);
            }
            catch (OperationCanceledException) { break; }
            first = false;
        }
    }

    /// <summary>触发一次刷新（循环/菜单/面板共用）。已有刷新在执行时跳过并返回 true（不算失败）。</summary>
    public async Task<bool> RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0))
            return true; // 已有刷新在执行，跳过并发触发
        try
        {
            return await RefreshAsyncCore();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>保存账号后的强制刷新：等待进行中的刷新完成后立即用新凭据重查（最多等 3 分钟）。</summary>
    private async Task ForceRefreshAfterAccountChange()
    {
        try
        {
            await _refreshGate.WaitAsync(TimeSpan.FromSeconds(180));
            try { await RefreshAsyncCore(); }
            finally { _refreshGate.Release(); }
        }
        catch { }
    }

    private async Task<bool> RefreshAsyncCore()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_cfg.Username) || string.IsNullOrWhiteSpace(_cfg.PasswordPlain))
            {
                State = new OvertimeState { Error = "未设置账号" };
                return false;
            }
            var fresh = await _service.FetchAsync(_cfg.Username, _cfg.PasswordPlain);
            State = fresh;
            LastOk = fresh.UpdatedAt;
            RecordHistory(fresh);
            CheckDailyNotify(fresh);
            _lastLoggedError = null;
            Log.Info("刷新成功: 本月加班 {0}h（工作日 {1}h / 节假日 {2}h）",
                Fmt.Hours(fresh.TotalHours), Fmt.Hours(fresh.WorkDayHours), Fmt.Hours(fresh.HolidayHours));
            return true;
        }
        catch (Exception e)
        {
            // 网络类异常翻译为用户可读文案，原始信息进日志
            var msg = e switch
            {
                TaskCanceledException => "请求超时，请检查网络或 VPN",
                HttpRequestException => "网络连接失败，请检查网络或 VPN",
                _ => e.Message,
            };
            // 首次由成功转失败时弹气泡提醒（后续持续失败不重复打扰）
            var firstFailure = State.Error is null && LastOk is not null;
            if (_lastLoggedError != msg)
            {
                Log.Warn("刷新失败: {0}", e.Message);
                _lastLoggedError = msg;
            }
            State = new OvertimeState { Error = msg, Details = State.Details, UpdatedAt = State.UpdatedAt };
            if (firstFailure)
            {
                _ui.Post(_ =>
                {
                    try { _icon.ShowBalloonTip(6000, "刷新失败", msg, ToolTipIcon.Warning); }
                    catch { }
                }, null);
            }
            return false;
        }
        finally
        {
            UpdateUi();
        }
    }

    /// <summary>按明细写入每日加班时长历史（同一天重复刷新会覆盖为最新值）</summary>
    private void RecordHistory(OvertimeState fresh)
    {
        if (fresh.Details.Count == 0) return;
        var changed = false;
        foreach (var d in fresh.Details)
        {
            if (d.Date.Length < 10) continue;
            _history.Upsert(d.Date[..10], _cfg.Username, d.Hours);
            changed = true;
        }
        if (changed) _history.Save();
    }

    /// <summary>每日加班超时提醒：当天累计加班首次达到阈值时弹一次通知（0=关闭）</summary>
    private void CheckDailyNotify(OvertimeState fresh)
    {
        var threshold = _cfg.NotifyDailyHours;
        if (threshold <= 0) return;
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_dailyNotified.Contains(today)) return;
        var todayHours = fresh.Details.Where(d => d.Date == today).Sum(d => d.Hours);
        if (todayHours < threshold) return;

        _dailyNotified.Add(today);
        var msg = $"今日已加班 {Fmt.Hours(todayHours)}h，本月累计 {Fmt.Hours(fresh.TotalHours)}h。注意休息！";
        Log.Info("每日加班提醒: {0}", msg);
        _ui.Post(_ =>
        {
            try { _icon.ShowBalloonTip(6000, "加班提醒", msg, ToolTipIcon.Warning); }
            catch { }
        }, null);
    }

    /// <summary>更新每日提醒阈值（面板调用），0=关闭</summary>
    public void SetNotifyDailyHours(double hours)
    {
        _cfg.NotifyDailyHours = hours;
        _cfg.Save();
        Log.Info("每日加班提醒阈值已更新: {0}h", hours);
    }

    /// <summary>生成符合系统 127 字符上限的 tooltip 文本</summary>
    private string ClampTooltip()
    {
        var text = BuildTooltip();
        if (text.Length > 127) text = BuildShortTooltip();
        if (text.Length > 127) text = text[..127];
        return text;
    }

    private void UpdateUi()
    {
        var err = State.Error is not null;
        var hours = err ? 0 : State.TotalHours;
        var text = ClampTooltip();

        _ui.Post(_ =>
        {
            try
            {
                if (_hover is { Visible: true } h)
                {
                    h.UpdateText(BuildTooltip());
                }
                else
                {
                    _icon.Text = text;
                }
                var old = _currentIcon;
                _currentIcon = IconFactory.MakeHours(hours, err, _cfg.IconShowHours);
                _icon.Icon = _currentIcon.Icon;
                old?.Dispose(); // 先挂新图标再释放旧句柄，避免 GDI HICON 泄漏
            }
            catch { }
        }, null);
    }

    private string BuildShortTooltip()
    {
        var ls = new List<string>();
        if (State.Error is not null)
        {
            ls.Add($"加班: 出错({State.Error})");
        }
        else if (State.UpdatedAt is null)
        {
            ls.Add("加班: 未刷新");
        }
        else
        {
            ls.Add($"本月加班 {Fmt.Hours(State.TotalHours)}h");
            ls.Add($"工作日 {Fmt.Hours(State.WorkDayHours)}h / 节假日 {Fmt.Hours(State.HolidayHours)}h");
        }
        if (LastOk is { } t) ls.Add("更新 " + t.ToString("HH:mm"));
        return string.Join("\n", ls);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _web.Stop();
        _hover?.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _currentIcon?.Dispose();
    }
}
