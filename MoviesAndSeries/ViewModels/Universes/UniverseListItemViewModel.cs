namespace MoviesAndSeries.ViewModels.Universes;

public sealed class UniverseListItemViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ColorHex { get; init; } = "#7C3AED";

    public int ContentCount { get; init; }

    public int MovieCount { get; init; }

    public int SeriesCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
