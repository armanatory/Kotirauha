namespace Kotirauha.Core.Abstractions;

// Best-effort guess of what a free-text note is about. Either field may be null
// when the text is too vague. Category is an IncidentCategory name; Location is
// a capture-page location key (stairwell, corridor, yard, basement, apartment, other).
public record EntryClassification(string? Category, string? Location);

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

    // Detect the likely category and location from what the resident has typed.
    Task<EntryClassification> ClassifyAsync(string text, CancellationToken ct = default);

    // A friendly, neutral, anonymous display nickname in the given language
    // (used so neighbours see a handle rather than a real name).
    Task<string> SuggestNicknameAsync(string language, CancellationToken ct = default);
}
