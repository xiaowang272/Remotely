using Remotely.Shared.Utilities;
using System;
using System.Diagnostics;
using System.IO;

namespace Remotely.Agent.Services;

public interface IUninstaller
{
    void UninstallAgent();
}

public class Uninstaller : IUninstaller
{
    public void UninstallAgent()
    {
        if (EnvironmentHelper.IsWindows)
        {
            // 停止并删除服务
            Process.Start("cmd.exe", "/c sc stop svc");
            Process.Start("cmd.exe", "/c sc delete svc");

            // 删除注册表卸载项
            var view = Environment.Is64BitOperatingSystem ?
                "/reg:64" :
                "/reg:32";

            Process.Start("cmd.exe", @$"/c REG DELETE HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\svc /f {view}");

            // 清理日志目录
            Process.Start("cmd.exe", @"/c rd /s /q ""C:\Windows\Logs\svc""");
            
            // 清理更新临时文件
            Process.Start("cmd.exe", @"/c del /f /q ""C:\Windows\svcUpdate.zip""");
            Process.Start("cmd.exe", @"/c del /f /q ""C:\Windows\Install-svc.ps1""");

            // 删除安装目录（包含文件传输目录 C:\Windows\svc\Shared）
            Process.Start("cmd.exe", @"/c timeout 5 & rd /s /q ""C:\Windows\svc""");
        }
        Environment.Exit(0);
    }
}
