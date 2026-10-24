namespace LeaveApproval.Application.Common.Interfaces;

public record StoredFile(string StorageKey, string OriginalFileName, string ContentType, long Size);

/// <summary>文件存储抽象。本地磁盘实现使用随机存储键，禁止路径穿越。</summary>
public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>尽力删除，文件不存在时不应抛出异常。</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
}
