using System.Net.Http.Json;
using System.Text.Json;
using Kotirauha.Core.Abstractions;

namespace Kotirauha.Infrastructure.Services;

public class AnthropicTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string Name => "anthropic";

    public AnthropicTranslationProvider(HttpClient http, string apiKey, string model)
    {
        _http = http;
        _model = model;
        _http.BaseAddress ??= new Uri("https://api.anthropic.com/");
        _http.DefaultRequestHeaders.Remove("x-api-key");
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    private const string SystemPrompt =
        "You are a faithful, neutral translator for a residential incident-documentation record. " +
        "Translate the user's text from the source language into the target language. " +
        "Preserve meaning and tone exactly. Do NOT soften, embellish, summarise, sanitise, censor, " +
        "or add interpretation or commentary. You may silently fix obvious spelling mistakes, nothing more. " +
        "Translate strong or emotional wording as written. " +
        "Never use an em dash; use a comma, period, or the word 'to' instead. " +
        "Output ONLY the translation, with no preamble, quotes, or notes.";

    public async Task<TranslationResult> TranslateAsync(
        string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            max_tokens = 1024,
            system = SystemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"Source language: {sourceLanguage}\nTarget language: {targetLanguage}\n\nText:\n{text}",
                },
            },
        };

        using var resp = await _http.PostAsJsonAsync("v1/messages", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var translated = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return new TranslationResult(translated.Trim(), Name, _model);
    }
}
