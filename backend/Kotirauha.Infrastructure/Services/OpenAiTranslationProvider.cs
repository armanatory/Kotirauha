using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kotirauha.Core.Abstractions;

namespace Kotirauha.Infrastructure.Services;

public class OpenAiTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string Name => "openai";

    public OpenAiTranslationProvider(HttpClient http, string apiKey, string model)
    {
        _http = http;
        _model = model;
        _http.BaseAddress ??= new Uri("https://api.openai.com/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    private const string SystemPrompt =
        "You translate a neighbour's incident note between English and Finnish for a building's shared record. " +
        "Translate faithfully and plainly. Keep the meaning, facts and tone exactly. " +
        "Do NOT make it more formal, fluffy, or elaborate; do NOT add, remove, or reinterpret anything. " +
        "You may silently fix obvious spelling or typing mistakes, nothing more. " +
        "Never use an em dash (—); use a comma, period, or the word 'to' instead. " +
        "Output only the translated text, with no quotes, labels, or commentary.";

    public async Task<TranslationResult> TranslateAsync(
        string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Translate from {sourceLanguage} to {targetLanguage}:\n\n{text}" },
            },
        };

        using var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var translated = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        // Belt-and-braces: never let an em dash through.
        translated = translated.Replace("—", ", ").Replace("–", "-").Trim();

        return new TranslationResult(translated, Name, _model);
    }
}
