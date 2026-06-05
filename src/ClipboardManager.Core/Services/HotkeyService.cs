using System.Windows.Interop;
using ClipboardManager.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 全局热键服务 — 使用 Win32 RegisterHotKey API
///
/// 默认热键: Ctrl + Shift + V
/// 使用隐藏 HwndSource 窗口接收 WM_HOTKEY 消息
/// </summary>
public class HotkeyService : IHotkeyService
{
    private readonly ILogger<HotkeyService> _logger;

    private HwndSource? _hwndSource;
    private bool _isRegistered;
    private const int HotkeyId = 9001;

    // 事件
    private EventHandler? _hotkeyPressed;
    public event EventHandler? HotkeyPressed
    {
        add => _hotkeyPressed += value;
        remove => _hotkeyPressed -= value;
    }

    public HotkeyService(ILogger<HotkeyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册全局热键
    /// </summary>
    /// <param name="modifiers">修饰键组合 (MOD_CONTROL|MOD_SHIFT)</param>
    /// <param name="virtualKey">虚拟键码 (0x56 = V)</param>
    public void Register(uint modifiers, uint virtualKey)
    {
        Unregister();

        // 创建隐藏窗口用于接收热键消息
        // 使用 HwndSourceParameters 指定 WS_POPUP + WS_VISIBLE 确保消息正常路由
        var parameters = new HwndSourceParameters("HotkeyWindow")
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

        // 注册热键 (MOD_NOREPEAT 防止按住时重复触发)
        var success = NativeMethods.RegisterHotKey(
            _hwndSource.Handle,
            HotkeyId,
            modifiers | NativeMethods.MOD_NOREPEAT,
            virtualKey);

        if (!success)
        {
            _logger.LogWarning("热键注册失败（可能与其他应用冲突），释放资源");
            _hwndSource.Dispose();
            _hwndSource = null;
            throw new InvalidOperationException(
                $"全局热键注册失败 (Modifiers={modifiers}, Key=0x{virtualKey:X2})，" +
                "请检查是否与其他软件的热键冲突");
        }

        _isRegistered = true;
        _logger.LogInformation("全局热键已注册: Ctrl+Shift+V (ID={Id})", HotkeyId);
    }

    /// <summary>
    /// 窗口消息处理
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && (int)wParam == HotkeyId)
        {
            _logger.LogDebug("全局热键触发");
            _hotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 注销全局热键
    /// </summary>
    public void Unregister()
    {
        if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero && _isRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _isRegistered = false;
            _logger.LogDebug("全局热键已注销");
        }
    }

    public void Dispose()
    {
        Unregister();
        _hwndSource?.Dispose();
        _hwndSource = null;
    }
}
