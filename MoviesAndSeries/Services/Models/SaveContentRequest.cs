using MoviesAndSeries.Models.Enums;

namespace MoviesAndSeries.Services.Models;

public sealed class SaveContentRequest
{
    public string Title { get; init; } = string.Empty;

    public ContentType Type { get; init; } = ContentType.Movie;

    public int? SeasonCount { get; init; }

    public int? UniverseId { get; init; }

    public int? UniverseOrder { get; init; }
}
