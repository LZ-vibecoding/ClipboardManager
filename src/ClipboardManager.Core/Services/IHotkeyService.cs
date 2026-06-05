namespace ClipboardManager.Core.Services;

/// <summary>
/// 全局热键服务接口
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>热键按下事件</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>注册全局热键</summary>
    void Register(uint modifiers, uint virtualKey);

    /// <summary>取消注册</summary>
    void Unregister();
}
