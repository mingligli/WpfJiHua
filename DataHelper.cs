using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DesktopPlanWidget
{
    public static class DataHelper
    {
        private static string PlanFile => "plans.json";
        private static string ConfigFile => "config.json";

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

        public static void SavePlans(List<PlanTask> list)
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(PlanFile, JsonSerializer.Serialize(list, opt));
            }
            catch { }
        }

        public static AppConfig GetConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    // 首次运行：默认开机自启开启
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

        public static void SaveConfig(AppConfig cfg)
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, opt));
            }
            catch { }
        }

        public static List<PlanTask> GetCustomDaysPlans(int days)
        {
            var today = DateTime.Now.Date;
            var end = today.AddDays(days);
            return GetAllPlans().FindAll(p => p.PlanDate >= today && p.PlanDate <= end);
        }

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

        public static void GenerateNextRepeatPlans()
        {
            var all = GetAllPlans();
            bool changed = false;
            var now = DateTime.Now;

            for (int i = all.Count - 1; i >= 0; i--)
            {
                var p = all[i];
                if (p.RepeatType == RepeatType.None) continue;
                if (p.PlanDate > now) continue;

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

                if (next <= now) next = now.AddMinutes(1);
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
    }

    public class BackupPackage
    {
        public List<PlanTask> Plans { get; set; } = new List<PlanTask>();
        public AppConfig Config { get; set; } = new AppConfig();
    }

    public class AppConfig
    {
        public int ShowDays { get; set; } = 7;
        public double TextOpacity { get; set; } = 1.0;
        public string TitleColor { get; set; } = "#FFFFFF";
        public string DateColor { get; set; } = "#FFCC00";
        public string ContentColor { get; set; } = "#FFFFFF";
        // 新增：开机自启配置
        public bool AutoStart { get; set; } = true;
    }

    public enum RepeatType { None, Day, Week, Month, Year, YearLunar }

    public class PlanTask
    {
        public DateTime PlanDate { get; set; }
        public string Content { get; set; } = "";
        public bool IsFinish { get; set; }
        public RepeatType RepeatType { get; set; }
        public int Interval { get; set; }
        public bool IsAutoRepeat { get; set; }
    }
}