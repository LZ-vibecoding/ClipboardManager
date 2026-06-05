using System.Windows;
using System.Windows.Input;

namespace ClipboardManager.Wpf.Views;

/// <summary>
/// 侧边栏吸附手柄 — 窗口"最小化"到屏幕右侧时显示的小 tab
///
/// 设计：36×110px，右边缘圆角，蓝底 📋 图标 + "剪"文字，
/// 悬停时背景变浅蓝，点击后恢复主窗口。
/// </summary>
public partial class SidebarTabWindow : Window
{
    /// <summary>用户点击 tab → 触发此事件，由 HistoryWindow 恢复自身</summary>
    public event EventHandler? RestoreRequested;

    public SidebarTabWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点击 tab 任意位置 → 通知主窗口恢复
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        RestoreRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
        base.OnMouseDown(e);
    }
}
