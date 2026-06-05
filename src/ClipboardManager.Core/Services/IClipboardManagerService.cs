using ClipboardManager.Core.Models;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 剪贴板管理器总协调服务接口
/// </summary>
public interface IClipboardManagerService
{
    /// <summary>历史记录变化事件</summary>
    event EventHandler<IReadOnlyList<ClipboardItem>>? HistoryChanged;

    /// <summary>请求显示历史窗口事件</summary>
    event EventHandler? RequestShowHistory;

    /// <summary>初始化所有服务</summary>
    Task InitializeAsync();

    /// <summary>获取历史记录（支持搜索）</summary>
    Task<List<ClipboardItem>> GetHistoryAsync(string? search = null);

    /// <summary>粘贴指定条目到当前窗口（完整流程：设剪贴板 + 模拟 Ctrl+V）</summary>
    Task PasteAsync(ClipboardItem item);

    /// <summary>仅将条目内容写入系统剪贴板</summary>
    Task CopyToClipboardAsync(ClipboardItem item);

    /// <summary>设置条目固定状态</summary>
    Task PinAsync(string id, bool pinned);

    /// <summary>删除条目</summary>
    Task DeleteAsync(string id);
}
