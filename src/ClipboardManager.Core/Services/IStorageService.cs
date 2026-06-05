using ClipboardManager.Core.Models;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 数据存储服务接口
/// </summary>
public interface IStorageService : IDisposable
{
    /// <summary>初始化数据库（建表）</summary>
    Task InitializeAsync();

    /// <summary>插入新条目</summary>
    Task InsertAsync(ClipboardItem item);

    /// <summary>根据内容哈希查询条目</summary>
    Task<ClipboardItem?> GetByHashAsync(string hash, ClipboardType type);

    /// <summary>查询最近条目（支持搜索）</summary>
    Task<List<ClipboardItem>> GetRecentAsync(string? search = null, int limit = 200);

    /// <summary>更新条目的最近使用时间</summary>
    Task UpdateTimestampAsync(string id);

    /// <summary>设置固定状态</summary>
    Task SetPinnedAsync(string id, bool pinned);

    /// <summary>删除条目</summary>
    Task DeleteAsync(string id);

    /// <summary>获取总条目数</summary>
    Task<int> GetCountAsync();

    /// <summary>获取需要删除的候选条目（超限清理用）</summary>
    Task<List<ClipboardItem>> GetTrimCandidatesAsync(int count);

    /// <summary>根据 ID 获取条目</summary>
    Task<ClipboardItem?> GetByIdAsync(string id);
}
