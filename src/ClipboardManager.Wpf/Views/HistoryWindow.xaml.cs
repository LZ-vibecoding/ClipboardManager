using System.Windows;
using System.Windows.Input;
using ClipboardManager.Core.Helpers;
using ClipboardManager.Wpf.ViewModels;

namespace ClipboardManager.Wpf.Views;

/// <summary>
/// 剪贴板历史记录窗口
///
/// 行为：
/// - 弹出在鼠标所在屏幕的右下角（贴近任务栏）
/// - 失焦自动关闭（带 300ms 防抖，避免闪退）
/// - 搜索框自动获得焦点，支持 Esc 关闭、Ctrl+F 搜索
/// - 搜索时有清空按钮
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _viewModel;

    // 吸附侧边栏
    private SidebarTabWindow? _sidebarTab;
    private double _normalLeft;
    private double _normalTop;

    public HistoryWindow(HistoryViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // 粘贴流程：ViewModel 将内容写入剪贴板后 → 关闭窗口（用户手动 Ctrl+V）
        _viewModel.PasteReady += (_, _) =>
        {
            Close();
        };

        // 双击条目 → 粘贴命令（同样的 PasteCommand → 上述流程）
        // 搜索框文本变化时显示/隐藏清空按钮
        SearchBox.TextChanged += (_, _) =>
        {
            ClearSearchBtn.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        };
    }

    /// <summary>
    /// 在鼠标所在屏幕的右下角弹出窗口
    ///
    /// 关键细节：Win32 GetMonitorInfo 返回物理像素，WPF Left/Top 是设备无关像素，
    /// 需要用 DPI 缩放比转换 (pixels ÷ scale = dips)。
    ///
    /// 修正：先将所有物理像素除以缩放系数得到 DIP，再计算 Left/Top。
    /// 旧代码的错误：wa.Right(物理像素) - Width(DIP) 混合单位直接相减。
    /// </summary>
    public void ShowAtBottomRight()
    {
        // 如果吸附手柄正在显示，关闭它
        HideSidebarTab();

        // 获取鼠标位置（物理像素）
        if (!NativeMethods.GetCursorPos(out var mousePos))
        {
            ShowAtPrimaryScreenBottomRight();
            return;
        }

        // 获取鼠标所在显示器
        var hMonitor = NativeMethods.MonitorFromPoint(mousePos,
            NativeMethods.MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new NativeMethods.MONITORINFO(initialize: true);
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            ShowAtPrimaryScreenBottomRight();
            return;
        }

        // 获取该显示器的 DPI 缩放
        var scale = GetMonitorScale(hMonitor);

        // 物理像素 → 统一转换为 DIP
        var wa = monitorInfo.rcWork;
        var waLeft = wa.Left / scale;
        var waTop = wa.Top / scale;
        var waRight = wa.Right / scale;
        var waBottom = wa.Bottom / scale;

        // Width/Height 可能在首次显示前为 NaN，用 XAML 默认值
        var width = double.IsNaN(Width) ? 420 : Width;
        var height = double.IsNaN(Height) ? 600 : Height;

        Left = waRight - width - 8;
        Top = waBottom - height - 8;

        // 边界保护
        if (Left < waLeft) Left = waLeft + 8;
        if (Top < waTop) Top = waTop + 8;

        ShowAndLoad();
    }

    /// <summary>
    /// 回退方案：SystemParameters.WorkArea 已经是 DPI 感知的，无需转换
    /// </summary>
    private void ShowAtPrimaryScreenBottomRight()
    {
        HideSidebarTab();

        var wa = SystemParameters.WorkArea;

        Left = wa.Right - Width - 8;
        Top = wa.Bottom - Height - 8;

        if (Left < wa.Left) Left = wa.Left + 8;
        if (Top < wa.Top) Top = wa.Top + 8;
        ShowAndLoad();
    }

    /// <summary>
    /// 获取指定显示器的 DPI 缩放系数（96 DPI = 1.0x, 144 DPI = 1.5x）
    /// </summary>
    private static double GetMonitorScale(IntPtr hMonitor)
    {
        try
        {
            if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI,
                    out var dpiX, out _) == 0) // S_OK
            {
                return dpiX / 96.0;
            }
        }
        catch
        {
            // 降级：假设 96 DPI (100%)
        }
        return 1.0;
    }

    /// <summary>
    /// 显示窗口 + 自动聚焦 + 加载数据
    /// </summary>
    private void ShowAndLoad()
    {
        Show();

        // 搜索框自动获得焦点
        SearchBox.Focus();
        SearchBox.SelectAll();

        // 异步加载历史数据
        _ = _viewModel.LoadAsync();
    }

    /// <summary>
    /// 窗口激活时，不需要额外操作（用户通过 Esc 或关闭按钮手动关窗）
    /// </summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
    }

    /// <summary>
    /// 失焦时不自动关闭（用户通过 Esc 或 × 按钮手动关闭）
    /// </summary>
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
    }

    /// <summary>
    /// 键盘快捷键处理
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Esc 关闭
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        // Ctrl+F 聚焦搜索框
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        HideSidebarTab();
        Close();
    }

    /// <summary>
    /// 窗口关闭时确保手柄也被清理
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        HideSidebarTab();
        base.OnClosed(e);
    }

    /// <summary>
    /// 最小化按钮 → 吸附到屏幕右侧边缘
    ///
    /// 窗口滑到屏幕右侧，只留 ~30px 的条在屏幕上，
    /// 其余部分滑出屏幕右侧。点击可见条恢复原位。
    /// </summary>
    /// <summary>
    /// 最小化 → 吸附侧边栏（优雅手柄版）
    ///
    /// 点击后隐藏主窗口，在屏幕右侧边缘显示一个小巧的 📋 手柄。
    /// 点击手柄恢复主窗口到原位。
    /// </summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (_sidebarTab != null)
        {
            RestoreFromSidebar();
            return;
        }

        // 保存当前位置
        _normalLeft = Left;
        _normalTop = Top;

        // 隐藏主窗口
        Hide();

        // 创建并显示侧边栏手柄
        _sidebarTab = new SidebarTabWindow();
        _sidebarTab.RestoreRequested += (_, _) => RestoreFromSidebar();

        // 定位到当前屏幕右侧边缘（垂直居中）
        PositionSidebarTab();

        _sidebarTab.Show();
    }

    /// <summary>
    /// 将手柄定位到当前屏幕右边缘
    /// </summary>
    private void PositionSidebarTab()
    {
        if (_sidebarTab == null) return;

        try
        {
            if (NativeMethods.GetCursorPos(out var pos))
            {
                var hMonitor = NativeMethods.MonitorFromPoint(pos,
                    NativeMethods.MONITOR_DEFAULTTONEAREST);
                var mi = new NativeMethods.MONITORINFO(initialize: true);
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    var scale = GetMonitorScale(hMonitor);
                    var right = mi.rcWork.Right / scale;
                    var top = mi.rcWork.Top / scale;
                    var bottom = mi.rcWork.Bottom / scale;

                    _sidebarTab.Left = right - 36;  // 36px 宽
                    _sidebarTab.Top = (top + bottom) / 2 - 55; // 垂直居中
                    return;
                }
            }
        }
        catch { }

        // 降级：主屏幕右边缘
        var wa = SystemParameters.WorkArea;
        _sidebarTab.Left = wa.Right - 36;
        _sidebarTab.Top = (wa.Top + wa.Bottom) / 2 - 55;
    }

    /// <summary>
    /// 从侧边栏恢复主窗口
    /// </summary>
    private void RestoreFromSidebar()
    {
        // 关闭手柄
        if (_sidebarTab != null)
        {
            _sidebarTab.RestoreRequested -= null;
            _sidebarTab.Close();
            _sidebarTab = null;
        }

        // 恢复主窗口到原位
        Left = _normalLeft;
        Top = _normalTop;
        Show();

        // 重新聚焦
        SearchBox.Focus();
        SearchBox.SelectAll();
        _ = _viewModel.LoadAsync();
    }

    /// <summary>
    /// 关闭侧边栏手柄（窗口正常显示/关闭时调用）
    /// </summary>
    private void HideSidebarTab()
    {
        if (_sidebarTab != null)
        {
            _sidebarTab.RestoreRequested -= null;
            _sidebarTab.Close();
            _sidebarTab = null;
        }
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SearchBox.Focus();
    }

    /// <summary>
    /// 双击条目 → 触发粘贴
    ///
    /// 使用 MouseDown + ClickCount 检测双击（Border 不支持 MouseDoubleClick）
    /// </summary>
    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2 &&
            sender is FrameworkElement element &&
            element.DataContext is Core.Models.ClipboardItem item &&
            DataContext is HistoryViewModel vm)
        {
            vm.PasteCommand.Execute(item);
            e.Handled = true;
        }
    }
}
