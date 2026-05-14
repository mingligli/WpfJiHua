using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopPlanWidget
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private DispatcherTimer _dailyTimer;
        private int _showDays = 7;
        private const string PLAN_FILE = "plans.txt";

        private string _titleColor = "#FFFFFF";
        private string _dateColor = "#FFCC00";
        private string _contentColor = "#FFFFFF";

        private SolidColorBrush _titleBrush;
        public SolidColorBrush TitleBrush
        {
            get => _titleBrush;
            set { _titleBrush = value; OnPropertyChanged(nameof(TitleBrush)); }
        }

        private SolidColorBrush _dateBrush;
        public SolidColorBrush DateBrush
        {
            get => _dateBrush;
            set { _dateBrush = value; OnPropertyChanged(nameof(DateBrush)); }
        }

        private SolidColorBrush _contentBrush;
        public SolidColorBrush ContentBrush
        {
            get => _contentBrush;
            set { _contentBrush = value; OnPropertyChanged(nameof(ContentBrush)); }
        }

        // 🔥 文字透明度
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
        private const int WS_EX_LAYERED = 0x0008000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 16;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const int VK_CONTROL = 0x11;
        private const string AutoStartRegName = "桌面提醒.exe";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            LoadColorToPlanFile();
            ApplyColor();

            // 透明度滑动
            sliderOpacity.ValueChanged += (s, e) => { TextOpacity = sliderOpacity.Value; SaveColorToPlanFile(); };

            btnSettings.Click += (s, e) => settingPopup.IsOpen = true;
            BtnOpenManage.Click += (s, e) => OpenManage();
            BtnSetDays.Click += (s, e) => SetDays();
            BtnAppearance.Click += BtnAppearance_Click;
            BtnExit.Click += (s, e) => Close();
            BtnBackupData.Click += (s, e) => BackupData();
            BtnRestoreData.Click += (s, e) => RestoreData();

            CompositionTarget.Rendering += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int style = GetWindowLong(h, GWL_EXSTYLE);
                if (GetAsyncKeyState(VK_CONTROL) < 0)
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

            Loaded += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                SetWindowPos(h, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_LAYERED);
                SetWindowToTopRight();
                RefreshList();
                LoadAutoStartCheck();
            };
        }

        #region 颜色 + 天数 + 透明度 配置
        private void LoadColorToPlanFile()
        {
            try
            {
                if (!File.Exists(PLAN_FILE))
                {
                    _showDays = 7;
                    _textOpacity = 1.0;
                    return;
                }

                var lines = File.ReadAllLines(PLAN_FILE);
                foreach (var line in lines)
                {
                    if (line.StartsWith("[COLOR]"))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 4)
                        {
                            _titleColor = parts[1];
                            _dateColor = parts[2];
                            _contentColor = parts[3];
                        }
                    }
                    if (line.StartsWith("[DAYS]"))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int d))
                        {
                            _showDays = d;
                        }
                    }
                    if (line.StartsWith("[OPACITY]"))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 2 && double.TryParse(parts[1], out double op))
                        {
                            _textOpacity = op;
                        }
                    }
                }
            }
            catch
            {
                _showDays = 7;
                _textOpacity = 1.0;
            }
        }

        private void SaveColorToPlanFile()
        {
            try
            {
                List<string> newLines = new List<string>();
                bool hasColor = false;
                bool hasDays = false;
                bool hasOpacity = false;

                if (File.Exists(PLAN_FILE))
                {
                    foreach (var line in File.ReadAllLines(PLAN_FILE))
                    {
                        if (line.StartsWith("[COLOR]"))
                        {
                            newLines.Add($"[COLOR]|{_titleColor}|{_dateColor}|{_contentColor}");
                            hasColor = true;
                        }
                        else if (line.StartsWith("[DAYS]"))
                        {
                            newLines.Add($"[DAYS]|{_showDays}");
                            hasDays = true;
                        }
                        else if (line.StartsWith("[OPACITY]"))
                        {
                            newLines.Add($"[OPACITY]|{_textOpacity}");
                            hasOpacity = true;
                        }
                        else
                        {
                            newLines.Add(line);
                        }
                    }
                }

                if (!hasColor)
                    newLines.Add($"[COLOR]|{_titleColor}|{_dateColor}|{_contentColor}");
                if (!hasDays)
                    newLines.Add($"[DAYS]|{_showDays}");
                if (!hasOpacity)
                    newLines.Add($"[OPACITY]|{_textOpacity}");

                File.WriteAllLines(PLAN_FILE, newLines);
            }
            catch { }
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
        }
        #endregion

        private void BtnAppearance_Click(object sender, RoutedEventArgs e)
        {
            var win = new AppearanceSettingWindow(_titleColor, _dateColor, _contentColor);
            win.Owner = this;
            win.ShowDialog();

            if (win.DialogResult == true)
            {
                _titleColor = win.TitleColor;
                _dateColor = win.DateColor;
                _contentColor = win.ContentColor;
                SaveColorToPlanFile();
                ApplyColor();
            }
        }

        private void SetDays()
        {
            Window input = new Window
            {
                Title = "设置显示天数",
                Width = 220,
                Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var panel = new StackPanel { Margin = new Thickness(15, 10, 15, 15) };
            var txt = new TextBox
            {
                Text = _showDays.ToString(),
                Height = 28,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var ok = new Button
            {
                Content = "确定",
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            ok.Click += (s, e) =>
            {
                if (int.TryParse(txt.Text, out int d) && d >= 1 && d <= 30)
                {
                    _showDays = d;
                    SaveColorToPlanFile();
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
            SetWindowToTopRight();
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
            return $"{dt:yyyy-MM-dd} {monthNames[m]} {day} {week}";
        }

        private void OpenManage()
        {
            var w = new EditPlanWindow();
            w.Owner = this;
            w.ShowDialog();
            RefreshList();
        }

        #region 开机自启
        private void SetAutoStart(bool isOpen)
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                using var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (isOpen) k.SetValue(AutoStartRegName, $"\"{exe}\"");
                else k.DeleteValue(AutoStartRegName, false);
                LoadAutoStartCheck();
            }
            catch { }
        }

        private void LoadAutoStartCheck()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                chkAutoStart.IsChecked = k.GetValue(AutoStartRegName) != null;
            }
            catch { chkAutoStart.IsChecked = false; }
        }

        private void chkAutoStart_Checked(object sender, RoutedEventArgs e) => SetAutoStart(true);
        private void chkAutoStart_Unchecked(object sender, RoutedEventArgs e) => SetAutoStart(false);
        #endregion

        #region 备份恢复
        private void BackupData()
        {
            SaveFileDialog d = new SaveFileDialog { Filter = "备份文件|*.bak", FileName = $"backup_{DateTime.Now:yyyyMMdd}.bak" };
            if (d.ShowDialog() == true)
                File.Copy(PLAN_FILE, d.FileName, true);
        }

        private void RestoreData()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "备份文件|*.bak" };
            if (d.ShowDialog() == true)
            {
                File.Copy(d.FileName, PLAN_FILE, true);
                LoadColorToPlanFile();
                ApplyColor();
                RefreshList();
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