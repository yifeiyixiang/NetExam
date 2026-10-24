using System.Text.RegularExpressions;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Infrastructure.FileStorage;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>存储根目录（相对路径基于应用工作目录）。可通过环境变量 FileStorage__RootPath 覆盖。</summary>
    public string RootPath { get; set; } = "uploads";
}

/// <summary>
/// 本地磁盘文件存储：
/// - 存储键为随机 GUID + 安全扩展名，原始文件名仅用于展示；
/// - 读取时校验键格式，杜绝路径穿越；
/// - 数据库事务失败后由 Handler 调用 DeleteAsync 补偿清理。
/// </summary>
public partial class LocalFileStorageService : IFileStorageService
{
    [GeneratedRegex(@"^[0-9a-f]{32}(\.[a-z0-9]{1,8})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SafeStorageKeyRegex();

    [GeneratedRegex(@"^\.[a-z0-9]{1,10}$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SafeExtensionRegex();

    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(FileStorageSettings settings, ILogger<LocalFileStorageService> logger)
    {
        _rootPath = Path.GetFullPath(settings.RootPath);
        Directory.CreateDirectory(_rootPath);
        _logger = logger;
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = GetSafeExtension(originalFileName);
        var storageKey = Guid.NewGuid().ToString("N") + extension;
        var filePath = ResolveSafePath(storageKey);

        await using (var target = File.Create(filePath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var size = new FileInfo(filePath).Length;
        _logger.LogDebug("文件已保存：StorageKey={StorageKey}，Size={Size}", storageKey, size);
        return new StoredFile(storageKey, originalFileName, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, size);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SafeStorageKeyRegex().IsMatch(storageKey))
            {
                return Task.CompletedTask;
            }

            var filePath = ResolveSafePath(storageKey);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除文件失败：StorageKey={StorageKey}", storageKey);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!SafeStorageKeyRegex().IsMatch(storageKey))
        {
            throw new NotFoundException("附件");
        }

        var filePath = ResolveSafePath(storageKey);
        if (!File.Exists(filePath))
        {
            throw new NotFoundException("附件");
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    private static string GetSafeExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;
        return SafeExtensionRegex().IsMatch(extension) ? extension : string.Empty;
    }

    /// <summary>存储键已通过白名单正则校验，仅包含十六进制与受限扩展名，无路径分隔符。</summary>
    private string ResolveSafePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotFoundException("附件");
        }

        return fullPath;
    }
}
