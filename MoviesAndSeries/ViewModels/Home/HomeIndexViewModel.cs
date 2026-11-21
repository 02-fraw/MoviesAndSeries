using MoviesAndSeries.Models.Enums;

namespace MoviesAndSeries.ViewModels.Home;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<HomeContentItemViewModel> ContentItems { get; init; } = [];

    public IReadOnlyList<HomeUniverseOptionViewModel> Universes { get; init; } = [];

    public int TotalCount { get; init; }

    public int MovieCount { get; init; }

    public int SeriesCount { get; init; }

    public int UniverseCount { get; init; }
}

public sealed class HomeContentItemViewModel
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public ContentType Type { get; init; }

    public bool IsWatched { get; init; }

    public int? UniverseId { get; init; }

    public string? UniverseName { get; init; }

    public string? UniverseColorHex { get; init; }

    public int? UniverseOrder { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public IReadOnlyList<HomeSeasonViewModel> Seasons { get; init; } = [];
}

public sealed class HomeSeasonViewModel
{
    public int Id { get; init; }

    public int SeasonNumber { get; init; }

    public bool IsWatched { get; init; }
}

public sealed class HomeUniverseOptionViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;
}
