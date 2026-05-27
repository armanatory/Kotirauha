using Kotirauha.Core.Abstractions;

namespace Kotirauha.Infrastructure.Services;

// Used when no real translation API key is configured (local dev, tests, CI).
// Produces a clearly-marked placeholder so the flow works end to end without cost.
public class StubTranslationProvider : ITranslationProvider
{
    public string Name => "stub";

    public Task<TranslationResult> TranslateAsync(
        string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default)
    {
        var translated = $"[{targetLanguage}] {text}";
        return Task.FromResult(new TranslationResult(translated, Name, "stub-echo"));
    }
}
