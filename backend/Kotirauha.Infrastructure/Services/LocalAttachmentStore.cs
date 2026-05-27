using Kotirauha.Core.Abstractions;

namespace Kotirauha.Infrastructure.Services;

public class LocalAttachmentStore : IAttachmentStore
{
    private readonly string _root;

    public LocalAttachmentStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin",
        };
        var key = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(_root, key);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Path.Combine(_root, storageKey);
        if (!File.Exists(path)) return Task.FromResult<(Stream, string)?>(null);

        var contentType = Path.GetExtension(path) switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
        Stream content = File.OpenRead(path);
        return Task.FromResult<(Stream, string)?>((content, contentType));
    }
}
