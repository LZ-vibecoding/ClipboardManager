using System.Windows.Input;
using ClipboardManager.Core.Models;
using ClipboardManager.Core.Services;

namespace ClipboardManager.Wpf.ViewModels;

/// <summary>
/// 设置窗口 ViewModel — 绑定所有配置项
///
/// 加载当前设置 → 用户修改 → 保存到数据库
/// 变化实时应用到运行中的服务（开机自启立即生效）
/// </summary>
public class SettingsViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly AutoStartService _autoStartService;
    private readonly IClipboardManagerService _managerService;

    /// <summary>窗口关闭请求事件</summary>
    public event EventHandler? RequestClose;

    public SettingsViewModel(
        ISettingsService settingsService,
        AutoStartService autoStartService,
        IClipboardManagerService managerService)
    {
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _managerService = managerService;

        SaveCommand = new RelayCommand(_ => OnSave());
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    // ── 设置属性 ──

    /// <summary>最大条目数 (50-1000)</summary>
    public int MaxItemCount { get; set; } = 200;

    /// <summary>是否开机自启</summary>
    public bool AutoStartWithWindows { get; set; }

    /// <summary>是否监控图片</summary>
    public bool MonitorImages { get; set; } = true;

    /// <summary>是否监控文本</summary>
    public bool MonitorText { get; set; } = true;

    /// <summary>缩略图尺寸 (64-256)</summary>
    public int ThumbnailSize { get; set; } = 120;

    /// <summary>图片质量 (1-100)</summary>
    public int ImageQuality { get; set; } = 85;

    // ── 命令 ──

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    // ── 加载/保存 ──

    /// <summary>
    /// 从数据库加载当前设置
    /// </summary>
    public async Task LoadAsync()
    {
        var settings = await _settingsService.LoadAsync();

        MaxItemCount = settings.MaxItemCount;
        AutoStartWithWindows = _autoStartService.IsEnabled();
        MonitorImages = settings.MonitorImages;
        MonitorText = settings.MonitorText;
        ThumbnailSize = settings.ThumbnailSize;
        ImageQuality = settings.ImageQuality;
    }

    /// <summary>
    /// 保存设置到数据库 + 应用到运行中服务
    /// </summary>
    private async void OnSave()
    {
        // 1. 保存到数据库
        var settings = new AppSettings
        {
            MaxItemCount = MaxItemCount,
            MonitorImages = MonitorImages,
            MonitorText = MonitorText,
            ThumbnailSize = ThumbnailSize,
            ImageQuality = ImageQuality
        };

        await _settingsService.SaveAsync(settings);

        // 2. 开机自启立即生效
        _autoStartService.SetEnabled(AutoStartWithWindows);

        // 3. 通知管理器重载设置（影响监控行为等）
        // 目前 ClipboardManagerService 在下次操作时自动读取最新设置

        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
