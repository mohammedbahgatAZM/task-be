namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.KnowledgeBase;

public class LocalDiskGuideAttachmentStorageOptions
{
    public const string SectionName = "GuideAttachments";
    public string RootPath { get; set; } = "App_Data/guide-attachments";
}

public class LocalDiskGuideAttachmentStorage(IOptions<LocalDiskGuideAttachmentStorageOptions> options) : IGuideAttachmentStorage
{
    public async Task<string> SaveAsync(Guid guideId, Guid attachmentId, string fileName, Stream content, CancellationToken ct)
    {
        var guideDir = Path.Combine(options.Value.RootPath, guideId.ToString());
        Directory.CreateDirectory(guideDir);

        var storageFileName = $"{attachmentId}_{Path.GetFileName(fileName)}";
        var storageKey = Path.Combine(guideId.ToString(), storageFileName);
        var fullPath = Path.Combine(guideDir, storageFileName);

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
