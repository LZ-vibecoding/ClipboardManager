using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using ClipboardManager.Core.Models;
using ClipboardManager.Core.Services;

namespace ClipboardManager.Wpf.ViewModels;

/// <summary>
/// 历史记录分组（按日期）
/// </summary>
public class ClipboardItemGroup
{
    public string Header { get; init; } = string.Empty;
    public ObservableCollection<ClipboardItem> Items { get; init; } = new();
}

/// <summary>
/// 历史窗口 ViewModel — 加载、搜索、固定、删除、粘贴
///
/// 数据按"固定项 → 今天 → 昨天 → 更早"分组显示
/// 搜索使用 300ms 防抖，避免频繁查询数据库
/// </summary>
public class HistoryViewModel : INotifyPropertyChanged
{
    private readonly IClipboardManagerService _manager;
    private readonly Dispatcher _dispatcher;

    private CancellationTokenSource? _searchDebounceCts;
    private const int SearchDebounceMs = 300;

    public HistoryViewModel(IClipboardManagerService manager)
    {
        _manager = manager;
        // 捕获当前 Dispatcher（WPF 主线程），用于跨线程更新 UI 集合
        _dispatcher = Dispatcher.CurrentDispatcher;

        // 命令
        PinCommand = new RelayCommand<ClipboardItem>(OnPin);
        DeleteCommand = new RelayCommand<ClipboardItem>(OnDelete);
        PasteCommand = new RelayCommand<ClipboardItem>(OnPaste);

        // 监听外部变化（热键触发时重新加载）
        _manager.HistoryChanged += async (_, items) =>
            await _dispatcher.InvokeAsync(() => RebuildGroups(items));
    }

    /// <summary>粘贴操作完成，通知窗口关闭</summary>
    public event EventHandler? PasteReady;

    /// <summary>按日期分组的历史记录</summary>
    public ObservableCollection<ClipboardItemGroup> Groups { get; } = new();

    /// <summary>是否正在加载</summary>
    public bool IsLoading { get; private set; }

    private bool _hasItems;
    /// <summary>是否有条目</summary>
    public bool HasItems
    {
        get => _hasItems;
        private set => SetProperty(ref _hasItems, value);
    }

    /// <summary>无条目时显示的提示文字</summary>
    public string EmptyMessage { get; private set; } = "暂无剪贴板历史记录。\n复制文本或图片后会自动记录。";

    private string _searchText = string.Empty;
    /// <summary>搜索文本（300ms 防抖）</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnSearchTextChanged();
        }
    }

    // ── 命令 ──

    public ICommand PinCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand PasteCommand { get; }

    // ── INotifyPropertyChanged ──

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }

    // ── 公开方法 ──

    /// <summary>
    /// 加载历史记录
    /// </summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _manager.GetHistoryAsync();
            RebuildGroups(items);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    public async Task SearchAsync(string query)
    {
        IsLoading = true;
        try
        {
            var items = await _manager.GetHistoryAsync(
                string.IsNullOrWhiteSpace(query) ? null : query);
            RebuildGroups(items);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── 私有方法 ──

    /// <summary>
    /// 搜索文本变化 → 防抖后执行搜索
    /// </summary>
    private void OnSearchTextChanged()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        var text = _searchText;

        // 防抖 → 延迟后回到 UI 线程执行搜索（ObservableCollection 只能在 UI 线程修改）
        Task.Delay(SearchDebounceMs, token)
            .ContinueWith(async _ =>
            {
                if (token.IsCancellationRequested) return;
                await _dispatcher.InvokeAsync(() => SearchAsync(text));
            }, token, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    /// 将条目列表按日期分组
    /// </summary>
    private void RebuildGroups(IReadOnlyList<ClipboardItem> items)
    {
        Groups.Clear();

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        var pinned = items.Where(i => i.IsPinned).ToList();
        var todayItems = items.Where(i => !i.IsPinned && i.UpdatedAt.Date == today).ToList();
        var yesterdayItems = items.Where(i => !i.IsPinned && i.UpdatedAt.Date == yesterday).ToList();
        var olderItems = items.Where(i => !i.IsPinned && i.UpdatedAt.Date < yesterday).ToList();

        if (pinned.Count > 0)
            Groups.Add(new ClipboardItemGroup { Header = "📌 固定内容", Items = new ObservableCollection<ClipboardItem>(pinned) });

        if (todayItems.Count > 0)
            Groups.Add(new ClipboardItemGroup { Header = "今天", Items = new ObservableCollection<ClipboardItem>(todayItems) });

        if (yesterdayItems.Count > 0)
            Groups.Add(new ClipboardItemGroup { Header = "昨天", Items = new ObservableCollection<ClipboardItem>(yesterdayItems) });

        if (olderItems.Count > 0)
            Groups.Add(new ClipboardItemGroup { Header = "更早", Items = new ObservableCollection<ClipboardItem>(olderItems) });

        HasItems = Groups.Sum(g => g.Items.Count) > 0;
    }

    // ── 命令处理 ──

    private async void OnPin(ClipboardItem? item)
    {
        if (item == null) return;
        await _manager.PinAsync(item.Id, !item.IsPinned);
        item.IsPinned = !item.IsPinned;
        await LoadAsync(); // 重新分组（固定项需置顶）
    }

    private async void OnDelete(ClipboardItem? item)
    {
        if (item == null) return;
        await _manager.DeleteAsync(item.Id);
        await LoadAsync();
    }

    /// <summary>
    /// 粘贴操作
    ///
    /// 1. 将条目内容写入系统剪贴板
    /// 2. 触发 PasteReady → 由 HistoryWindow 关闭窗口
    /// 3. 用户手动按 Ctrl+V 完成粘贴
    /// </summary>
    private async void OnPaste(ClipboardItem? item)
    {
        if (item == null) return;
        try
        {
            await _manager.CopyToClipboardAsync(item);
            PasteReady?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"粘贴失败: {ex.Message}");
        }
    }
}
