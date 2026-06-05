namespace ClipboardManager.Core.Models;

/// <summary>
/// 剪贴板历史条目
/// </summary>
public class ClipboardItem
{
    /// <summary>主键 UUID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>内容 SHA256 哈希（去重用）</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>内容类型：文本或图片</summary>
    public ClipboardType Type { get; set; }

    /// <summary>文本内容（当 Type=Text 时）</summary>
    public string? TextContent { get; set; }

    /// <summary>图片文件相对路径（当 Type=Image 时）</summary>
    public string? ImagePath { get; set; }

    /// <summary>缩略图相对路径</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>是否固定（固定后不会被自动清理）</summary>
    public bool IsPinned { get; set; }

    /// <summary>首次复制时间 (北京时间)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最近使用时间 (北京时间)</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
