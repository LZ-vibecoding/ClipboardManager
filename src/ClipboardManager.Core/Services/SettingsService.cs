using System.Text.Json;
using ClipboardManager.Core.Data;
using ClipboardManager.Core.Models;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 设置服务 — 从 SQLite 的 settings 表读写配置
///
/// 所有设置以 JSON 格式存储在 settings 表中（key="app_config"）。
/// 这样做的好处：
/// - 新增设置项无需改表结构
/// - 默认值与反序列化默认值对齐
/// - 易于迁移和备份
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly DatabaseContext _db;
    private readonly ILogger<SettingsService> _logger;

    private const string ConfigKey = "app_config";

    // 内存缓存，避免每次读取都查数据库
    private AppSettings? _cached;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _db = new DatabaseContext();
        _logger = logger;
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    public async Task<AppSettings> LoadAsync()
    {
        if (_cached != null) return _cached;

        try
        {
            using var conn = _db.CreateConnection();
            var json = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT value FROM settings WHERE key = @Key",
                new { Key = ConfigKey });

            if (json != null)
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _cached = settings;
                    _logger.LogDebug("配置已加载: MaxItems={MaxItems}", settings.MaxItemCount);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载配置时出错，使用默认配置");
        }

        _cached = new AppSettings();
        return _cached;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public async Task SaveAsync(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings);

            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync("""
                INSERT INTO settings (key, value) VALUES (@Key, @Value)
                ON CONFLICT(key) DO UPDATE SET value = @Value
                """, new { Key = ConfigKey, Value = json });

            _cached = settings;
            _logger.LogInformation("配置已保存: MaxItems={MaxItems}", settings.MaxItemCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置时出错");
            throw;
        }
    }
}
