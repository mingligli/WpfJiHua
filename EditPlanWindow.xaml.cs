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
            LoadData();
        }

        private void LoadData()
        {
            _plans = DataHelper.GetAllPlans();
            list.ItemsSource = _plans;
        }

        // ==============================
        // 🔥 添加：只加当前，不生成下一次
        // ==============================
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

            if (!int.TryParse(interval.Text, out int i) || i < 1)
            {
                MessageBox.Show("间隔必须≥1", "提示");
                return;
            }

            // 只添加原始任务
            _plans.Add(new PlanTask
            {
                PlanDate = dp.SelectedDate.Value,
                Content = txt.Text.Trim(),
                RepeatType = (RepeatType)cbo.SelectedIndex,
                Interval = i,
                IsAutoRepeat = false
            });

            DataHelper.SavePlans(_plans);
            LoadData();
            txt.Clear();

            MessageBox.Show("添加成功！\r\n下次计划将在 启动/次日8点 自动生成", "提示");
        }

        private void Del(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is PlanTask t)
            {
                _plans.RemoveAll(x =>
                    x.Content == t.Content
                    && x.RepeatType == t.RepeatType
                    && x.Interval == t.Interval
                );
                DataHelper.SavePlans(_plans);
                LoadData();
            }
        }
    }
}