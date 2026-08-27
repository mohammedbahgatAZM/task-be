namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.Customers;

public class LocalDiskAttachmentStorageOptions
{
    public const string SectionName = "Attachments";
    public string RootPath { get; set; } = "App_Data/attachments";
}

public class LocalDiskAttachmentStorage(IOptions<LocalDiskAttachmentStorageOptions> options) : IAttachmentStorage
{
    public async Task<string> SaveAsync(Guid customerId, Guid attachmentId, string fileName, Stream content, CancellationToken ct)
    {
        var customerDir = Path.Combine(options.Value.RootPath, customerId.ToString());
        Directory.CreateDirectory(customerDir);

        var storageFileName = $"{attachmentId}_{Path.GetFileName(fileName)}";
        var storageKey = Path.Combine(customerId.ToString(), storageFileName);
        var fullPath = Path.Combine(customerDir, storageFileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = Path.Combine(options.Value.RootPath, storageKey);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }
}
