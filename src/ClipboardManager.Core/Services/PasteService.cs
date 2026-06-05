using System.Runtime.InteropServices;
using ClipboardManager.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 粘贴服务 — 使用 SendInput 模拟 Ctrl+V 到当前活动窗口
/// </summary>
public class PasteService : IPasteService
{
    private readonly ILogger<PasteService> _logger;

    public PasteService(ILogger<PasteService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 模拟 Ctrl+V 粘贴到当前活动窗口
    ///
    /// 实现方式：SendInput 发送按键序列（Ctrl Down → V Down → V Up → Ctrl Up）
    /// 比 SendKeys 更可靠，不会被 Windows 的 User Interface Privilege Isolation (UIPI) 拦截
    /// </summary>
    public void SimulatePaste()
    {
        _logger.LogInformation("模拟 Ctrl+V 粘贴");

        // 构建按键输入序列: Ctrl down → V down → V up → Ctrl up
        var inputs = new NativeMethods.INPUT[4];

        // 0: Ctrl 按下
        inputs[0] = CreateKeyInput(NativeMethods.VK_CONTROL, KeyDirection.Down);

        // 1: V 按下
        inputs[1] = CreateKeyInput(NativeMethods.VK_V, KeyDirection.Down);

        // 2: V 释放
        inputs[2] = CreateKeyInput(NativeMethods.VK_V, KeyDirection.Up);

        // 3: Ctrl 释放
        inputs[3] = CreateKeyInput(NativeMethods.VK_CONTROL, KeyDirection.Up);

        // 发送输入序列
        var result = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());

        if (result != inputs.Length)
        {
            _logger.LogWarning("SendInput 返回值 ({Result}) 与预期 ({Expected}) 不符",
                result, inputs.Length);
        }
        else
        {
            _logger.LogDebug("Ctrl+V 模拟成功");
        }
    }

    /// <summary>
    /// 创建一个键盘输入结构
    /// </summary>
    private static NativeMethods.INPUT CreateKeyInput(ushort virtualKey, KeyDirection direction)
    {
        return new NativeMethods.INPUT
        {
            Type = 1, // INPUT_KEYBOARD
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = direction == KeyDirection.Up
                        ? NativeMethods.KEYEVENTF_KEYUP
                        : 0u,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private enum KeyDirection
    {
        Down,
        Up
    }
}
