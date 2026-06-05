namespace ClipboardManager.Core.Services;

/// <summary>
/// 图片处理服务接口
/// </summary>
public interface IImageService
{
    /// <summary>
    /// 保存图片到磁盘
    /// </summary>
    /// <param name="imageBytes">图片原始字节</param>
    /// <returns>哈希、原图路径、缩略图路径</returns>
    Task<ImageSaveResult> SaveImageAsync(byte[] imageBytes);

    /// <summary>
    /// 删除图片文件
    /// </summary>
    void DeleteImageFiles(string? imagePath, string? thumbnailPath);
}

public class ImageSaveResult
{
    public string Hash { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
    public string ThumbnailPath { get; init; } = string.Empty;
    public bool AlreadyExisted { get; init; }
}
