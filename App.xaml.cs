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

        // 判断进程是否存在
        private bool IsProcessRunning()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

            return processes.Any(p => p.Id != currentProcess.Id);
        }
    }
}