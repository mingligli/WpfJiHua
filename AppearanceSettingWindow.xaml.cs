using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPlanWidget
{
    public partial class AppearanceSettingWindow : Window
    {
        public string TitleColor { get; set; }
        public string DateColor { get; set; }
        public string ContentColor { get; set; }

        private readonly Color[] _colorPool =
        {
            Colors.White, Colors.Black, Colors.Red, Colors.Green, Colors.Blue,
            Colors.Yellow, Colors.Orange, Colors.Purple, Colors.Cyan, Colors.Gray,
            Color.FromRgb(255,204,0), Color.FromRgb(180,180,180)
        };

        public AppearanceSettingWindow(string t, string d, string c)
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // 赋值
            TitleColor = t;
            DateColor = d;
            ContentColor = c;

            // 🔥 强制刷新显示（打开就显示）
            try
            {
                btnTitle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(t));
                btnDate.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(d));
                btnContent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c));
            }
            catch { }

            // 选择颜色
            btnTitle.Click += (s, e) => PickColor(btnTitle, v => { TitleColor = v; btnTitle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(v)); });
            btnDate.Click += (s, e) => PickColor(btnDate, v => { DateColor = v; btnDate.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(v)); });
            btnContent.Click += (s, e) => PickColor(btnContent, v => { ContentColor = v; btnContent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(v)); });

            // 保存
            btnSave.Click += (s, e) => { DialogResult = true; Close(); };

            // 重置默认
            btnReset.Click += (s, e) =>
            {
                TitleColor = "#FFFFFF";
                DateColor = "#FFCC00";
                ContentColor = "#FFFFFF";

                // 🔥 直接刷新
                btnTitle.Background = Brushes.White;
                btnDate.Background = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                btnContent.Background = Brushes.White;
            };

            // 关闭
            btnClose.Click += (s, e) => { DialogResult = false; Close(); };
        }

        private void PickColor(Button btn, Action<string> onSelected)
        {
            var win = new Window
            {
                Title = "选择颜色",
                Width = 280,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var wrap = new WrapPanel { Margin = new Thickness(10) };

            foreach (var c in _colorPool)
            {
                var b = new Button
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(4),
                    Background = new SolidColorBrush(c),
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(1)
                };

                b.Click += (_, __) =>
                {
                    string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    onSelected(hex);
                    win.Close();
                };

                wrap.Children.Add(b);
            }

            win.Content = wrap;
            win.ShowDialog();
        }
    }
}