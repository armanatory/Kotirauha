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

    // Keyword heuristics (EN + FI) used when no AI key is configured.
    public Task<EntryClassification> ClassifyAsync(string text, CancellationToken ct = default)
    {
        var t = (text ?? "").ToLowerInvariant();
        bool Has(params string[] words) => words.Any(w => t.Contains(w));

        string? category =
            Has("smell", "haju", "odor", "stink", "lemu") ? "Smell"
            : Has("smoke", "smoking", "cigarette", "tupak", "savu", "suitsuke", "incense") ? "SmokingOrIncense"
            : Has("park", "auto", "car", "pysäk", "ajoneuvo") ? "Parking"
            : Has("noise", "loud", "music", "melu", "ääni", "musiikki", "remont") ? "Noise"
            : Has("danger", "unsafe", "fire", "threat", "vaara", "turvall", "uhka", "palo") ? "SafetyConcern"
            : Has("laundry", "trash", "rubbish", "garbage", "elevator", "pyykki", "roska", "jäte", "hissi", "yhteis") ? "CommonAreaMisuse"
            : null;

        string? location =
            Has("stair", "rappu", "porras", "portai") ? "stairwell"
            : Has("corridor", "hallway", "käytävä") ? "corridor"
            : Has("yard", "parking lot", "piha", "parkki", "pysäköinti") ? "yard"
            : Has("basement", "cellar", "laundry", "kellari", "pyykki", "varasto") ? "basement"
            : Has("apartment", "flat", "door", "asunto", "asunnon", "ovi", "naapuri") ? "apartment"
            : null;

        return Task.FromResult(new EntryClassification(category, location));
    }

    private static readonly string[] NickFi =
        ["Rauhallinen naapuri", "Hiljainen asukas", "Ystävällinen naapuri", "Tyyni asukas", "Huomaavainen naapuri", "Kohtelias asukas"];
    private static readonly string[] NickEn =
        ["Quiet Neighbour", "Friendly Resident", "Calm Neighbour", "Kind Resident", "Thoughtful Neighbour", "Polite Resident"];

    public Task<string> SuggestNicknameAsync(string language, CancellationToken ct = default)
    {
        var pool = language == "en" ? NickEn : NickFi;
        return Task.FromResult(pool[Random.Shared.Next(pool.Length)]);
    }
}
