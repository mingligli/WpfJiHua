using System;
using System.Linq;
using System.Windows;

namespace DesktopPlanWidget
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 检测进程是否已经运行
            if (IsProcessRunning())
            {
                MessageBox.Show("日程提醒工具已经在运行中，请勿重复启动！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // 退出程序
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// 判断是否已有同名进程在运行（用于实现单实例启动的简单检测）。
        /// </summary>
        /// <remarks>      
        private static bool IsProcessRunning()
        {
            // 获取当前进程（用于比较进程 Id 和进程名）
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();

            // 根据当前进程的进程名查找系统中所有同名进程
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

            // 如果存在不同 Id 的同名进程，则说明已有另一个实例运行
            return processes.Any(p => p.Id != currentProcess.Id);
        }
    }
}