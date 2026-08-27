namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.Platform;

public class LocalDiskBrandingAssetStorageOptions
{
    public const string SectionName = "BrandingAssets";
    public string RootPath { get; set; } = "App_Data/branding-assets";
}

public class LocalDiskBrandingAssetStorage(IOptions<LocalDiskBrandingAssetStorageOptions> options) : IBrandingAssetStorage
{
    public async Task<string> SaveAsync(string fileName, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(options.Value.RootPath);
        var storageKey = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(options.Value.RootPath, storageKey);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(options.Value.RootPath, storageKey)));
}
