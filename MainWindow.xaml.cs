using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopPlanWidget
{
    /// <summary>
    /// 主窗口：桌面计划挂件核心界面
    /// 实现计划展示、置顶/置底、鼠标穿透、配置保存、开机自启、备份恢复等功能
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region 属性变更通知
        /// <summary>
        /// 属性变更事件（实现UI自动刷新）
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propName">属性名称</param>
        private void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        #endregion

        #region 全局变量
        /// <summary>
        /// 默认显示未来天数的计划
        /// </summary>
        private int _showDays = 7;

        /// <summary>
        /// 开机自启注册表项名称
        /// </summary>
        private const string AutoStartRegName = "DesktopPlanWidget";

        /// <summary>
        /// 午夜定时器（每日凌晨刷新重复任务）
        /// </summary>
        private DispatcherTimer _midnightTimer;

        /// <summary>
        /// 标题颜色（十六进制）
        /// </summary>
        private string _titleColor = "#FFFFFF";

        /// <summary>
        /// 日期颜色（十六进制）
        /// </summary>
        private string _dateColor = "#FFCC00";

        /// <summary>
        /// 内容颜色（十六进制）
        /// </summary>
        private string _contentColor = "#FFFFFF";

        /// <summary>
        /// 标题画刷（UI绑定）
        /// </summary>
        public SolidColorBrush TitleBrush { get; set; }

        /// <summary>
        /// 日期画刷（UI绑定）
        /// </summary>
        public SolidColorBrush DateBrush { get; set; }

        /// <summary>
        /// 内容画刷（UI绑定）
        /// </summary>
        public SolidColorBrush ContentBrush { get; set; }

        /// <summary>
        /// 文本透明度
        /// </summary>
        private double _textOpacity = 1.0;

        /// <summary>
        /// 文本透明度（支持绑定+自动通知UI刷新）
        /// </summary>
        public double TextOpacity
        {
            get => _textOpacity;
            set { _textOpacity = value; OnPropertyChanged(nameof(TextOpacity)); }
        }
        #endregion

        #region Win32 API 导入（窗口穿透、置顶、置底功能）
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr ins, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 16;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const int VK_CONTROL = 0x11;
        #endregion

        /// <summary>
        /// 主窗口构造函数
        /// </summary>
        public MainWindow()
        {
            // 强制设置工作目录为程序所在目录（解决开机自启动读不到数据）
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            InitializeComponent();
            DataContext = this;

            // 加载配置并应用颜色样式
            LoadConfig();
            ApplyColor();

            // 绑定UI控件事件
            sliderOpacity.ValueChanged += (s, e) => { TextOpacity = sliderOpacity.Value; SaveConfig(); };
            btnSettings.Click += (s, e) => settingPopup.IsOpen = true;
            BtnOpenManage.Click += (s, e) => OpenManage();
            BtnSetDays.Click += (s, e) => SetDays();
            BtnAppearance.Click += BtnAppearance_Click;
            BtnExit.Click += (s, e) => Close();
            BtnBackupData.Click += (s, e) => BackupData();
            BtnRestoreData.Click += (s, e) => RestoreData();

            // 窗口加载完成后执行初始化
            Loaded += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                // 将窗口置于桌面最底层
                SetWindowPos(h, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                // 设置分层窗口样式
                SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_LAYERED);
                // 窗口定位到屏幕右上角
                SetWindowToTopRight();
                // 生成过期的重复计划
                DataHelper.GenerateNextRepeatPlans();
                // 刷新计划列表
                RefreshList();
                // 启动凌晨自动刷新定时器
                StartMidnightTimer();
                // 同步开机自启状态
                SyncAutoStart();
            };

            // 实时渲染事件：实现按住Ctrl键取消窗口鼠标穿透
            CompositionTarget.Rendering += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int style = GetWindowLong(h, GWL_EXSTYLE);
                bool isCtrlDown = GetAsyncKeyState(VK_CONTROL) < 0;
                bool isPopupOpen = settingPopup.IsOpen;

                // 按住Ctrl 或 打开设置面板 → 取消鼠标穿透，可点击窗口
                if (isCtrlDown || isPopupOpen)
                {
                    if ((style & WS_EX_TRANSPARENT) != 0)
                        SetWindowLong(h, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
                }
                // 正常状态 → 开启鼠标穿透
                else
                {
                    if ((style & WS_EX_TRANSPARENT) == 0)
                        SetWindowLong(h, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
                }
            };
        }

        /// <summary>
        /// 启动凌晨定时器：每天1点自动刷新重复计划
        /// </summary>
        private void StartMidnightTimer()
        {
            _midnightTimer = new DispatcherTimer();
            _midnightTimer.Interval = TimeSpan.FromMinutes(1);
            _midnightTimer.Tick += (s, e) =>
            {
                if (DateTime.Now.Hour == 1 && DateTime.Now.Minute == 0)
                {
                    DataHelper.GenerateNextRepeatPlans();
                    RefreshList();
                }
            };
            _midnightTimer.Start();
        }

        /// <summary>
        /// 从文件加载应用配置（显示天数、透明度、颜色等）
        /// </summary>
        private void LoadConfig()
        {
            var cfg = DataHelper.GetConfig();
            _showDays = cfg.ShowDays;
            TextOpacity = cfg.TextOpacity;
            _titleColor = cfg.TitleColor;
            _dateColor = cfg.DateColor;
            _contentColor = cfg.ContentColor;
            sliderOpacity.Value = TextOpacity;
        }

        /// <summary>
        /// 保存当前配置到文件
        /// </summary>
        private void SaveConfig()
        {
            var cfg = DataHelper.GetConfig();
            cfg.ShowDays = _showDays;
            cfg.TextOpacity = TextOpacity;
            cfg.TitleColor = _titleColor;
            cfg.DateColor = _dateColor;
            cfg.ContentColor = _contentColor;
            DataHelper.SaveConfig(cfg);
        }

        /// <summary>
        /// 应用颜色配置到界面画刷
        /// </summary>
        private void ApplyColor()
        {
            try
            {
                TitleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_titleColor));
                DateBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_dateColor));
                ContentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_contentColor));
            }
            catch
            {
                // 颜色解析失败时使用默认白色/黄色
                TitleBrush = Brushes.White;
                DateBrush = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                ContentBrush = Brushes.White;
            }
            // 通知UI刷新颜色
            OnPropertyChanged(nameof(TitleBrush));
            OnPropertyChanged(nameof(DateBrush));
            OnPropertyChanged(nameof(ContentBrush));
        }

        /// <summary>
        /// 打开外观设置窗口（修改标题/日期/内容颜色）
        /// </summary>
        private void BtnAppearance_Click(object sender, RoutedEventArgs e)
        {
            var win = new AppearanceSettingWindow(_titleColor, _dateColor, _contentColor);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                _titleColor = win.TitleColor;
                _dateColor = win.DateColor;
                _contentColor = win.ContentColor;
                SaveConfig();
                ApplyColor();
            }
        }

        /// <summary>
        /// 设置计划显示天数（弹窗输入）
        /// </summary>
        private void SetDays()
        {
            //使用代码创建一个简单的输入窗口，要求输入1-30的整数
            Window input = new()
            {
                Title = "显示天数",
                Width = 220,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false//不会在 Windows 底部任务栏上显示图标
            };

            var panel = new StackPanel { Margin = new Thickness(15) };
            var txt = new TextBox { Text = _showDays.ToString(), Height = 30 };
            var ok = new Button { Content = "确定", Width = 100, Margin = new Thickness(0, 10, 0, 0) };
            ok.Click += (s, e) =>
            {
                if (int.TryParse(txt.Text, out int d) && d >= 1 && d <= 30)
                {
                    _showDays = d;
                    SaveConfig();
                    RefreshList();
                    input.Close();
                }
            };
            panel.Children.Add(txt);
            panel.Children.Add(ok);
            input.Content = panel;
            input.ShowDialog();
        }

        /// <summary>
        /// 将窗口定位到屏幕右上角
        /// </summary>
        private void SetWindowToTopRight()
        {
            Left = SystemParameters.WorkArea.Width - Width - 10;
            Top = 10;
        }

        /// <summary>
        /// 刷新界面计划列表（读取数据并绑定到UI）
        /// </summary>
        private void RefreshList()
        {
            var list = DataHelper.GetCustomDaysPlans(_showDays).OrderBy(p => p.PlanDate).ToList();
            var result = new List<PlanItem>();
            foreach (var p in list)
                result.Add(new PlanItem
                {
                    PlanDate = p.PlanDate,
                    Content = p.Content,
                    IsFinish = p.IsFinish,
                    DisplayDate = GetDisplayDate(p.PlanDate)
                });
            lstPlan.ItemsSource = result;
        }

        /// <summary>
        /// 格式化日期显示：公历+农历+星期 三合一展示
        /// </summary>
        /// <param name="dt">日期时间</param>
        /// <returns>格式化后的日期字符串</returns>
        private string GetDisplayDate(DateTime dt)
        {
            var cal = new ChineseLunisolarCalendar();
            int m = cal.GetMonth(dt);
            int d = cal.GetDayOfMonth(dt);
            string[] monthNames = { "", "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "十一月", "腊月" };
            string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            string day;

            // 农历日期格式化
            if (d <= 10) day = d == 10 ? "初十" : "初" + digits[d];
            else if (d <= 20) day = d == 20 ? "二十" : "十" + digits[d - 10];
            else day = d == 30 ? "三十" : "廿" + digits[d - 20];

            // 星期格式化
            string week = dt.DayOfWeek switch
            {
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                DayOfWeek.Sunday => "周日",
                _ => ""
            };

            // 组合最终显示格式
            return $"{dt:yyyy-MM-dd HH:mm} {monthNames[m]} {day} {week}";
        }

        /// <summary>
        /// 打开计划管理编辑窗口
        /// </summary>
        private void OpenManage()
        {
            var w = new EditPlanWindow();
            w.Owner = this;
            w.ShowDialog();
            RefreshList();
        }

        #region 开机自启功能
        /// <summary>
        /// 检查注册表中是否已设置开机自启
        /// </summary>
        /// <returns>已设置返回true</returns>
        private bool CheckAutoStartReg()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue(AutoStartRegName) != null;
        }

        /// <summary>
        /// 设置开机自启注册表项
        /// </summary>
        /// <param name="enable">true=开启自启，false=关闭自启</param>
        private void SetAutoStartReg(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                string path = Process.GetCurrentProcess().MainModule.FileName;
                if (enable)
                    key.SetValue(AutoStartRegName, path);
                else
                    key.DeleteValue(AutoStartRegName, false);
            }
            catch { }
        }

        /// <summary>
        /// 同步配置文件与注册表、UI勾选框的自启状态
        /// </summary>
        private void SyncAutoStart()
        {
            var cfg = DataHelper.GetConfig();
            // 界面勾选同步配置
            chkAutoStart.IsChecked = cfg.AutoStart;

            // 绑定勾选事件：改勾选 立刻写注册表 + 保存配置
            chkAutoStart.Checked += (s, e) =>
            {
                SetAutoStartReg(true);
                cfg.AutoStart = true;
                DataHelper.SaveConfig(cfg);
            };
            chkAutoStart.Unchecked += (s, e) =>
            {
                SetAutoStartReg(false);
                cfg.AutoStart = false;
                DataHelper.SaveConfig(cfg);
            };

            // 初始化：按配置自动写入注册表
            SetAutoStartReg(cfg.AutoStart);
        }
        #endregion

        #region 数据备份与恢复
        /// <summary>
        /// 备份所有数据（计划+配置）到.bak文件
        /// </summary>
        private void BackupData()
        {
            SaveFileDialog d = new() { Filter = "备份|*.bak", FileName = $"backup_{DateTime.Now:yyyyMMdd}" };
            if (d.ShowDialog() == true)
            {
                SaveConfig();
                DataHelper.BackupAll(d.FileName);
                MessageBox.Show("备份成功", "提示");
            }
        }

        /// <summary>
        /// 从.bak备份文件恢复数据，并刷新界面所有配置和计划
        /// </summary>
        private void RestoreData()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "备份|*.bak" };
            if (d.ShowDialog() == true)
            {
                DataHelper.RestoreAll(d.FileName);
                LoadConfig();
                ApplyColor();
                DataHelper.GenerateNextRepeatPlans();
                RefreshList();
                // 恢复备份后同步开机自启勾选和注册表
                SyncAutoStart();
                MessageBox.Show("恢复成功，数据已立即生效！", "成功");
            }
        }
        #endregion
    }

    /// <summary>
    /// 计划展示项（主界面UI绑定实体）
    /// </summary>
    public class PlanItem
    {
        public DateTime PlanDate { get; set; }
        public string Content { get; set; }
        public bool IsFinish { get; set; }
        public string DisplayDate { get; set; }
    }
}