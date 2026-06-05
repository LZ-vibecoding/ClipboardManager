using System.Runtime.InteropServices;

namespace ClipboardManager.Core.Helpers;

/// <summary>
/// Win32 API 声明 — 剪贴板监控和全局热键相关
/// </summary>
public static class NativeMethods
{
    // ─── 剪贴板查看器链 ───

    /// <summary>
    /// 注册剪贴板查看器窗口（添加到查看器链）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

    /// <summary>
    /// 从剪贴板查看器链中移除窗口
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

    /// <summary>
    /// 发送消息（用于传递到下一个查看器）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // ─── 剪贴板序列号 ───

    /// <summary>
    /// 获取剪贴板序列号（用于去重 — 每次剪贴板变化递增）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetClipboardSequenceNumber();

    // ─── 全局热键 ───

    /// <summary>
    /// 注册全局热键
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// 注销全局热键
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ─── 窗口管理 ───

    /// <summary>
    /// 获取前台窗口句柄（当前活动窗口）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 设置窗口到前台
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 获取窗口线程/进程 ID
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 附加输入线程（用于跨进程焦点操作）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    /// <summary>
    /// 获取当前线程 ID
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    // ─── 模拟输入 (SendInput) ───

    /// <summary>
    /// 发送模拟输入（用于模拟 Ctrl+V）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// INPUT 结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type; // 1 = INPUT_KEYBOARD
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ─── 多屏幕信息 ───

    /// <summary>
    /// 获取鼠标光标位置（屏幕坐标）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// 获取包含指定点的显示器句柄
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>
    /// 获取显示器信息（包括工作区）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>MonitorFromPoint 标志：返回包含该点的显示器</summary>
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    /// <summary>
    /// 获取指定显示器的 DPI（用于物理像素 → WPF 设备无关像素转换）
    /// </summary>
    [DllImport("shcore.dll", SetLastError = true)]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

    /// <summary>获取有效的 DPI 值（推荐使用）</summary>
    public const uint MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        public MONITORINFO() : this(false) { }

        public MONITORINFO(bool initialize)
        {
            cbSize = Marshal.SizeOf<MONITORINFO>();
            rcMonitor = default;
            rcWork = default;
            dwFlags = 0;
        }
    }

    // ─── 热键修饰符常量 ───

    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_WIN = 0x0008;
    internal const uint MOD_NOREPEAT = 0x4000;

    // ─── 虚拟键码 ───

    internal const ushort VK_CONTROL = 0x11;
    internal const ushort VK_LCONTROL = 0xA2;
    internal const ushort VK_RCONTROL = 0xA3;
    internal const ushort VK_V = 0x56;

    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    // ─── 窗口消息常量 ───

    internal const int WM_DRAWCLIPBOARD = 0x0308;
    internal const int WM_CHANGECBCHAIN = 0x030D;
    internal const int WM_HOTKEY = 0x0312;
    internal const int WM_DESTROY = 0x0002;
}
