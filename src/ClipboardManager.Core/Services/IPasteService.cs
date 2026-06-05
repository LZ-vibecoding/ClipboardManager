namespace ClipboardManager.Core.Services;

/// <summary>
/// 粘贴服务接口 — 模拟 Ctrl+V 到当前活动窗口
/// </summary>
public interface IPasteService
{
    /// <summary>模拟粘贴操作</summary>
    void SimulatePaste();
}
