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

    private static readonly string[] Categories =
        ["Noise", "Smell", "SmokingOrIncense", "Parking", "SafetyConcern", "CommonAreaMisuse", "Other"];
    private static readonly string[] Locations =
        ["stairwell", "corridor", "yard", "basement", "apartment", "other"];

    private const string ClassifyPrompt =
        "You read a resident's short note about a building incident (English or Finnish) and label it. " +
        "Reply ONLY with JSON: {\"category\": <one of " +
        "Noise, Smell, SmokingOrIncense, Parking, SafetyConcern, CommonAreaMisuse, Other>, " +
        "\"location\": <one of stairwell, corridor, yard, basement, apartment, other>}. " +
        "Pick the single best fit. If a field is genuinely unclear, use \"Other\" / \"other\".";

    public async Task<EntryClassification> ClassifyAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return new EntryClassification(null, null);

        var payload = new
        {
            model = _model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = ClassifyPrompt },
                new { role = "user", content = text },
            },
        };

        using var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) return new EntryClassification(null, null);

        using var parsed = JsonDocument.Parse(content);
        var cat = parsed.RootElement.TryGetProperty("category", out var c) ? c.GetString() : null;
        var loc = parsed.RootElement.TryGetProperty("location", out var l) ? l.GetString() : null;

        cat = Categories.FirstOrDefault(x => string.Equals(x, cat, StringComparison.OrdinalIgnoreCase));
        loc = Locations.FirstOrDefault(x => string.Equals(x, loc, StringComparison.OrdinalIgnoreCase));
        return new EntryClassification(cat, loc);
    }

    private const string NicknamePrompt =
        "Invent ONE friendly, neutral, anonymous nickname for a resident of an apartment building, " +
        "so neighbours see a handle instead of a real name. Write it in the requested language. " +
        "Two short words at most, suitable for all ages, never a real personal name, no numbers, no quotes, no punctuation. " +
        "Reply with just the nickname.";

    public async Task<string> SuggestNicknameAsync(string language, CancellationToken ct = default)
    {
        var langName = language == "en" ? "English" : "Finnish";
        var payload = new
        {
            model = _model,
            temperature = 1.0,
            messages = new object[]
            {
                new { role = "system", content = NicknamePrompt },
                new { role = "user", content = $"Language: {langName}." },
            },
        };

        using var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var name = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        name = name.Trim().Trim('"', '\'', '.').Replace("\n", " ").Trim();
        return name.Length is > 0 and <= 40 ? name : "";
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
