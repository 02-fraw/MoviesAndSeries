namespace MoviesAndSeries.ViewModels.Universes;

public sealed class UniverseIndexViewModel
{
    public IReadOnlyList<UniverseListItemViewModel> Universes { get; init; } = [];

    public int UniverseCount { get; init; }

    public int TotalContentCount { get; init; }

    public int MovieCount { get; init; }

    public int SeriesCount { get; init; }
}
