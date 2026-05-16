using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace DesktopPlanWidget
{
    /// <summary>
    /// 计划显示包装类
    /// 作用：给UI界面显示使用，包含友好描述文字，同时持有原始的PlanTask数据
    /// </summary>
    public class PlanDisplayItem : IComparable<PlanDisplayItem>
    {
        /// <summary>
        /// 计划日期
        /// </summary>
        public DateTime PlanDate { get; set; }

        /// <summary>
        /// 计划内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 重复类型的友好显示文本（如：每1天、一次性）
        /// </summary>
        public string RepeatDesc { get; set; }

        /// <summary>
        /// 原始计划任务对象（用于修改、删除时操作真实数据）
        /// </summary>
        public PlanTask SourceTask { get; set; }

        /// <summary>
        /// 比较方法：按计划日期排序（早的排前面）
        /// </summary>
        public int CompareTo(PlanDisplayItem other)
        {
            if (other == null) return 1;
            return this.PlanDate.CompareTo(other.PlanDate);
        }
    }

    /// <summary>
    /// 计划编辑窗口（添加/修改/删除/列表展示）
    /// </summary>
    public partial class EditPlanWindow : Window
    {
        /// <summary>
        /// 本地缓存的所有计划列表
        /// </summary>
        private List<PlanTask> _plans;

        /// <summary>
        /// 当前正在修改的计划（为null时表示新增）
        /// </summary>
        private PlanTask _currentModifyPlan;

        /// <summary>
        /// UI绑定的显示列表（自动刷新界面）
        /// </summary>
        private ObservableCollection<PlanDisplayItem> _displayItems;

        public EditPlanWindow()
        {
            InitializeComponent();

            // 默认选中今天日期
            dp.SelectedDate = DateTime.Now;

            // 生成时间选择下拉框：00:00 ~ 23:30，每30分钟一个选项
            for (int h = 0; h < 24; h++)
                for (int m = 0; m < 60; m += 30)
                    cbTime.Items.Add($"{h:D2}:{m:D2}");

            // 默认选中 09:00（索引18）
            cbTime.SelectedIndex = 18;

            // 初始化UI绑定集合
            _displayItems = new ObservableCollection<PlanDisplayItem>();
            list.ItemsSource = _displayItems;

            // 加载计划列表并刷新界面
            RefreshList();
            // 更新按钮状态（添加/修改按钮切换）
            UpdateButtonState();
        }

        /// <summary>
        /// 刷新计划列表：从文件读取数据并展示到界面
        /// </summary>
        private void RefreshList()
        {
            // 从JSON文件获取所有计划
            _plans = DataHelper.GetAllPlans();

            // 清空界面显示列表
            _displayItems.Clear();

            // 按日期升序排序
            var sortedPlans = _plans.OrderBy(p => p.PlanDate).ToList();

            // 遍历转为显示项并添加到列表
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

        /// <summary>
        /// 根据计划的重复类型，返回界面显示的文字
        /// </summary>
        /// <param name="p">计划任务</param>
        /// <returns>重复类型显示文本</returns>
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

        /// <summary>
        /// 清空编辑区内容，恢复到新增状态
        /// </summary>
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

        /// <summary>
        /// 更新按钮显示状态
        /// 没有选中修改项时显示【添加】，选中时显示【修改】
        /// </summary>
        private void UpdateButtonState()
        {
            btnAdd.Visibility = _currentModifyPlan == null ? Visibility.Visible : Visibility.Collapsed;
            btnUpdate.Visibility = _currentModifyPlan != null ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 添加新计划按钮点击事件
        /// </summary>
        private void Add(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证重复间隔必须是≥1的数字
                if (!int.TryParse(interval.Text, out int i) || i < 1)
                {
                    MessageBox.Show("间隔必须≥1的数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证日期和时间不能为空
                if (dp.SelectedDate == null || string.IsNullOrWhiteSpace(cbTime.Text))
                {
                    MessageBox.Show("请选择日期和时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证计划内容不能为空
                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    MessageBox.Show("请输入计划内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 构建新计划对象
                PlanTask newPlan = new PlanTask
                {
                    PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.Text),
                    Content = txt.Text.Trim(),
                    RepeatType = (RepeatType)cbo.SelectedIndex,
                    Interval = i
                };

                // 添加到列表并保存到文件
                _plans.Add(newPlan);
                DataHelper.SavePlans(_plans);

                // 清空输入框并刷新列表
                ClearEdit();
                RefreshList();

                MessageBox.Show("添加成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载选中的计划到编辑区（修改前的加载）
        /// </summary>
        private void LoadToEdit(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null) return;

                // 获取按钮绑定的显示项
                PlanDisplayItem item = btn.DataContext as PlanDisplayItem;
                if (item == null) return;

                // 设置当前修改对象
                _currentModifyPlan = item.SourceTask;

                // 将选中计划信息填充到输入控件
                dp.SelectedDate = _currentModifyPlan.PlanDate.Date;
                cbTime.Text = _currentModifyPlan.PlanDate.ToString("HH:mm");
                txt.Text = _currentModifyPlan.Content;
                cbo.SelectedIndex = (int)_currentModifyPlan.RepeatType;
                interval.Text = _currentModifyPlan.Interval.ToString();

                // 切换为修改按钮状态
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载修改失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 修改并保存现有计划
        /// </summary>
        private void Update(object sender, RoutedEventArgs e)
        {
            try
            {
                // 必须先选中要修改的计划
                if (_currentModifyPlan == null)
                {
                    MessageBox.Show("请先选择要修改的计划", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证日期时间
                if (dp.SelectedDate == null || string.IsNullOrWhiteSpace(cbTime.Text))
                {
                    MessageBox.Show("日期和时间不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证间隔数字
                if (!int.TryParse(interval.Text, out int i) || i < 1)
                {
                    MessageBox.Show("间隔必须≥1的数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证内容不能为空
                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    MessageBox.Show("请输入计划内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 更新当前计划的所有字段
                _currentModifyPlan.PlanDate = dp.SelectedDate.Value.Date + TimeSpan.Parse(cbTime.Text);
                _currentModifyPlan.Content = txt.Text.Trim();
                _currentModifyPlan.RepeatType = (RepeatType)cbo.SelectedIndex;
                _currentModifyPlan.Interval = i;

                // 保存到文件并刷新界面
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

        /// <summary>
        /// 删除选中的计划
        /// </summary>
        private void Del(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null) return;

                // 获取绑定的计划显示项
                PlanDisplayItem item = btn.DataContext as PlanDisplayItem;
                if (item == null) return;

                PlanTask selectedPlan = item.SourceTask;

                // 弹出删除确认框
                MessageBoxResult result = MessageBox.Show(
                    $"确定要删除计划「{selectedPlan.Content}」吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 从列表移除并保存
                    _plans.Remove(selectedPlan);
                    DataHelper.SavePlans(_plans);
                    RefreshList();

                    // 如果删除的是当前正在修改的项，则清空编辑区
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