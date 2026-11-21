namespace MoviesAndSeries.ViewModels.Management;

public sealed class ManagementIndexViewModel
{
    public IReadOnlyList<ManagementContentItemViewModel> ContentItems { get; init; } = [];

    public IReadOnlyList<UniverseOptionViewModel> Universes { get; init; } = [];

    public IReadOnlyList<int> SeasonOptions { get; init; } = [];

    public int TotalContentCount { get; init; }

    public int MovieCount { get; init; }

    public int SeriesCount { get; init; }

    public int UniverseCount { get; init; }
}
