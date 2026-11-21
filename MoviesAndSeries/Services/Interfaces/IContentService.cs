using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Services.Models;

namespace MoviesAndSeries.Services.Interfaces;

public interface IContentService
{
    Task<ContentItem> CreateAsync(
        SaveContentRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        int contentItemId,
        SaveContentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int contentItemId,
        CancellationToken cancellationToken = default);

    Task MoveWithinUniverseAsync(
        int contentItemId,
        int targetOrder,
        CancellationToken cancellationToken = default);

    Task ReorderUniverseAsync(
        int universeId,
        IReadOnlyList<int> orderedContentItemIds,
        CancellationToken cancellationToken = default);

    Task RemoveFromUniverseAsync(
        int contentItemId,
        CancellationToken cancellationToken = default);

    Task<bool> SetMovieWatchedAsync(
        int contentItemId,
        bool isWatched,
        CancellationToken cancellationToken = default);

    Task<SeasonWatchResult> SetSeasonWatchedAsync(
        int seasonId,
        bool isWatched,
        CancellationToken cancellationToken = default);
}
