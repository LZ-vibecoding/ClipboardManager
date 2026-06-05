using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 开机自启管理 — 通过注册表 HKCU\...\Run 设置
///
/// 使用 HKCU (当前用户) 而不是 HKLM，不需要管理员权限。
/// 注册表路径: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
/// 键名: ClipboardManager
/// </summary>
public class AutoStartService
{
    private readonly ILogger<AutoStartService> _logger;

    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClipboardManager";

    /// <summary>当前可执行文件路径（用于注册表写入）</summary>
    private static string ExecutablePath
    {
        get
        {
            // 单文件发布模式：Environment.ProcessPath 返回实际 exe 路径
            // dotnet run 模式下 path 是 dotnet.exe，要排除
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) &&
                processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return processPath;
            }

            // 开发模式（dotnet run）：用程序集位置拼出路径
            var assemblyLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                if (assemblyLocation.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return assemblyLocation;
                // DLL 模式 → dotnet 启动
                return $"dotnet \"{assemblyLocation}\"";
            }

            // 极端降级：用 BaseDirectory 推测 exe 路径
            var baseDir = System.AppContext.BaseDirectory;
            var guessedExe = Path.Combine(baseDir, "ClipboardManager.Wpf.exe");
            if (File.Exists(guessedExe))
                return guessedExe;

            throw new InvalidOperationException("无法获取程序集路径");
        }
    }

    public AutoStartService(ILogger<AutoStartService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查是否已启用开机自启
    /// </summary>
    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            if (key == null) return false;

            var value = key.GetValue(ValueName) as string;
            return value?.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查开机自启状态时出错");
            return false;
        }
    }

    /// <summary>
    /// 设置开机自启
    /// </summary>
    public void SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RegistryKeyPath);

            if (key == null)
            {
                _logger.LogError("无法打开或创建注册表项: {Path}", RegistryKeyPath);
                return;
            }

            if (enable)
            {
                key.SetValue(ValueName, ExecutablePath, RegistryValueKind.String);
                _logger.LogInformation("开机自启已启用: {Path}", ExecutablePath);
            }
            else
            {
                if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName);
                }
                _logger.LogInformation("开机自启已禁用");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置开机自启时出错");
        }
    }
}
