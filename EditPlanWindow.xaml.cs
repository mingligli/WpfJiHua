using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace DesktopPlanWidget
{
    // 包装类：用于显示 RepeatDesc，同时持有原始 PlanTask
    public class PlanDisplayItem : IComparable<PlanDisplayItem>
    {
        public DateTime PlanDate { get; set; }
        public string Content { get; set; }
        public string RepeatDesc { get; set; }
        public PlanTask SourceTask { get; set; }

        public int CompareTo(PlanDisplayItem other)
        {
            if (other == null) return 1;
            return this.PlanDate.CompareTo(other.PlanDate);
        }
    }

    public partial class EditPlanWindow : Window
    {
        private List<PlanTask> _plans;
        private PlanTask _currentModifyPlan;
        private ObservableCollection<PlanDisplayItem> _displayItems;

        public EditPlanWindow()
        {
            InitializeComponent();
            dp.SelectedDate = DateTime.Now;

            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += 30)
                    cbTime.Items.Add($"{h:D2}:{m:D2}");

            cbTime.SelectedIndex = 18;

            _displayItems = new ObservableCollection<PlanDisplayItem>();
            list.ItemsSource = _displayItems;

            RefreshList();
            UpdateButtonState();
        }

        private void RefreshList()
        {
            // 获取所有计划
            _plans = DataHelper.GetAllPlans();

            // 清空并重新填充 ObservableCollection
            _displayItems.Clear();

            // 按 PlanDate 排序后添加到集合
            var sortedPlans = _plans.OrderBy(p => p.PlanDate).ToList();

            foreach (var plan in sortedPlans)
            {
                _displayItems.Add(new PlanDisplayItem
                {
                    PlanDate = plan.PlanDate,
                    Content = plan.Content,
                    RepeatDesc = GetRepeatText(plan),
                    SourceTask = plan
                });
            }
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
            dp.SelectedDate = DateTime.Now;
            cbTime.SelectedIndex = 18;
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
            try
            {
                if (!int.TryParse(interval.Text, out int i) || i < 1)
                {
                    MessageBox.Show("间隔必须≥1的数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (dp.SelectedDate == null || string.IsNullOrWhiteSpace(cbTime.Text))
                {
                    MessageBox.Show("请选择日期和时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    MessageBox.Show("请输入计划内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PlanTask newPlan = new PlanTask
                {
                    PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.Text),
                    Content = txt.Text.Trim(),
                    RepeatType = (RepeatType)cbo.SelectedIndex,
                    Interval = i
                };

                _plans.Add(newPlan);
                DataHelper.SavePlans(_plans);
                ClearEdit();
                RefreshList();

                MessageBox.Show("添加成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadToEdit(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null) return;

                // 直接转成包装类，不用反射
                PlanDisplayItem item = btn.DataContext as PlanDisplayItem;
                if (item == null) return;

                _currentModifyPlan = item.SourceTask;

                dp.SelectedDate = _currentModifyPlan.PlanDate.Date;
                cbTime.Text = _currentModifyPlan.PlanDate.ToString("HH:mm");
                txt.Text = _currentModifyPlan.Content;
                cbo.SelectedIndex = (int)_currentModifyPlan.RepeatType;
                interval.Text = _currentModifyPlan.Interval.ToString();

                UpdateButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载修改失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModifyPlan == null)
                {
                    MessageBox.Show("请先选择要修改的计划", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (dp.SelectedDate == null || string.IsNullOrWhiteSpace(cbTime.Text))
                {
                    MessageBox.Show("日期和时间不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(interval.Text, out int i) || i < 1)
                {
                    MessageBox.Show("间隔必须≥1的数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    MessageBox.Show("请输入计划内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentModifyPlan.PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.Text);
                _currentModifyPlan.Content = txt.Text.Trim();
                _currentModifyPlan.RepeatType = (RepeatType)cbo.SelectedIndex;
                _currentModifyPlan.Interval = i;

                DataHelper.SavePlans(_plans);
                RefreshList();
                ClearEdit();

                MessageBox.Show("修改成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"修改失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Del(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null) return;

                // 直接转包装类，不用反射
                PlanDisplayItem item = btn.DataContext as PlanDisplayItem;
                if (item == null) return;

                PlanTask selectedPlan = item.SourceTask;

                MessageBoxResult result = MessageBox.Show(
                    $"确定要删除计划「{selectedPlan.Content}」吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _plans.Remove(selectedPlan);
                    DataHelper.SavePlans(_plans);
                    RefreshList();

                    if (_currentModifyPlan == selectedPlan)
                        ClearEdit();

                    MessageBox.Show("删除成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}