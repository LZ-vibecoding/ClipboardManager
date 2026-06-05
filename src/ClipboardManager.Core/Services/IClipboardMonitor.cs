using ClipboardManager.Core.Models;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 剪贴板监控服务接口
/// </summary>
public interface IClipboardMonitor : IDisposable
{
    /// <summary>剪贴板数据就绪事件</summary>
    event EventHandler<ClipboardData>? ClipboardDataReady;

    /// <summary>开始监控</summary>
    void Start();

    /// <summary>停止监控</summary>
    void Stop();
}

/// <summary>
/// 剪贴板数据
/// </summary>
public class ClipboardData
{
    public ClipboardType Type { get; init; }
    public string Hash { get; init; } = string.Empty;
    public string? Text { get; init; }
    public byte[]? ImageBytes { get; init; }
}
