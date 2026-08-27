namespace SupportCrm.Application.Platform;

public interface IBrandingAssetStorage
{
    Task<string> SaveAsync(string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
