namespace SupportCrm.Domain.Entities;

public class ArticleAttachment
{
    public Guid Id { get; private set; }
    public Guid ArticleId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private ArticleAttachment() { } // EF Core

    public ArticleAttachment(Guid articleId, string fileName, string contentType, long sizeBytes, string storageKey, string uploadedByName, DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(sizeBytes));

        Id = Guid.NewGuid();
        ArticleId = articleId;
        FileName = fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByName = string.IsNullOrWhiteSpace(uploadedByName) ? "unknown" : uploadedByName;
        UploadedAtUtc = uploadedAtUtc;
    }
}
