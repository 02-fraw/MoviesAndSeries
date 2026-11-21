using MoviesAndSeries.Models.Enums;

namespace MoviesAndSeries.ViewModels.Universes;

public sealed class UniverseContentItemViewModel
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public ContentType Type { get; init; }

    public int UniverseOrder { get; init; }

    public bool IsWatched { get; init; }

    public int SeasonCount { get; init; }

    public int WatchedSeasonCount { get; init; }

    public string TypeDisplayName =>
        Type == ContentType.Movie ? "Movie" : "Series";

    public bool IsFullyWatched =>
        Type == ContentType.Movie
            ? IsWatched
            : SeasonCount > 0 && WatchedSeasonCount == SeasonCount;
}
