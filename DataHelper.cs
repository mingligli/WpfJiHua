using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DesktopPlanWidget
{
    public static class DataHelper
    {
        public static string AppDataPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopPlanWidget");

        private static string DataFile => Path.Combine(AppDataPath, "plans.json");
        private static string ConfigFile => Path.Combine(AppDataPath, "config.json");

        static DataHelper()
        {
            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);
        }

        public static List<PlanTask> GetAllPlans()
        {
            try
            {
                return File.Exists(DataFile)
                    ? JsonSerializer.Deserialize<List<PlanTask>>(File.ReadAllText(DataFile))
                    : new List<PlanTask>();
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
                File.WriteAllText(DataFile, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static AppConfig GetConfig()
        {
            try
            {
                return File.Exists(ConfigFile)
                    ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile))
                    : new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));
            }
            catch { }
        }

        public static List<PlanTask> GetCustomDaysPlans(int days)
        {
            var today = DateTime.Now.Date;
            return GetAllPlans().FindAll(p =>
                p.PlanDate.Date >= today &&
                p.PlanDate.Date <= today.AddDays(days));
        }

        public static void RefreshNextRepeatTasks() { }

        public static bool BackupData(string savePath)
        {
            try
            {
                File.Copy(DataFile, savePath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RestoreData(string backupPath)
        {
            try
            {
                File.Copy(backupPath, DataFile, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class AppConfig
    {
        public int ShowDays { get; set; } = 7;
        public double WindowOpacity { get; set; } = 1.0;
        public bool AutoBackupEnabled { get; set; } = true; // 默认开启自动备份
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