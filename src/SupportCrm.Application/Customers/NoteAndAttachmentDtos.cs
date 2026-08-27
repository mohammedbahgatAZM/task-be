namespace SupportCrm.Application.Customers;

public record AddNoteRequest(string Text, string AuthorName, bool IsPinned);
public record SetNotePinnedRequest(bool IsPinned);

public record NoteDto(Guid Id, string Text, string AuthorName, bool IsPinned, DateTimeOffset CreatedAtUtc);

public record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);

public class AttachmentTooLargeException(long sizeBytes, long maxSizeBytes)
    : Exception($"Attachment size {sizeBytes} bytes exceeds the configured limit of {maxSizeBytes} bytes.");
