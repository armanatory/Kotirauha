namespace Kotirauha.Core.Abstractions;

public record TranslationResult(string TranslatedText, string Provider, string Model);

public interface ITranslationProvider
{
    string Name { get; }

    Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default);
}
