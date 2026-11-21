namespace MoviesAndSeries.Services.Models;

public sealed record SeasonWatchResult(
    int SeasonNumber,
    bool IsWatched,
    bool SeriesIsWatched);
