using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClipboardManager.Core.Helpers;
using ClipboardManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 剪贴板监控服务 — 使用 Win32 SetClipboardViewer API
///
/// 功能：
/// - 注册到剪贴板查看器链，实时接收剪贴板变化通知
/// - 三重去重：序列号 + 防抖 + SHA256 内容哈希
/// - 支持文本和图片内容的读取（在 STA 线程上操作剪贴板）
/// </summary>
public class ClipboardMonitor : IClipboardMonitor
{
    private readonly ILogger<ClipboardMonitor> _logger;

    private HwndSource? _hwndSource;
    private IntPtr _nextViewerHandle;
    private int _lastSeqNum;
    private CancellationTokenSource? _debounceCts;
    private Dispatcher? _dispatcher;
    private bool _isRunning;

    private const int DebounceMs = 80;

    // 事件
    private EventHandler<ClipboardData>? _clipboardDataReady;
    public event EventHandler<ClipboardData>? ClipboardDataReady
    {
        add => _clipboardDataReady += value;
        remove => _clipboardDataReady -= value;
    }

    public ClipboardMonitor(ILogger<ClipboardMonitor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 开始监控剪贴板（必须在 STA 线程上调用）
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        // 保存当前线程的 Dispatcher（WPF 主线程 = STA）
        _dispatcher = Dispatcher.CurrentDispatcher;

        _logger.LogInformation("注册剪贴板查看器...");

        // 创建隐藏窗口用于接收 Win32 消息
        var parameters = new HwndSourceParameters("ClipboardMonitor")
        {
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP (隐藏窗口)
            ParentWindow = IntPtr.Zero,
            PositionX = 0,
            PositionY = 0,
            Width = 0,
            Height = 0
        };
        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        // 注册到剪贴板查看器链
        _nextViewerHandle = NativeMethods.SetClipboardViewer(_hwndSource.Handle);

        _logger.LogInformation("剪贴板监控已启动");
    }

    /// <summary>
    /// 窗口消息处理
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_DRAWCLIPBOARD:
                HandleClipboardChange();
                // 传递消息到下一个查看器
                if (_nextViewerHandle != IntPtr.Zero)
                {
                    NativeMethods.SendMessage(_nextViewerHandle, msg, wParam, lParam);
                }
                handled = true;
                break;

            case NativeMethods.WM_CHANGECBCHAIN:
                if (wParam == _nextViewerHandle)
                {
                    _nextViewerHandle = lParam;
                }
                else if (_nextViewerHandle != IntPtr.Zero)
                {
                    NativeMethods.SendMessage(_nextViewerHandle, msg, wParam, lParam);
                }
                handled = true;
                break;

            case NativeMethods.WM_DESTROY:
                UnregisterViewer();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 处理剪贴板变化 — 三重去重
    /// </summary>
    private void HandleClipboardChange()
    {
        // 层级1: 序列号去重
        var seqNum = NativeMethods.GetClipboardSequenceNumber();
        if (seqNum == _lastSeqNum) return;
        _lastSeqNum = seqNum;

        // 层级2: 防抖定时器
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(DebounceMs, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
            {
                // 通过 Dispatcher 回到 STA 线程再读取剪贴板
                _dispatcher?.BeginInvoke(DispatcherPriority.Normal,
                    () => ReadAndPublishClipboardData());
            }
        }, token);
    }

    /// <summary>
    /// 读取剪贴板内容并发布事件（在 STA 线程上执行）
    /// </summary>
    private void ReadAndPublishClipboardData()
    {
        try
        {
            // 尝试读取文本
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (string.IsNullOrEmpty(text)) return;

                var hash = HashHelper.ComputeTextHash(text);

                var data = new ClipboardData
                {
                    Type = ClipboardType.Text,
                    Hash = hash,
                    Text = text
                };

                _clipboardDataReady?.Invoke(this, data);
                _logger.LogDebug("捕获文本: {Length} 字符, Hash={Hash}", text.Length, hash[..8]);
                return;
            }

            // 尝试读取图片
            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image == null) return;

                // 将 BitmapSource 编码为 PNG 字节
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                var imageBytes = ms.ToArray();

                var hash = HashHelper.ComputeHash(imageBytes);

                var data = new ClipboardData
                {
                    Type = ClipboardType.Image,
                    Hash = hash,
                    ImageBytes = imageBytes
                };

                _clipboardDataReady?.Invoke(this, data);
                _logger.LogDebug("捕获图片: {Size} bytes, Hash={Hash}", imageBytes.Length, hash[..8]);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "读取剪贴板时被其他进程锁定，跳过本次");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取剪贴板时发生未预期错误");
        }
    }

    private void UnregisterViewer()
    {
        if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
        {
            NativeMethods.ChangeClipboardChain(_hwndSource.Handle, _nextViewerHandle);
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        UnregisterViewer();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    public void Dispose()
    {
        Stop();
        _hwndSource?.Dispose();
        _hwndSource = null;
    }
}
