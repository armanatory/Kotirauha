namespace Kotirauha.Core.Abstractions;

// Generates short "start writing" suggestions for the capture box, in the
// building's shared language, loosely informed by recent notes from the same
// building so they feel relevant (e.g. common topics and times of day).
public interface ISuggestionProvider
{
    string Name { get; }

    Task<IReadOnlyList<string>> SuggestEntryTextsAsync(
        string language,
        IReadOnlyList<string> recentExamples,
        CancellationToken ct = default);
}
