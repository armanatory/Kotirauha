using Kotirauha.Core.Abstractions;

namespace Kotirauha.Infrastructure.Services;

// Used when no AI key is configured. Returns a small set of generic, neutral
// starters in the building language so the tap-to-start flow still works.
public class StubSuggestionProvider : ISuggestionProvider
{
    private static readonly string[] Fi =
    [
        "Häiritsevää melua naapurista ilta-aikaan.",
        "Rappukäytävässä on epämiellyttävä haju.",
        "Tupakansavua kantautuu parvekkeelle.",
        "Vieras auto pihan pysäköintipaikalla.",
        "Ulko-ovi jää toistuvasti auki.",
        "Roskat eivät mahdu jäteastiaan.",
    ];

    private static readonly string[] En =
    [
        "Disturbing noise from a neighbour in the evening.",
        "An unpleasant smell in the stairwell.",
        "Cigarette smoke drifting onto the balcony.",
        "An unfamiliar car in the yard parking spot.",
        "The front door is repeatedly left open.",
        "Rubbish does not fit in the waste bin.",
    ];

    public string Name => "stub";

    public Task<IReadOnlyList<string>> SuggestEntryTextsAsync(
        string language, IReadOnlyList<string> recentExamples, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(language == "en" ? En : Fi);
}
