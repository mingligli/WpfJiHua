using System;
using System.Collections.Generic;
using System.Windows;

namespace DesktopPlanWidget
{
    public partial class EditPlanWindow : Window
    {
        private List<PlanTask> _plans = new List<PlanTask>();

        public EditPlanWindow()
        {
            InitializeComponent();
            dp.SelectedDate = DateTime.Now;

            // 自动加载时间点 00:00 ~ 23:30
            for (int h = 0; h < 24; h++)
            {
                for (int m = 0; m < 60; m += 30)
                {
                    cbTime.Items.Add($"{h:D2}:{m:D2}");
                }
            }
            cbTime.SelectedIndex = 18; // 默认 09:00
            LoadData();
        }

        private void LoadData()
        {
            _plans = DataHelper.GetAllPlans();
            list.ItemsSource = _plans;
        }

        private void Add(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txt.Text))
            {
                MessageBox.Show("请输入计划内容！", "提示");
                return;
            }

            if (!dp.SelectedDate.HasValue)
            {
                MessageBox.Show("请选择日期！", "提示");
                return;
            }

            if (cbTime.SelectedItem == null)
            {
                MessageBox.Show("请选择时间！", "提示");
                return;
            }

            if (!int.TryParse(interval.Text, out int i) || i < 1)
            {
                MessageBox.Show("间隔必须≥1", "提示");
                return;
            }

            // 合并日期 + 选择的时间
            DateTime date = dp.SelectedDate.Value;
            string timeStr = cbTime.SelectedItem.ToString();
            DateTime planDate = date.Date + TimeSpan.Parse(timeStr);

            _plans.Add(new PlanTask
            {
                PlanDate = planDate,
                Content = txt.Text.Trim(),
                RepeatType = (RepeatType)cbo.SelectedIndex,
                Interval = i,
                IsAutoRepeat = false
            });

            DataHelper.SavePlans(_plans);
            LoadData();
            txt.Clear();
        }

        private void Del(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is PlanTask t)
            {
                _plans.RemoveAll(x =>
                    x.Content == t.Content &&
                    x.RepeatType == t.RepeatType &&
                    x.Interval == t.Interval
                );
                DataHelper.SavePlans(_plans);
                LoadData();
            }
        }
    }
}