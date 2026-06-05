using ClipboardManager.Core.Helpers;
using ClipboardManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 剪贴板管理器总协调服务
///
/// 串联所有核心服务：监控 → 去重 → 存储 → 展示 → 粘贴
/// </summary>
public class ClipboardManagerService : IClipboardManagerService
{
    private readonly IStorageService _storage;
    private readonly IClipboardMonitor _monitor;
    private readonly IHotkeyService _hotkey;
    private readonly ISettingsService _settings;
    private readonly IImageService _imageService;
    private readonly ILogger<ClipboardManagerService> _logger;

    private bool _initialized;

    // 事件
    private EventHandler<IReadOnlyList<ClipboardItem>>? _historyChanged;
    public event EventHandler<IReadOnlyList<ClipboardItem>>? HistoryChanged
    {
        add => _historyChanged += value;
        remove => _historyChanged -= value;
    }

    private EventHandler? _requestShowHistory;
    public event EventHandler? RequestShowHistory
    {
        add => _requestShowHistory += value;
        remove => _requestShowHistory -= value;
    }

    public ClipboardManagerService(
        IStorageService storage,
        IClipboardMonitor monitor,
        IHotkeyService hotkey,
        ISettingsService settings,
        IImageService imageService,
        ILogger<ClipboardManagerService> logger)
    {
        _storage = storage;
        _monitor = monitor;
        _hotkey = hotkey;
        _settings = settings;
        _imageService = imageService;
        _logger = logger;
    }

    /// <summary>
    /// 初始化所有服务
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("剪贴板管理器初始化...");

        // 1. 初始化存储
        await _storage.InitializeAsync();

        // 2. 加载设置
        var settings = await _settings.LoadAsync();

        // 3. 剪贴板监控事件绑定
        _monitor.ClipboardDataReady += OnClipboardDataReady;

        // 4. 热键事件绑定
        _hotkey.HotkeyPressed += OnHotkeyPressed;

        // 5. 注册全局热键 (Ctrl + Shift + V)
        _hotkey.Register(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT,
            NativeMethods.VK_V);

        // 6. 启动剪贴板监控
        _monitor.Start();

