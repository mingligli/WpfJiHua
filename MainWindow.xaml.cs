using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopPlanWidget
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private int _showDays = 7;
        private const string AutoStartRegName = "DesktopPlanWidget";
        private DispatcherTimer _midnightTimer;

        private string _titleColor = "#FFFFFF";
        private string _dateColor = "#FFCC00";
        private string _contentColor = "#FFFFFF";

        public SolidColorBrush TitleBrush { get; set; }
        public SolidColorBrush DateBrush { get; set; }
        public SolidColorBrush ContentBrush { get; set; }

        private double _textOpacity = 1.0;
        public double TextOpacity
        {
            get => _textOpacity;
            set { _textOpacity = value; OnPropertyChanged(nameof(TextOpacity)); }
        }

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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadConfig();
            ApplyColor();

            sliderOpacity.ValueChanged += (s, e) => { TextOpacity = sliderOpacity.Value; SaveConfig(); };
            btnSettings.Click += (s, e) => settingPopup.IsOpen = true;
            BtnOpenManage.Click += (s, e) => OpenManage();
            BtnSetDays.Click += (s, e) => SetDays();
            BtnAppearance.Click += BtnAppearance_Click;
            BtnExit.Click += (s, e) => Close();
            BtnBackupData.Click += (s, e) => BackupData();
            BtnRestoreData.Click += (s, e) => RestoreData();

            Loaded += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                SetWindowPos(h, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_LAYERED);
                SetWindowToTopRight();
                DataHelper.GenerateNextRepeatPlans();
                RefreshList();
                StartMidnightTimer();
                SyncAutoStart();
            };

            CompositionTarget.Rendering += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int style = GetWindowLong(h, GWL_EXSTYLE);
                bool isCtrlDown = GetAsyncKeyState(VK_CONTROL) < 0;
                bool isPopupOpen = settingPopup.IsOpen;

                if (isCtrlDown || isPopupOpen)
                {
                    if ((style & WS_EX_TRANSPARENT) != 0)
                        SetWindowLong(h, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
                }
                else
                {
                    if ((style & WS_EX_TRANSPARENT) == 0)
                        SetWindowLong(h, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
                }
            };
        }

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
                TitleBrush = Brushes.White;
                DateBrush = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                ContentBrush = Brushes.White;
            }
            OnPropertyChanged(nameof(TitleBrush));
            OnPropertyChanged(nameof(DateBrush));
            OnPropertyChanged(nameof(ContentBrush));
        }

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

        private void SetDays()
        {
            Window input = new Window
            {
                Title = "显示天数",
                Width = 220,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
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

        private void SetWindowToTopRight()
        {
            Left = SystemParameters.WorkArea.Width - Width - 10;
            Top = 10;
        }

        private void RefreshList()
        {
            var list = DataHelper.GetCustomDaysPlans(_showDays);
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

        private string GetDisplayDate(DateTime dt)
        {
            var cal = new ChineseLunisolarCalendar();
            int m = cal.GetMonth(dt);
            int d = cal.GetDayOfMonth(dt);
            string[] monthNames = { "", "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "十一月", "腊月" };
            string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            string day;

            if (d <= 10) day = d == 10 ? "初十" : "初" + digits[d];
            else if (d <= 20) day = d == 20 ? "二十" : "十" + digits[d - 10];
            else day = d == 30 ? "三十" : "廿" + digits[d - 20];

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

            return $"{dt:yyyy-MM-dd HH:mm} {monthNames[m]} {day} {week}";
        }

        private void OpenManage()
        {
            var w = new EditPlanWindow();
            w.Owner = this;
            w.ShowDialog();
            RefreshList();
        }

        #region 开机自启
        private bool CheckAutoStartReg()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue(AutoStartRegName) != null;
        }

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

        #region 备份恢复
        private void BackupData()
        {
            SaveFileDialog d = new SaveFileDialog { Filter = "备份|*.bak", FileName = $"backup_{DateTime.Now:yyyyMMdd}" };
            if (d.ShowDialog() == true)
            {
                SaveConfig();
                DataHelper.BackupAll(d.FileName);
                MessageBox.Show("备份成功", "提示");
            }
        }

        // 备份恢复（修改后：恢复后立即刷新界面，立即生效）
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

    public class PlanItem
    {
        public DateTime PlanDate { get; set; }
        public string Content { get; set; }
        public bool IsFinish { get; set; }
        public string DisplayDate { get; set; }
    }
}