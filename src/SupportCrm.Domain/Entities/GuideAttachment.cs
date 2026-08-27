namespace SupportCrm.Domain.Entities;

public class GuideAttachment
{
    public Guid Id { get; private set; }
    public Guid GuideId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private GuideAttachment() { } // EF Core

    public GuideAttachment(Guid guideId, string fileName, string contentType, long sizeBytes, string storageKey, string uploadedByName, DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(sizeBytes));

        Id = Guid.NewGuid();
        GuideId = guideId;
        FileName = fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByName = string.IsNullOrWhiteSpace(uploadedByName) ? "unknown" : uploadedByName;
        UploadedAtUtc = uploadedAtUtc;
    }
}
