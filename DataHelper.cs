using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace DesktopPlanWidget
{
    /// <summary>
    /// 数据操作助手类
    /// 负责计划任务、配置文件的读写、备份恢复、重复任务生成等核心数据处理
    /// </summary>
    public static class DataHelper
    {
        private static readonly ChineseLunisolarCalendar LunarCalendar = new ChineseLunisolarCalendar();

        /// <summary>
        /// 计划任务存储文件名
        /// </summary>
        private static string PlanFile => "plans.json";

        /// <summary>
        /// 应用配置存储文件名
        /// </summary>
        private static string ConfigFile => "config.json";

        /// <summary>
        /// 获取所有计划任务
        /// </summary>
        /// <returns>计划任务集合，文件不存在/异常时返回空集合</returns>
        public static List<PlanTask> GetAllPlans()
        {
            try
            {
                if (!File.Exists(PlanFile))
                    return new List<PlanTask>();

                var json = File.ReadAllText(PlanFile);
                return JsonSerializer.Deserialize<List<PlanTask>>(json) ?? new List<PlanTask>();
            }
            catch
            {
                return new List<PlanTask>();
            }
        }

        /// <summary>
        /// 保存所有计划任务到JSON文件
        /// </summary>
        /// <param name="list">要保存的计划任务集合</param>
        public static void SavePlans(List<PlanTask> list)
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(PlanFile, JsonSerializer.Serialize(list, opt));
            }
            catch { }
        }

        /// <summary>
        /// 获取应用配置
        /// </summary>
        /// <returns>应用配置对象，首次运行/异常时返回默认配置</returns>
        public static AppConfig GetConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new AppConfig { AutoStart = true };
                }

                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig { AutoStart = true };
            }
            catch
            {
                return new AppConfig { AutoStart = true };
            }
        }

        /// <summary>
        /// 保存应用配置到JSON文件
        /// </summary>
        /// <param name="cfg">要保存的配置对象</param>
        public static void SaveConfig(AppConfig cfg)
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, opt));
            }
            catch { }
        }

        /// <summary>
        /// 获取指定天数范围内的自定义计划任务
        /// </summary>
        /// <param name="days">要查询的天数范围</param>
        /// <returns>符合时间条件的计划任务集合</returns>
        public static List<PlanTask> GetCustomDaysPlans(int days)
        {
            var today = DateTime.Now.Date;
            var end = today.AddDays(days);
            return GetAllPlans().FindAll(p => p.PlanDate >= today && p.PlanDate <= end);
        }

        /// <summary>
        /// 备份所有数据（计划+配置）到指定路径
        /// </summary>
        /// <param name="savePath">备份文件保存路径</param>
        /// <returns>备份成功返回true，失败返回false</returns>
        public static bool BackupAll(string savePath)
        {
            try
            {
                var pack = new BackupPackage
                {
                    Plans = GetAllPlans(),
                    Config = GetConfig()
                };
                var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(savePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从备份文件恢复所有数据
        /// </summary>
        /// <param name="bakPath">备份文件路径</param>
        /// <returns>恢复成功返回true，失败返回false</returns>
        public static bool RestoreAll(string bakPath)
        {
            try
            {
                if (!File.Exists(bakPath)) return false;
                var json = File.ReadAllText(bakPath);
                var pack = JsonSerializer.Deserialize<BackupPackage>(json);

                if (pack != null)
                {
                    SavePlans(pack.Plans);
                    SaveConfig(pack.Config);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成下一次重复的计划任务（全天模式）
        /// 只关注日期，忽略时间部分。过期后自动生成下一个周期的新任务，并删除旧任务。
        /// </summary>
        public static void GenerateNextRepeatPlans()
        {
            var all = GetAllPlans();
            bool changed = false;
            var today = DateTime.Now.Date;

            for (int i = all.Count - 1; i >= 0; i--)
            {
                var p = all[i];
                if (p.RepeatType == RepeatType.None) continue;
                if (p.PlanDate.Date >= today) continue;

                DateTime next = p.PlanDate;
                switch (p.RepeatType)
                {
                    case RepeatType.Day:
                        next = next.AddDays(p.Interval);
                        break;
                    case RepeatType.Week:
                        next = next.AddDays(p.Interval * 7);
                        break;
                    case RepeatType.Month:
                        next = next.AddMonths(p.Interval);
                        break;
                    case RepeatType.Year:
                        next = next.AddYears(p.Interval);
                        break;
                    case RepeatType.YearLunar:
                        next = GetNextLunarDate(p.PlanDate, p.Interval);
                        break;
                }

                next = next.Date;

                if (next <= today)
                    next = today.AddDays(1);

                all.Add(new PlanTask
                {
                    PlanDate = next,
                    Content = p.Content,
                    RepeatType = p.RepeatType,
                    Interval = p.Interval,
                    IsAutoRepeat = true
                });
                all.RemoveAt(i);
                changed = true;
            }

            if (changed) SavePlans(all);
        }

        /// <summary>
        /// 根据给定的公历日期和农历年间隔，返回下一个农历日期对应的公历日期
        /// </summary>
        /// <param name="currentDate">当前公历日期（需对应农历生日）</param>
        /// <param name="yearsToAdd">农历年偏移量（通常为1）</param>
        /// <returns>下一个农历日期对应的公历零点</returns>
        private static DateTime GetNextLunarDate(DateTime currentDate, int yearsToAdd)
        {
            // 获取当前日期的农历信息
            int lunarYear = LunarCalendar.GetYear(currentDate);
            int lunarMonth = LunarCalendar.GetMonth(currentDate);      // 1-12 正常，13 表示闰月
            int lunarDay = LunarCalendar.GetDayOfMonth(currentDate);
            int leapMonth = LunarCalendar.GetLeapMonth(lunarYear);      // 获取闰月，无闰月返回0

            // 解析当前月份信息
            int actualMonth = lunarMonth;
            bool isLeap = false;
            if (lunarMonth == 13)
            {
                isLeap = true;
                actualMonth = leapMonth;                                // 闰月的实际月份值
            }
            else
            {
                // 处理闰月影响：如果当前月份大于闰月，则实际月份需要减1
                if (leapMonth > 0 && lunarMonth > leapMonth)
                    actualMonth = lunarMonth - 1;
            }

            // 计算目标农历年
            int targetLunarYear = lunarYear + yearsToAdd;
            int targetLeapMonth = LunarCalendar.GetLeapMonth(targetLunarYear);
            int targetMonth = actualMonth;
            bool targetIsLeap = false;

            // 处理目标闰月
            if (isLeap)
            {
                // 如果原日期是闰月，检查目标年是否有相同的闰月
                if (targetLeapMonth == actualMonth)
                {
                    targetIsLeap = true;
                    targetMonth = 13;   // 标记为闰月（用13表示）
                }
                // 否则降级为平月，targetMonth保持actualMonth即可
            }
            else
            {
                // 处理平月受闰月影响的情况
                if (targetLeapMonth > 0 && actualMonth >= targetLeapMonth)
                    targetMonth = actualMonth + 1;
                else
                    targetMonth = actualMonth;
            }

            // 获取目标农历月份的天数，处理日期溢出
            int daysInMonth;
            if (targetIsLeap)
                daysInMonth = LunarCalendar.GetDaysInMonth(targetLunarYear, targetMonth);
            else
                daysInMonth = LunarCalendar.GetDaysInMonth(targetLunarYear, targetMonth);
            int safeDay = Math.Min(lunarDay, daysInMonth);

            // 转换为公历
            try
            {
                return LunarCalendar.ToDateTime(targetLunarYear, targetMonth, safeDay, 0, 0, 0, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                // 最后的保险：如果转换失败，返回当前日期加一年（公历）
                return currentDate.AddYears(yearsToAdd).Date;
            }
        }
    }

    /// <summary>
    /// 备份数据包类
    /// 用于统一封装计划任务和应用配置，实现一键备份/恢复
    /// </summary>
    public class BackupPackage
    {
        public List<PlanTask> Plans { get; set; } = new List<PlanTask>();
        public AppConfig Config { get; set; } = new AppConfig();
    }

    /// <summary>
    /// 应用配置实体类
    /// 存储界面显示、样式、开机自启等配置项
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 默认显示天数
        /// </summary>
        public int ShowDays { get; set; } = 7;
        /// <summary>
        /// 文本透明度
        /// </summary>
        public double TextOpacity { get; set; } = 1.0;
        /// <summary>
        /// 标题颜色（十六进制）
        /// </summary>
        public string TitleColor { get; set; } = "#FFFFFF";
        /// <summary>
        /// 日期颜色（十六进制）
        /// </summary>
        public string DateColor { get; set; } = "#FFCC00";
        /// <summary>
        /// 内容颜色（十六进制）
        /// </summary>
        public string ContentColor { get; set; } = "#FFFFFF";
        /// <summary>
        /// 是否开机自启
        /// </summary>
        public bool AutoStart { get; set; } = true;
    }

    /// <summary>
    /// 计划重复类型枚举
    /// </summary>
    public enum RepeatType
    {
        /// <summary>
        /// 不重复
        /// </summary>
        None,
        /// <summary>
        /// 按天重复
        /// </summary>
        Day,
        /// <summary>
        /// 按周重复
        /// </summary>
        Week,
        /// <summary>
        /// 按月重复
        /// </summary>
        Month,
        /// <summary>
        /// 按年重复
        /// </summary>
        Year,
        /// <summary>
        /// 按农历年重复
        /// </summary>
        YearLunar
    }

    /// <summary>
    /// 计划任务实体类
    /// 存储单个计划的所有属性信息
    /// </summary>
    public class PlanTask
    {
        /// <summary>
        /// 计划执行日期
        /// </summary>
        public DateTime PlanDate { get; set; }
        /// <summary>
        /// 计划内容
        /// </summary>
        public string Content { get; set; } = "";
        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsFinish { get; set; }
        /// <summary>
        /// 重复类型
        /// </summary>
        public RepeatType RepeatType { get; set; }
        /// <summary>
        /// 重复间隔（对应天/周/月/年）
        /// </summary>
        public int Interval { get; set; }
        /// <summary>
        /// 是否为自动生成的重复任务
        /// </summary>
        public bool IsAutoRepeat { get; set; }
    }
}