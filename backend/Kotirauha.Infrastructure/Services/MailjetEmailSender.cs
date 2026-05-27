using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Kotirauha.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kotirauha.Infrastructure.Services;

public class MailjetEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<MailjetEmailSender> _logger;

    public MailjetEmailSender(HttpClient http, string apiKey, string apiSecret, string fromEmail, string fromName, ILogger<MailjetEmailSender> logger)
    {
        _http = http;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _logger = logger;
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken ct = default)
    {
        var payload = new
        {
            Messages = new[]
            {
                new
                {
                    From = new { Email = _fromEmail, Name = _fromName },
                    To = new[] { new { Email = toEmail } },
                    Subject = subject,
                    TextPart = textBody,
                    HTMLPart = htmlBody,
                },
            },
        };

        using var resp = await _http.PostAsJsonAsync("https://api.mailjet.com/v3.1/send", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("Mailjet send failed ({Status}): {Body}", resp.StatusCode, body);
            throw new InvalidOperationException($"Email send failed: {resp.StatusCode}");
        }
    }
}

// Used when Mailjet is not configured (local dev / tests). Logs instead of sending.
public class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;
    public NoOpEmailSender(ILogger<NoOpEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken ct = default)
    {
        _logger.LogInformation("[NoOpEmail] To={To} Subject={Subject}\n{Text}", toEmail, subject, textBody);
        return Task.CompletedTask;
    }
}
