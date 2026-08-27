namespace SupportCrm.Application.Customers;

public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
}
