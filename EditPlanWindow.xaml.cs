using System;
using System.Collections.Generic;
using System.Windows;

namespace DesktopPlanWidget
{
    public partial class EditPlanWindow : Window
    {
        private List<PlanTask> _plans = new List<PlanTask>();
        private PlanTask _currentModifyPlan = null; // 正在修改的计划

        public EditPlanWindow()
        {
            InitializeComponent();
            dp.SelectedDate = DateTime.Now;

            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += 30)
                    cbTime.Items.Add($"{h:D2}:{m:D2}");

            cbTime.SelectedIndex = 18;
            LoadData();
            UpdateButtonState(); // 初始化按钮状态
        }

        private void LoadData()
        {
            _plans = DataHelper.GetAllPlans();
            list.ItemsSource = _plans;
        }

        // 清空编辑区
        private void ClearEdit()
        {
            txt.Clear();
            interval.Text = "1";
            cbo.SelectedIndex = 0;
            _currentModifyPlan = null;
            UpdateButtonState(); // 改回添加状态
        }

        // 切换按钮状态：添加 / 修改
        private void UpdateButtonState()
        {
            btnAdd.Visibility = _currentModifyPlan == null ? Visibility.Visible : Visibility.Collapsed;
            btnUpdate.Visibility = _currentModifyPlan != null ? Visibility.Visible : Visibility.Collapsed;
        }

        // 添加计划
        private void Add(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txt.Text)) { MessageBox.Show("请输入内容"); return; }
            if (!dp.SelectedDate.HasValue) { MessageBox.Show("请选择日期"); return; }
            if (cbTime.SelectedItem == null) { MessageBox.Show("请选择时间"); return; }
            if (!int.TryParse(interval.Text, out int i) || i < 1) { MessageBox.Show("间隔必须≥1"); return; }

            DateTime planDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.SelectedItem.ToString());
            _plans.Add(new PlanTask
            {
                PlanDate = planDate,
                Content = txt.Text.Trim(),
                RepeatType = (RepeatType)cbo.SelectedIndex,
                Interval = i,
                IsAutoRepeat = false
            });

            DataHelper.SavePlans(_plans);
            ClearEdit();
            LoadData();
        }

        // 加载到编辑区（修改）
        private void LoadToEdit(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null || !(btn.Tag is PlanTask plan)) return;

            _currentModifyPlan = plan;

            dp.SelectedDate = plan.PlanDate.Date;
            cbTime.Text = plan.PlanDate.ToString("HH:mm");
            txt.Text = plan.Content;
            cbo.SelectedIndex = (int)plan.RepeatType;
            interval.Text = plan.Interval.ToString();

            UpdateButtonState(); // 切换到修改状态
        }

        // 保存修改（不新增，只更新）
        private void Update(object sender, RoutedEventArgs e)
        {
            if (_currentModifyPlan == null) { MessageBox.Show("请先选择计划"); return; }
            if (string.IsNullOrWhiteSpace(txt.Text)) { MessageBox.Show("内容不能为空"); return; }
            if (!dp.SelectedDate.HasValue || cbTime.SelectedItem == null) { MessageBox.Show("日期时间不能为空"); return; }
            if (!int.TryParse(interval.Text, out int i) || i < 1) { MessageBox.Show("间隔≥1"); return; }

            _currentModifyPlan.PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.SelectedItem.ToString());
            _currentModifyPlan.Content = txt.Text.Trim();
            _currentModifyPlan.RepeatType = (RepeatType)cbo.SelectedIndex;
            _currentModifyPlan.Interval = i;

            DataHelper.SavePlans(_plans);
            ClearEdit();
            LoadData();
            MessageBox.Show("修改成功！");
        }

        // 删除
        private void Del(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null || !(btn.Tag is PlanTask plan)) return;

            _plans.Remove(plan);
            DataHelper.SavePlans(_plans);
            ClearEdit();
            LoadData();
        }
    }
}