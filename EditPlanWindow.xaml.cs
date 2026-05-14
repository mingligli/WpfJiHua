using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

            // 初始化时间下拉框
            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += 30)
                    cbTime.Items.Add($"{h:D2}:{m:D2}");

            cbTime.SelectedIndex = 18;
            RefreshList();
            UpdateButtonState();
        }

        // 刷新列表 - 直接绑定 PlanTask 对象
        private void RefreshList()
        {
            _plans = DataHelper.GetAllPlans();
            list.ItemsSource = null;  // 先清空
            list.ItemsSource = _plans;  // 直接绑定原始对象
        }

        // 清空编辑框
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

        // 更新按钮状态
        private void UpdateButtonState()
        {
            btnAdd.Visibility = _currentModifyPlan == null ? Visibility.Visible : Visibility.Collapsed;
            btnUpdate.Visibility = _currentModifyPlan != null ? Visibility.Visible : Visibility.Collapsed;
        }

        // 添加计划
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

        // 加载计划到编辑框（修改按钮点击事件）
        private void LoadToEdit(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取按钮所在的DataContext（即 PlanTask 对象）
                Button btn = sender as Button;
                if (btn == null) return;

                PlanTask selectedPlan = btn.DataContext as PlanTask;
                if (selectedPlan == null) return;

                // 保存当前要修改的计划
                _currentModifyPlan = selectedPlan;

                // 回填数据到界面
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

        // 保存修改
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

                // 直接修改 _currentModifyPlan 对象的属性
                _currentModifyPlan.PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.Text);
                _currentModifyPlan.Content = txt.Text.Trim();
                _currentModifyPlan.RepeatType = (RepeatType)cbo.SelectedIndex;
                _currentModifyPlan.Interval = i;

                // 保存到文件
                DataHelper.SavePlans(_plans);

                // 刷新列表显示
                RefreshList();
                ClearEdit();

                MessageBox.Show("修改成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"修改失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 删除计划
        private void Del(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null) return;

                PlanTask selectedPlan = btn.DataContext as PlanTask;
                if (selectedPlan == null) return;

                // 确认删除
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

                    // 如果删除的是当前正在修改的计划，清空编辑框
                    if (_currentModifyPlan == selectedPlan)
                    {
                        ClearEdit();
                    }

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