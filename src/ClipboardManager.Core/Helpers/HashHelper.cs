using System.Security.Cryptography;
using System.Text;

namespace ClipboardManager.Core.Helpers;

/// <summary>
/// SHA256 哈希工具 — 用于内容去重
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// 计算文本的 SHA256 哈希（十六进制字符串）
    /// </summary>
    public static string ComputeTextHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return ComputeHash(bytes);
    }

    /// <summary>
    /// 计算二进制数据的 SHA256 哈希（十六进制字符串）
    /// </summary>
    public static string ComputeHash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexStringLower(hashBytes);
    }
}
