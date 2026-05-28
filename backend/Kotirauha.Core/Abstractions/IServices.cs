using Kotirauha.Core.Domain;

namespace Kotirauha.Core.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken ct = default);
}

public interface IJwtTokenService
{
    string CreateToken(User user);
}

public interface IAttachmentStore
{
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default);
    Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
