using ClipboardManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Extensions;

/// <summary>
/// DI 容器注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册剪贴板管理器所有核心服务
    /// </summary>
    public static IServiceCollection AddClipboardManager(this IServiceCollection services)
    {
        // 核心服务（单例 — 整个应用生命周期共享）
        services.AddSingleton<IClipboardManagerService, ClipboardManagerService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IClipboardMonitor, ClipboardMonitor>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<IPasteService, PasteService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<AutoStartService>();

        // 日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services;
    }
}
