using MoviesAndSeries.Models.Enums;

namespace MoviesAndSeries.ViewModels.Management;

public sealed class ManagementContentItemViewModel
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public ContentType Type { get; init; }

    public bool IsWatched { get; init; }

    public int SeasonCount { get; init; }

    public int WatchedSeasonCount { get; init; }

    public int? UniverseId { get; init; }

    public string? UniverseName { get; init; }

    public string? UniverseColorHex { get; init; }

    public int? UniverseOrder { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string TypeDisplayName =>
        Type == ContentType.Movie ? "Movie" : "Series";

    public bool IsFullyWatched =>
        Type == ContentType.Movie
            ? IsWatched
            : SeasonCount > 0 && WatchedSeasonCount == SeasonCount;
}
