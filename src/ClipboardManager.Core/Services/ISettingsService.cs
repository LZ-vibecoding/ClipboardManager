using ClipboardManager.Core.Models;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 设置服务接口 — 读写配置
/// </summary>
public interface ISettingsService
{
    /// <summary>加载配置</summary>
    Task<AppSettings> LoadAsync();

    /// <summary>保存配置</summary>
    Task SaveAsync(AppSettings settings);
}
