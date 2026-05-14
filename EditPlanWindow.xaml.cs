using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DesktopPlanWidget
{
    public partial class EditPlanWindow : Window
    {
        private List<PlanTask> _plans;
        private PlanTask _currentModifyPlan;

        public EditPlanWindow()
        {
            InitializeComponent();
            dp.SelectedDate = DateTime.Now;

            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += 30)
                    cbTime.Items.Add($"{h:D2}:{m:D2}");

            cbTime.SelectedIndex = 18;
            RefreshList();
            UpdateButtonState();
        }

        private void RefreshList()
        {
            _plans = DataHelper.GetAllPlans();

            // 给每一条数据加上 RepeatDesc 文字
            var displayList = _plans.Select(p => new
            {
                p.PlanDate,
                p.Content,
                RepeatDesc = GetRepeatText(p), // 一定显示
                p.RepeatType,
                p.Interval
            }).ToList();

            list.ItemsSource = displayList;
        }

        private string GetRepeatText(PlanTask p)
        {
            return p.RepeatType switch
            {
                RepeatType.None => "一次性",
                RepeatType.Day => $"每{p.Interval}天",
                RepeatType.Week => $"每{p.Interval}周",
                RepeatType.Month => $"每{p.Interval}月",
                RepeatType.Year => $"每{p.Interval}年",
                RepeatType.YearLunar => $"每{p.Interval}年(农历)",
                _ => "未知"
            };
        }

        private void ClearEdit()
        {
            txt.Clear();
            interval.Text = "1";
            cbo.SelectedIndex = 0;
            _currentModifyPlan = null;
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            btnAdd.Visibility = _currentModifyPlan == null ? Visibility.Visible : Visibility.Collapsed;
            btnUpdate.Visibility = _currentModifyPlan != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Add(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(interval.Text, out int i) || i < 1)
            { MessageBox.Show("间隔必须≥1"); return; }

            _plans.Add(new PlanTask
            {
                PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.SelectedItem.ToString()),
                Content = txt.Text.Trim(),
                RepeatType = (RepeatType)cbo.SelectedIndex,
                Interval = i
            });

            DataHelper.SavePlans(_plans);
            ClearEdit();
            RefreshList();
        }

        private void LoadToEdit(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var item = btn?.DataContext;
            if (item == null) return;

            DateTime planDate = (DateTime)item.GetType().GetProperty("PlanDate").GetValue(item);
            string content = (string)item.GetType().GetProperty("Content").GetValue(item);
            RepeatType type = (RepeatType)item.GetType().GetProperty("RepeatType").GetValue(item);
            int interval = (int)item.GetType().GetProperty("Interval").GetValue(item);

            _currentModifyPlan = _plans.FirstOrDefault(p =>
                p.PlanDate == planDate &&
                p.Content == content &&
                p.RepeatType == type &&
                p.Interval == interval);

            if (_currentModifyPlan == null) return;

            dp.SelectedDate = _currentModifyPlan.PlanDate.Date;
            cbTime.Text = _currentModifyPlan.PlanDate.ToString("HH:mm");
            txt.Text = _currentModifyPlan.Content;
            cbo.SelectedIndex = (int)_currentModifyPlan.RepeatType;
            this.interval.Text = _currentModifyPlan.Interval.ToString();
            UpdateButtonState();
        }

        private void Update(object sender, RoutedEventArgs e)
        {
            if (_currentModifyPlan == null) { MessageBox.Show("请选择计划"); return; }

            _currentModifyPlan.PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.SelectedItem.ToString());
            _currentModifyPlan.Content = txt.Text.Trim();
            _currentModifyPlan.RepeatType = (RepeatType)cbo.SelectedIndex;
            _currentModifyPlan.Interval = int.Parse(interval.Text);

            DataHelper.SavePlans(_plans);
            ClearEdit();
            RefreshList();
            MessageBox.Show("修改成功！");
        }

        private void Del(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var item = btn?.DataContext;
            if (item == null) return;

            DateTime planDate = (DateTime)item.GetType().GetProperty("PlanDate").GetValue(item);
            string content = (string)item.GetType().GetProperty("Content").GetValue(item);

            _plans.RemoveAll(p => p.PlanDate == planDate && p.Content == content);
            DataHelper.SavePlans(_plans);
            RefreshList();
        }
    }
}