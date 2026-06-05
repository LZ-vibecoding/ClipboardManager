using System.Data;
using ClipboardManager.Core.Data;
using ClipboardManager.Core.Models;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// SQLite 数据存储服务 — 使用 Dapper 执行 CRUD 操作
///
/// 所有数据库操作都通过 DatabaseContext 创建新连接，
/// SQLite 连接池会自动复用底层连接。
/// </summary>
public class StorageService : IStorageService
{
    private readonly DatabaseContext _db;
    private readonly ILogger<StorageService> _logger;

    public StorageService(ILogger<StorageService> logger)
    {
        // 启用 Dapper 蛇形命名映射（updated_at → UpdatedAt）
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        _db = new DatabaseContext();
        _logger = logger;
    }

    /// <summary>
    /// 初始化数据库（建表、索引、PRAGMA）
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("数据库初始化中...");
        await _db.InitializeAsync();
        _logger.LogInformation("数据库初始化完成: {Path}", _db.DatabasePath);
    }

    /// <summary>
    /// 插入新条目
    /// </summary>
    public async Task InsertAsync(ClipboardItem item)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO clipboard_items
                (id, content_hash, type, text_content, image_path, thumbnail_path,
                 is_pinned, created_at, updated_at)
            VALUES
                (@Id, @ContentHash, @Type, @TextContent, @ImagePath, @ThumbnailPath,
                 @IsPinned, @CreatedAt, @UpdatedAt)
            """, item);
    }

    /// <summary>
    /// 根据内容哈希和类型查询条目（用于去重）
    /// </summary>
    public async Task<ClipboardItem?> GetByHashAsync(string hash, ClipboardType type)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ClipboardItem>(
            "SELECT * FROM clipboard_items WHERE content_hash = @Hash AND type = @Type",
            new { Hash = hash, Type = (int)type });
    }

    /// <summary>
    /// 获取最近条目列表（支持文本搜索）
    ///
    /// 排序规则：固定项优先 → 按更新时间倒序
    /// </summary>
    public async Task<List<ClipboardItem>> GetRecentAsync(string? search = null, int limit = 200)
    {
        using var conn = _db.CreateConnection();

        if (string.IsNullOrWhiteSpace(search))
        {
            var items = await conn.QueryAsync<ClipboardItem>(
                "SELECT * FROM clipboard_items ORDER BY is_pinned DESC, updated_at DESC LIMIT @Limit",
                new { Limit = limit });
            return items.AsList();
        }

        var searched = await conn.QueryAsync<ClipboardItem>(
            "SELECT * FROM clipboard_items WHERE text_content LIKE @Pattern " +
            "ORDER BY is_pinned DESC, updated_at DESC LIMIT @Limit",
            new { Pattern = $"%{search}%", Limit = limit });
        return searched.AsList();
    }

    /// <summary>
    /// 更新条目最近使用时间（条目"浮"到列表顶部）
    /// </summary>
    public async Task UpdateTimestampAsync(string id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE clipboard_items SET updated_at = @Now WHERE id = @Id",
            new { Id = id, Now = DateTime.Now });
    }

    /// <summary>
    /// 设置条目的固定/取消固定状态
    /// </summary>
    public async Task SetPinnedAsync(string id, bool pinned)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE clipboard_items SET is_pinned = @Pinned WHERE id = @Id",
            new { Id = id, Pinned = pinned ? 1 : 0 });
    }

    /// <summary>
    /// 删除条目
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM clipboard_items WHERE id = @Id",
            new { Id = id });
    }

    /// <summary>
    /// 获取当前总条目数
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM clipboard_items");
    }

    /// <summary>
    /// 获取需要清理的候选条目（按时间正序取最旧的 N 条非固定条目）
    /// </summary>
    public async Task<List<ClipboardItem>> GetTrimCandidatesAsync(int count)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<ClipboardItem>(
            "SELECT * FROM clipboard_items WHERE is_pinned = 0 " +
            "ORDER BY updated_at ASC LIMIT @Count",
            new { Count = count });
        return items.AsList();
    }

    /// <summary>
    /// 根据 ID 获取条目
    /// </summary>
    public async Task<ClipboardItem?> GetByIdAsync(string id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ClipboardItem>(
            "SELECT * FROM clipboard_items WHERE id = @Id",
            new { Id = id });
    }

    public void Dispose()
    {
    }
}