        _initialized = true;
        _logger.LogInformation("剪贴板管理器初始化完成");
    }

    /// <summary>
    /// 剪贴板数据就绪处理 — 去重 → 存储 → 刷新 UI
    /// </summary>
    private async void OnClipboardDataReady(object? sender, ClipboardData data)
    {
        try
        {
            // 层级3: 内容哈希去重 — 查询数据库是否已存在
            var existing = await _storage.GetByHashAsync(data.Hash, data.Type);
            if (existing != null)
            {
                // 已存在 → 更新时间戳，条目"浮"到列表顶部
                _logger.LogDebug("去重命中，更新时间戳: Hash={Hash}", data.Hash[..8]);
                await _storage.UpdateTimestampAsync(existing.Id);
                _historyChanged?.Invoke(this, await _storage.GetRecentAsync());
                return;
            }

            // 新内容 → 保存
            string? imagePath = null;
            string? thumbnailPath = null;

            if (data.Type == ClipboardType.Image && data.ImageBytes != null)
            {
                // 保存图片文件到磁盘
                var result = await _imageService.SaveImageAsync(data.ImageBytes);
                imagePath = result.ImagePath;
                thumbnailPath = result.ThumbnailPath;
                _logger.LogDebug("图片已保存: {Path}", imagePath);
            }

            // 写入数据库
            var item = new ClipboardItem
            {
                ContentHash = data.Hash,
                Type = data.Type,
                TextContent = data.Text,
                ImagePath = imagePath,
                ThumbnailPath = thumbnailPath,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _storage.InsertAsync(item);
            _logger.LogInformation("新条目已保存: Type={Type}, Hash={Hash}",
                data.Type, data.Hash[..8]);

            // 检查并清理超限条目
            await TrimToLimitAsync();

            // 通知 UI 刷新
            _historyChanged?.Invoke(this, await _storage.GetRecentAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理剪贴板数据时发生错误");
        }
    }

    /// <summary>
    /// 热键触发 — 显示历史窗口
    /// </summary>
    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _logger.LogDebug("热键触发 → 请求显示历史窗口");
        _requestShowHistory?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清理超限条目
    /// </summary>
    private async Task TrimToLimitAsync()
    {
        var settings = await _settings.LoadAsync();
        var count = await _storage.GetCountAsync();
        if (count <= settings.MaxItemCount) return;

        var toDelete = await _storage.GetTrimCandidatesAsync(count - settings.MaxItemCount);
        foreach (var item in toDelete)
        {
            if (item.ImagePath != null)
                _imageService.DeleteImageFiles(item.ImagePath, item.ThumbnailPath);
            await _storage.DeleteAsync(item.Id);
        }
        _logger.LogInformation("已清理 {Count} 条超限记录", toDelete.Count);
    }

    /// <summary>
    /// 获取历史记录
    /// </summary>
    public async Task<List<ClipboardItem>> GetHistoryAsync(string? search = null)
    {
        return await _storage.GetRecentAsync(search);
    }

    /// <summary>
    /// 仅将条目内容写入系统剪贴板（不模拟粘贴）
    ///
    /// 从 PasteAsync 提取出的前半段逻辑，用于"关窗后再粘贴"的流程。
    /// 剪贴板写入最多重试 5 次（其他进程可能占用剪贴板）
    /// </summary>
    public async Task CopyToClipboardAsync(ClipboardItem item)
    {
        _logger.LogInformation("写入剪贴板: Id={Id}, Type={Type}", item.Id, item.Type);

        var clipboardOk = false;
        for (var retry = 0; retry < 5; retry++)
        {
            try
            {
                if (item.Type == ClipboardType.Text && item.TextContent != null)
                {
                    System.Windows.Clipboard.SetText(item.TextContent);
                    _logger.LogDebug("文本已写入剪贴板");
                }
                else if (item.Type == ClipboardType.Image && item.ImagePath != null)
                {
                    if (!System.IO.File.Exists(item.ImagePath))
                    {
                        _logger.LogWarning("图片文件不存在: {Path}", item.ImagePath);
                        if (item.ThumbnailPath != null && System.IO.File.Exists(item.ThumbnailPath))
                        {
                            var thumb = new System.Windows.Media.Imaging.BitmapImage(
                                new Uri(item.ThumbnailPath, UriKind.Absolute));
                            System.Windows.Clipboard.SetImage(thumb);
                        }
                        else
                        {
                            _logger.LogError("无法粘贴图片：文件不存在");
                            return;
                        }
                    }
                    else
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage(
                            new Uri(item.ImagePath, UriKind.Absolute));
                        System.Windows.Clipboard.SetImage(bitmap);
                        _logger.LogDebug("图片已写入剪贴板: {Path}", item.ImagePath);
                    }
                }

                clipboardOk = true;
                break;
            }
            catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.ErrorCode == 0x800401D0)
            {
                _logger.LogWarning("剪贴板被占用，重试 {Retry}/5...", retry + 1);
                await Task.Delay(100 * (retry + 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入剪贴板失败");
                throw;
            }
        }

        if (!clipboardOk)
        {
            _logger.LogError("剪贴板写入失败，放弃");
            return;
        }

        // 50ms 等待确保剪贴板已就绪
        await Task.Delay(50);

        // 更新时间戳（条目浮到列表顶部）
        await _storage.UpdateTimestampAsync(item.Id);
    }

    /// <summary>
    /// 粘贴条目到当前活动窗口
    ///
    /// 流程：将条目内容写回系统剪贴板 → 等待就绪
    /// 图片从本地文件加载后设置为 BitmapImage
    ///
    /// 剪贴板写入最多重试 5 次（其他进程可能占用剪贴板）
    /// </summary>
    public async Task PasteAsync(ClipboardItem item)
    {
        _logger.LogInformation("粘贴条目: Id={Id}, Type={Type}", item.Id, item.Type);

        // 将内容写入系统剪贴板（需要 STA 线程 — OnPaste 由 UI 命令触发，已在 STA 上）
        // 如果剪贴板被其他进程占用，重试最多 5 次
        var clipboardOk = false;
        for (var retry = 0; retry < 5; retry++)
        {
            try
            {
                if (item.Type == ClipboardType.Text && item.TextContent != null)
                {
                    System.Windows.Clipboard.SetText(item.TextContent);
                    _logger.LogDebug("文本已写入剪贴板");
                }
                else if (item.Type == ClipboardType.Image && item.ImagePath != null)
                {
                    // 从本地 PNG 文件加载图片并设置到剪贴板
                    if (!System.IO.File.Exists(item.ImagePath))
                    {
                        _logger.LogWarning("图片文件不存在: {Path}", item.ImagePath);
                        // 降级：尝试从缩略图加载
                        if (item.ThumbnailPath != null && System.IO.File.Exists(item.ThumbnailPath))
                        {
                            var thumb = new System.Windows.Media.Imaging.BitmapImage(
                                new Uri(item.ThumbnailPath, UriKind.Absolute));
                            System.Windows.Clipboard.SetImage(thumb);
                        }
                        else
                        {
                            _logger.LogError("无法粘贴图片：文件不存在");
                            return;
                        }
                    }
                    else
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage(
                            new Uri(item.ImagePath, UriKind.Absolute));
                        System.Windows.Clipboard.SetImage(bitmap);
                        _logger.LogDebug("图片已写入剪贴板: {Path}", item.ImagePath);
                    }
                }

                clipboardOk = true;
                break;
            }
            catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.ErrorCode == 0x800401D0) // CLIPBRD_E_CANT_OPEN
            {
                _logger.LogWarning("剪贴板被占用，重试 {Retry}/5...", retry + 1);
                await Task.Delay(100 * (retry + 1)); // 递增等待: 100ms, 200ms, 300ms...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入剪贴板失败");
                throw;
            }
        }

        if (!clipboardOk)
        {
            _logger.LogError("剪贴板写入失败，放弃粘贴");
            return;
        }

        // 50ms 等待确保剪贴板已就绪
        await Task.Delay(50);

        // 更新时间戳（条目浮到列表顶部）
        await _storage.UpdateTimestampAsync(item.Id);
    }

    /// <summary>
    /// 固定/取消固定条目
    /// </summary>
    public async Task PinAsync(string id, bool pinned)
    {
        await _storage.SetPinnedAsync(id, pinned);
    }

    /// <summary>
    /// 删除条目
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        var item = await _storage.GetByIdAsync(id);
        if (item?.ImagePath != null)
            _imageService.DeleteImageFiles(item.ImagePath, item.ThumbnailPath);
        await _storage.DeleteAsync(id);
    }
}
