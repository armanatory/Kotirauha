using Kotirauha.Core.Domain;

namespace Kotirauha.Core.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IJwtTokenService
{
    string CreateToken(User user);
}

public interface IAttachmentStore
{
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default);
    Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default);
}
