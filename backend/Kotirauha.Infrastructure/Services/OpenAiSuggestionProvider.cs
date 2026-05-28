using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kotirauha.Core.Abstractions;

namespace Kotirauha.Infrastructure.Services;

public class OpenAiSuggestionProvider : ISuggestionProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string Name => "openai";

    public OpenAiSuggestionProvider(HttpClient http, string apiKey, string model)
    {
        _http = http;
        _model = model;
        _http.BaseAddress ??= new Uri("https://api.openai.com/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    private const string SystemPrompt =
        "You help a resident of a Finnish apartment building start writing a short incident note quickly. " +
        "You are given recent notes from this same building. Produce six short note starters the resident can tap and then edit. " +
        "Write every suggestion in the requested language. " +
        "Each suggestion is one plain, first-person, factual observation, at most about twelve words. " +
        "Keep them neutral and non-accusatory. Do NOT invent names, apartment numbers, dates, or specific people. " +
        "Cover a realistic spread of everyday topics (noise, smell, smoking, parking, safety, shared spaces) and reflect typical times of day seen in the examples. " +
        "Never use an em dash (—); use a comma or period instead. " +
        "Output only the suggestions, one per line, with no numbering, bullets, or quotes.";

    public async Task<IReadOnlyList<string>> SuggestEntryTextsAsync(
        string language, IReadOnlyList<string> recentExamples, CancellationToken ct = default)
    {
        var langName = language == "en" ? "English" : "Finnish";
        var examples = recentExamples.Count > 0
            ? "Recent notes from this building:\n" + string.Join("\n", recentExamples.Select(e => "- " + e))
            : "There are no recent notes yet. Suggest common, everyday building observations.";

        var payload = new
        {
            model = _model,
            temperature = 0.8,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Language: {langName}.\n\n{examples}" },
            },
        };

        using var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return Parse(content);
    }

    private static IReadOnlyList<string> Parse(string content) =>
        content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', '•', ' ').Trim())
            .Select(line =>
            {
                // Drop a leading "1." / "2)" style index if the model added one.
                var dot = line.IndexOfAny(['.', ')']);
                if (dot is > 0 and <= 2 && line[..dot].All(char.IsDigit)) line = line[(dot + 1)..].Trim();
                return line.Trim('"', '\'').Replace("—", ", ").Replace("–", "-").Trim();
            })
            .Where(line => line.Length is > 0 and <= 140)
            .Take(6)
            .ToList();
}
