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
                    return new AppConfig();

                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
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
            return GetAllPlans().FindAll(p => p.PlanDate.Date >= today && p.PlanDate.Date <= today.AddDays(days));
        }

        // 🔥 最简单、最稳定的备份恢复（不会再出错！）
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