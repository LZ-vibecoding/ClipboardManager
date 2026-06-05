namespace ClipboardManager.Core.Models;

/// <summary>
/// 应用配置
/// </summary>
public class AppSettings
{
    /// <summary>最大条目数（默认 200）</summary>
    public int MaxItemCount { get; set; } = 200;

    /// <summary>是否开机自启</summary>
    public bool AutoStartWithWindows { get; set; }

    /// <summary>是否监控图片（可关闭）</summary>
    public bool MonitorImages { get; set; } = true;

    /// <summary>是否监控文本</summary>
    public bool MonitorText { get; set; } = true;

    /// <summary>缩略图尺寸（像素）</summary>
    public int ThumbnailSize { get; set; } = 120;

    /// <summary>图片存储质量 1-100</summary>
    public int ImageQuality { get; set; } = 85;
}
