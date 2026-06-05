using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardManager.Core.Extensions;
using ClipboardManager.Core.Models;
using ClipboardManager.Core.Services;
using ClipboardManager.Wpf.ViewModels;
using ClipboardManager.Wpf.Views;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Wpf;

public partial class App : Application
{
    /// <summary>全局互斥锁 — 防止重复启动</summary>
    private static readonly Mutex AppMutex = new(true, "ClipboardManager_6F4B2A1E-3D8C-4E7F-9A1B-2C3D4E5F6789");

    private ServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private IClipboardManagerService? _manager;
    private AutoStartService? _autoStart;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 全局异常捕获 — 防止静默崩溃
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"发生未处理异常，应用将退出。\n\n{ex?.GetType().Name}: {ex?.Message}\n\n{ex?.StackTrace}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"UI 线程异常:\n\n{args.Exception.GetType().Name}: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            var ex = args.Exception?.InnerException ?? args.Exception;
            MessageBox.Show($"后台任务异常:\n\n{ex?.GetType().Name}: {ex?.Message}\n\n{ex?.StackTrace}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.SetObserved();
        };

        // 检查是否已有实例运行
        if (!AppMutex.WaitOne(TimeSpan.Zero, true))
        {
            MessageBox.Show("剪贴板管理器已在运行中。\n请在系统托盘中找到图标。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Environment.Exit(0);
            return;
        }

        base.OnStartup(e);

        // 1. 构建 DI 容器
        var services = new ServiceCollection();
        services.AddClipboardManager();
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("剪贴板管理器启动中...");

        // 2. 创建系统托盘图标
        CreateTrayIcon();

        // 3. 初始化核心服务
        _manager = _serviceProvider.GetRequiredService<IClipboardManagerService>();
        _manager.RequestShowHistory += OnRequestShowHistory;
        await _manager.InitializeAsync();

        // 4. 应用开机自启设置
        _autoStart = _serviceProvider.GetRequiredService<AutoStartService>();
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();
        var appSettings = await settings.LoadAsync();
        if (appSettings.AutoStartWithWindows != _autoStart.IsEnabled())
        {
            // 设置已变更时同步注册表
            _autoStart.SetEnabled(appSettings.AutoStartWithWindows);
        }

        logger.LogInformation("剪贴板管理器启动完成，已最小化到系统托盘");
    }

    /// <summary>
    /// 创建系统托盘图标 + 右键菜单
    /// </summary>
    private void CreateTrayIcon()
    {
        var blueBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        blueBrush.Freeze();

        _trayIcon = new TaskbarIcon
        {
            IconSource = CreateTrayIconSource(),
            ToolTipText = "剪贴板管理器",
            MenuActivation = PopupActivationMode.RightClick
        };

        // 右键菜单
        var contextMenu = new ContextMenu();

        var showMenuItem = new MenuItem { Header = "显示历史 (_S)" };
        showMenuItem.Click += (_, _) => OnRequestShowHistory(null, EventArgs.Empty);
        contextMenu.Items.Add(showMenuItem);

        contextMenu.Items.Add(new Separator());

        var settingsMenuItem = new MenuItem { Header = "设置 (_S)" };
        settingsMenuItem.Click += (_, _) => OnRequestShowSettings();
        contextMenu.Items.Add(settingsMenuItem);

        var exitMenuItem = new MenuItem { Header = "退出 (_X)" };
        exitMenuItem.Click += (_, _) => OnExitApp();
        contextMenu.Items.Add(exitMenuItem);

        _trayIcon.ContextMenu = contextMenu;

        // 双击托盘图标显示历史
        _trayIcon.DoubleClickCommand = new RelayCommand(_ => OnRequestShowHistory(null, EventArgs.Empty));
    }

    /// <summary>
    /// 打开设置窗口
    /// </summary>
    private void OnRequestShowSettings()
    {
        if (_serviceProvider == null) return;

        var viewModel = new SettingsViewModel(
            _serviceProvider.GetRequiredService<ISettingsService>(),
            _serviceProvider.GetRequiredService<AutoStartService>(),
            _serviceProvider.GetRequiredService<IClipboardManagerService>());

        var window = new SettingsWindow
        {
            DataContext = viewModel
        };

        // 窗口关闭时释放
        window.Closed += (_, _) => window = null;

        // 关闭事件
        viewModel.RequestClose += (_, _) => window.Close();

        // 异步加载设置
        _ = viewModel.LoadAsync();

        window.ShowDialog();
    }

    /// <summary>
    /// 创建一个简单的剪贴板图标（16x16）
    /// </summary>
    private static ImageSource CreateTrayIconSource()
    {
        var blueBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        var bluePen = new Pen(blueBrush, 1.5);
        var bgBrush = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1);
        bluePen.Freeze();
        blueBrush.Freeze();
        bgBrush.Freeze();
        linePen.Freeze();

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 剪贴板主体
            var rect = new System.Windows.Rect(2, 4, 12, 10);
            context.DrawRoundedRectangle(bgBrush, bluePen, rect, 1, 1);

            // 顶部夹子
            var clipRect = new System.Windows.Rect(5, 1, 6, 4);
            context.DrawRectangle(blueBrush, bluePen, clipRect);

            // 文档线条
            context.DrawLine(linePen, new System.Windows.Point(5, 8), new System.Windows.Point(11, 8));
            context.DrawLine(linePen, new System.Windows.Point(5, 10), new System.Windows.Point(11, 10));
            context.DrawLine(linePen, new System.Windows.Point(5, 12), new System.Windows.Point(9, 12));
        }

        var bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawingVisual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 显示历史记录窗口（热键 Ctrl+Shift+V 或托盘菜单触发）
    ///
    /// 每次创建一个新窗口实例，失焦后自动关闭并释放。
    /// 窗口弹出在鼠标所在屏幕的右下角。
    /// </summary>
    private void OnRequestShowHistory(object? sender, EventArgs e)
    {
        if (_manager == null) return;

        // 如果已有历史窗口打开，直接激活它（防止重复弹出）
        var existing = Current.Windows.OfType<HistoryWindow>().FirstOrDefault();
        if (existing != null)
        {
            existing.Activate();
            return;
        }

        // 创建 ViewModel + 窗口
        var viewModel = new HistoryViewModel(_manager);
        var window = new HistoryWindow(viewModel);

        // 窗口关闭时释放
        window.Closed += (_, _) => window = null;

        // 在鼠标所在屏幕右下角弹出
        window.ShowAtBottomRight();
    }

    /// <summary>
    /// 退出应用
    /// </summary>
    private void OnExitApp()
    {
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        AppMutex.ReleaseMutex();
        base.OnExit(e);
    }
}
