namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.KnowledgeBase;

public class LocalDiskArticleAttachmentStorageOptions
{
    public const string SectionName = "ArticleAttachments";
    public string RootPath { get; set; } = "App_Data/article-attachments";
}

public class LocalDiskArticleAttachmentStorage(IOptions<LocalDiskArticleAttachmentStorageOptions> options) : IArticleAttachmentStorage
{
    public async Task<string> SaveAsync(Guid articleId, Guid attachmentId, string fileName, Stream content, CancellationToken ct)
    {
        var articleDir = Path.Combine(options.Value.RootPath, articleId.ToString());
        Directory.CreateDirectory(articleDir);

        var storageFileName = $"{attachmentId}_{Path.GetFileName(fileName)}";
        var storageKey = Path.Combine(articleId.ToString(), storageFileName);
        var fullPath = Path.Combine(articleDir, storageFileName);

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
