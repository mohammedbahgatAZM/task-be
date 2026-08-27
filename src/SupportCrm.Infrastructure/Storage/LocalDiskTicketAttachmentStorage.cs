namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.Tickets;

public class LocalDiskTicketAttachmentStorageOptions
{
    public const string SectionName = "TicketAttachments";
    public string RootPath { get; set; } = "App_Data/ticket-attachments";
}

public class LocalDiskTicketAttachmentStorage(IOptions<LocalDiskTicketAttachmentStorageOptions> options) : ITicketAttachmentStorage
{
    public async Task<string> SaveAsync(Guid ticketId, Guid attachmentId, string fileName, Stream content, CancellationToken ct)
    {
        var ticketDir = Path.Combine(options.Value.RootPath, ticketId.ToString());
        Directory.CreateDirectory(ticketDir);

        var storageFileName = $"{attachmentId}_{Path.GetFileName(fileName)}";
        var storageKey = Path.Combine(ticketId.ToString(), storageFileName);
        var fullPath = Path.Combine(ticketDir, storageFileName);

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
