using System;
using System.Collections.Generic;
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
                // 文件不存在，返回空列表
                if (!File.Exists(PlanFile))
                    return new List<PlanTask>();

                // 读取JSON文件并反序列化为计划任务集合
                var json = File.ReadAllText(PlanFile);
                return JsonSerializer.Deserialize<List<PlanTask>>(json) ?? new List<PlanTask>();
            }
            catch
            {
                // 发生异常时返回空列表，保证程序不崩溃
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
                // 设置格式化缩进，让JSON文件更易读
                var opt = new JsonSerializerOptions { WriteIndented = true };
                // 序列化并写入文件
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
                // 配置文件不存在，返回默认配置（开启开机自启）
                if (!File.Exists(ConfigFile))
                {
                    return new AppConfig { AutoStart = true };
                }

                // 读取并反序列化配置
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig { AutoStart = true };
            }
            catch
            {
                // 异常时返回默认配置
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
            // 筛选出今日到N天后的所有计划
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
                // 封装备份数据对象
                var pack = new BackupPackage
                {
                    Plans = GetAllPlans(),
                    Config = GetConfig()
                };
                // 序列化并写入备份文件
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
                // 备份文件不存在，直接返回失败
                if (!File.Exists(bakPath)) return false;
                var json = File.ReadAllText(bakPath);
                var pack = JsonSerializer.Deserialize<BackupPackage>(json);

                // 数据不为空则覆盖保存本地数据
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
            var today = DateTime.Now.Date;   // 今天的零点（忽略时间）

            // 倒序遍历，避免删除元素导致索引异常
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var p = all[i];
                // 无重复类型 或 计划日期（仅日期）大于今天 → 未过期，跳过
                if (p.RepeatType == RepeatType.None) continue;
                if (p.PlanDate.Date > today) continue;   // 改为日期比较

                // 根据重复类型计算下一次执行时间（先按原时刻计算，再归一化到零点）
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
                        next = next.AddYears(p.Interval);
                        break;
                }
                // 全天模式：只保留日期部分（时间归零）
                next = next.Date;

                // 如果下一次日期 ≤ 今天（例如因为周期为0或时钟错误），则强制设为明天
                if (next <= today)
                    next = today.AddDays(1);

                // 添加新的重复计划（自动生成，时间为当天零点）
                all.Add(new PlanTask
                {
                    PlanDate = next,
                    Content = p.Content,
                    RepeatType = p.RepeatType,
                    Interval = p.Interval,
                    IsAutoRepeat = true
                });
                // 移除已过期的旧计划
                all.RemoveAt(i);
                changed = true;
            }

            if (changed) SavePlans(all);
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