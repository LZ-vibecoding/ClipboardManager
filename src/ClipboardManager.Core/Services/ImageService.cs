using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardManager.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipboardManager.Core.Services;

/// <summary>
/// 图片处理服务 — 保存图片文件 + 生成缩略图
///
/// 文件结构：
///   %LocalAppData%\ClipboardManager\
///     images\{hash[..2]}\{hash}.png       — 原图
///     thumbnails\{hash[..2]}\{hash}.png   — 120x120 缩略图
///
/// 哈希前 2 位作为子目录名，避免单个目录下文件过多（每目录约 65536 个文件后才需拆分）
/// </summary>
public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly string _imageBaseDir;
    private readonly string _thumbnailBaseDir;

    private const int ThumbnailSize = 120;               // 缩略图最长边
    private const int MaxPixels = 2000 * 2000;           // 超过 2000x2000 时等比缩放再保存
    private const int MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB 限制

    public ImageService(ILogger<ImageService> logger)
    {
        _logger = logger;

        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipboardManager");

        _imageBaseDir = Path.Combine(baseDir, "images");
        _thumbnailBaseDir = Path.Combine(baseDir, "thumbnails");

        Directory.CreateDirectory(_imageBaseDir);
        Directory.CreateDirectory(_thumbnailBaseDir);
    }

    /// <summary>
    /// 保存图片到磁盘
    ///
    /// 流程：计算哈希 → 创建子目录 → 保存原图 → 生成缩略图
    /// 如果文件已存在则跳过（内容相同）
    /// </summary>
    public async Task<ImageSaveResult> SaveImageAsync(byte[] imageBytes)
    {
        // 超大图片仅生成缩略图，不保存原图
        if (imageBytes.Length > MaxFileSizeBytes)
        {
            _logger.LogWarning("图片超过 50MB（实际 {Size}MB），仅生成缩略图",
                imageBytes.Length / 1024 / 1024);
            return await SaveThumbnailOnlyAsync(imageBytes);
        }

        var hash = HashHelper.ComputeHash(imageBytes);
        var subDir = hash[..2]; // 哈希前 2 位 → 子目录

        var imageDir = Path.Combine(_imageBaseDir, subDir);
        var thumbnailDir = Path.Combine(_thumbnailBaseDir, subDir);
        Directory.CreateDirectory(imageDir);
        Directory.CreateDirectory(thumbnailDir);

        var imagePath = Path.Combine(imageDir, $"{hash}.png");
        var thumbnailPath = Path.Combine(thumbnailDir, $"{hash}.png");

        // 文件已存在 → 跳过（内容相同）
        if (File.Exists(imagePath) && File.Exists(thumbnailPath))
        {
            return new ImageSaveResult
            {
                Hash = hash,
                ImagePath = imagePath,
                ThumbnailPath = thumbnailPath,
                AlreadyExisted = true
            };
        }

        // 保存原图（大图超过 2000px 时等比缩小再保存）
        await SaveOriginalImageAsync(imageBytes, imagePath, hash);

        // 生成缩略图
        await GenerateThumbnailAsync(imageBytes, thumbnailPath, hash);

        _logger.LogInformation("图片已保存: {Path}", imagePath);

        return new ImageSaveResult
        {
            Hash = hash,
            ImagePath = imagePath,
            ThumbnailPath = thumbnailPath
        };
    }

    /// <summary>
    /// 超大图片（>50MB）仅生成缩略图
    /// </summary>
    private async Task<ImageSaveResult> SaveThumbnailOnlyAsync(byte[] imageBytes)
    {
        var hash = HashHelper.ComputeHash(imageBytes);
        var subDir = hash[..2];
        var thumbnailDir = Path.Combine(_thumbnailBaseDir, subDir);
        Directory.CreateDirectory(thumbnailDir);

        var thumbnailPath = Path.Combine(thumbnailDir, $"{hash}.png");

        if (File.Exists(thumbnailPath))
        {
            return new ImageSaveResult
            {
                Hash = hash,
                ImagePath = string.Empty,
                ThumbnailPath = thumbnailPath,
                AlreadyExisted = true
            };
        }

        await GenerateThumbnailAsync(imageBytes, thumbnailPath, hash);

        return new ImageSaveResult
        {
            Hash = hash,
            ImagePath = string.Empty,
            ThumbnailPath = thumbnailPath
        };
    }

    /// <summary>
    /// 保存原图（超过 2000x2000 时等比缩小）
    /// </summary>
    private async Task SaveOriginalImageAsync(byte[] imageBytes, string imagePath, string hash)
    {
        // 先检查图片尺寸是否需要缩小
        var needsResize = await Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                return frame.PixelWidth * frame.PixelHeight > MaxPixels;
            }
            catch
            {
                return false; // 解码失败则直接保存原文件
            }
        });

        if (needsResize)
        {
            _logger.LogDebug("图片分辨率超过 2000x2000，等比缩放后保存");
            await Task.Run(() =>
            {
                using var ms = new MemoryStream(imageBytes);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];

                // 等比缩放到最长边 2000px
                double scale = Math.Min(
                    2000.0 / frame.PixelWidth,
                    2000.0 / frame.PixelHeight);
                var resized = new TransformedBitmap(frame, new ScaleTransform(scale, scale));

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(resized));
                using var outMs = new MemoryStream();
                encoder.Save(outMs);
                File.WriteAllBytes(imagePath, outMs.ToArray());
            });
        }
        else
        {
            // 直接保存原文件
            await File.WriteAllBytesAsync(imagePath, imageBytes);
        }
    }

    /// <summary>
    /// 生成 120x120 缩略图（保持宽高比）
    /// </summary>
    private async Task GenerateThumbnailAsync(byte[] imageBytes, string thumbnailPath, string hash)
    {
        await Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];

                // 计算缩放比例（保持宽高比）
                double scale = Math.Min(
                    (double)ThumbnailSize / frame.PixelWidth,
                    (double)ThumbnailSize / frame.PixelHeight);

                // 如果原图比缩略图还小，不放大
                if (scale >= 1.0)
                {
                    // 直接保存一份 PNG
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(frame));
                    using var outMs = new MemoryStream();
                    encoder.Save(outMs);
                    File.WriteAllBytes(thumbnailPath, outMs.ToArray());
                }
                else
                {
                    var thumb = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(thumb));
                    using var outMs = new MemoryStream();
                    encoder.Save(outMs);
                    File.WriteAllBytes(thumbnailPath, outMs.ToArray());
                }

                _logger.LogDebug("缩略图已生成: {Path}", thumbnailPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "缩略图生成失败 (Hash={Hash})，使用原图作为缩略图",
                    hash[..8]);
                // 降级：直接复制原图作为缩略图
                File.WriteAllBytes(thumbnailPath, imageBytes);
            }
        });
    }

    /// <summary>
    /// 删除图片文件（同时清理空目录）
    /// </summary>
    public void DeleteImageFiles(string? imagePath, string? thumbnailPath)
    {
        DeleteFileIfExists(imagePath);
        DeleteFileIfExists(thumbnailPath);
    }

    /// <summary>
    /// 删除文件，如果父目录为空则一并删除
    /// </summary>
    private void DeleteFileIfExists(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            File.Delete(path);
            _logger.LogDebug("已删除文件: {Path}", path);

            // 尝试删除空父目录
            var dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) &&
                !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除文件失败: {Path}", path);
        }
    }
}
