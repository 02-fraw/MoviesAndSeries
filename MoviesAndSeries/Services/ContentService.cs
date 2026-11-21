using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Data;
using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Models.Enums;
using MoviesAndSeries.Services.Exceptions;
using MoviesAndSeries.Services.Interfaces;
using MoviesAndSeries.Services.Models;

namespace MoviesAndSeries.Services;

public sealed class ContentService : IContentService
{
    private const int MaximumTitleLength = 200;
    private const int MaximumSeasonCount = 25;

    private readonly ApplicationDbContext _context;

    public ContentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContentItem> CreateAsync(
        SaveContentRequest request,
        CancellationToken cancellationToken = default)
    {
        string title = NormalizeTitle(request.Title);
        int seasonCount = ValidateAndGetSeasonCount(request.Type, request.SeasonCount);

        var contentItem = new ContentItem
        {
            Title = title,
            Type = request.Type,
            IsWatched = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (request.UniverseId.HasValue)
        {
            int universeId = request.UniverseId.Value;

            await EnsureUniverseExistsAsync(universeId, cancellationToken);

            int currentItemCount = await _context.ContentItems
                .CountAsync(item => item.UniverseId == universeId, cancellationToken);

            int targetOrder = ResolveInsertOrder(request.UniverseOrder, currentItemCount);

            await MakeRoomForInsertAsync(universeId, targetOrder, cancellationToken);

            contentItem.UniverseId = universeId;
            contentItem.UniverseOrder = targetOrder;
        }

        if (request.Type == ContentType.Series)
        {
            AddNewSeasons(contentItem, 1, seasonCount);
        }

        _context.ContentItems.Add(contentItem);
        await _context.SaveChangesAsync(cancellationToken);

        return contentItem;
    }

    public async Task UpdateAsync(
        int contentItemId,
        SaveContentRequest request,
        CancellationToken cancellationToken = default)
    {
        ContentItem? contentItem = await _context.ContentItems
            .Include(item => item.Seasons)
            .SingleOrDefaultAsync(item => item.Id == contentItemId, cancellationToken);

        if (contentItem is null)
        {
            throw new BusinessRuleException("The requested movie or series was not found.");
        }

        string title = NormalizeTitle(request.Title);
        int seasonCount = ValidateAndGetSeasonCount(request.Type, request.SeasonCount);

        await ApplyUniverseAssignmentAsync(
            contentItem,
            request.UniverseId,
            request.UniverseOrder,
            cancellationToken);

        ApplyTypeAndSeasonChanges(contentItem, request.Type, seasonCount);
        contentItem.Title = title;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int contentItemId,
        CancellationToken cancellationToken = default)
    {
        ContentItem? contentItem = await _context.ContentItems
            .SingleOrDefaultAsync(item => item.Id == contentItemId, cancellationToken);

        if (contentItem is null)
        {
            throw new BusinessRuleException("The requested movie or series was not found.");
        }

        if (contentItem.UniverseId.HasValue && contentItem.UniverseOrder.HasValue)
        {
            await CloseOrderGapAsync(
                contentItem.UniverseId.Value,
                contentItem.UniverseOrder.Value,
                contentItem.Id,
                cancellationToken);
        }

        _context.ContentItems.Remove(contentItem);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveWithinUniverseAsync(
        int contentItemId,
        int targetOrder,
        CancellationToken cancellationToken = default)
    {
        ContentItem? contentItem = await _context.ContentItems
            .SingleOrDefaultAsync(item => item.Id == contentItemId, cancellationToken);

        if (contentItem is null)
        {
            throw new BusinessRuleException("The requested movie or series was not found.");
        }

        await MoveTrackedContentAsync(contentItem, targetOrder, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderUniverseAsync(
        int universeId,
        IReadOnlyList<int> orderedContentItemIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureUniverseExistsAsync(universeId, cancellationToken);

        List<ContentItem> universeItems = await _context.ContentItems
            .Where(item => item.UniverseId == universeId)
            .ToListAsync(cancellationToken);

        if (orderedContentItemIds.Count != universeItems.Count)
        {
            throw new BusinessRuleException(
                "The submitted order does not include all items in this universe.");
        }

        HashSet<int> receivedIds = orderedContentItemIds.ToHashSet();

        bool hasDuplicateOrMissing =
            receivedIds.Count != universeItems.Count ||
            universeItems.Any(item => !receivedIds.Contains(item.Id));

        if (hasDuplicateOrMissing)
        {
            throw new BusinessRuleException("The submitted content order is invalid.");
        }

        Dictionary<int, ContentItem> itemMap = universeItems.ToDictionary(item => item.Id);

        for (int index = 0; index < orderedContentItemIds.Count; index++)
        {
            int contentItemId = orderedContentItemIds[index];
            itemMap[contentItemId].UniverseOrder = index + 1;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFromUniverseAsync(
        int contentItemId,
        CancellationToken cancellationToken = default)
    {
        ContentItem? contentItem = await _context.ContentItems
            .SingleOrDefaultAsync(item => item.Id == contentItemId, cancellationToken);

        if (contentItem is null)
        {
            throw new BusinessRuleException("The requested content item was not found.");
        }

        if (!contentItem.UniverseId.HasValue || !contentItem.UniverseOrder.HasValue)
        {
            return;
        }

        await CloseOrderGapAsync(
            contentItem.UniverseId.Value,
            contentItem.UniverseOrder.Value,
            contentItem.Id,
            cancellationToken);

        contentItem.UniverseId = null;
        contentItem.UniverseOrder = null;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SetMovieWatchedAsync(
        int contentItemId,
        bool isWatched,
        CancellationToken cancellationToken = default)
    {
        ContentItem? movie = await _context.ContentItems
            .SingleOrDefaultAsync(
                item => item.Id == contentItemId && item.Type == ContentType.Movie,
                cancellationToken);

        if (movie is null)
        {
            throw new BusinessRuleException("Movie not found.");
        }

        movie.IsWatched = isWatched;
        await _context.SaveChangesAsync(cancellationToken);

        return movie.IsWatched;
    }

    public async Task<SeasonWatchResult> SetSeasonWatchedAsync(
        int seasonId,
        bool isWatched,
        CancellationToken cancellationToken = default)
    {
        Season? season = await _context.Seasons
            .Include(item => item.ContentItem)
            .ThenInclude(contentItem => contentItem.Seasons)
            .SingleOrDefaultAsync(item => item.Id == seasonId, cancellationToken);

        if (season is null || season.ContentItem.Type != ContentType.Series)
        {
            throw new BusinessRuleException("Series season not found.");
        }

        season.IsWatched = isWatched;

        season.ContentItem.IsWatched =
            season.ContentItem.Seasons.Count > 0 &&
            season.ContentItem.Seasons.All(item => item.IsWatched);

        await _context.SaveChangesAsync(cancellationToken);

        return new SeasonWatchResult(
            season.SeasonNumber,
            season.IsWatched,
            season.ContentItem.IsWatched);
    }

    private async Task ApplyUniverseAssignmentAsync(
        ContentItem contentItem,
        int? requestedUniverseId,
        int? requestedUniverseOrder,
        CancellationToken cancellationToken)
    {
        if (requestedUniverseId.HasValue)
        {
            await EnsureUniverseExistsAsync(requestedUniverseId.Value, cancellationToken);
        }

        int? currentUniverseId = contentItem.UniverseId;
        int? currentUniverseOrder = contentItem.UniverseOrder;

        if (!requestedUniverseId.HasValue)
        {
            if (currentUniverseId.HasValue && currentUniverseOrder.HasValue)
            {
                await CloseOrderGapAsync(
                    currentUniverseId.Value,
                    currentUniverseOrder.Value,
                    contentItem.Id,
                    cancellationToken);
            }

            contentItem.UniverseId = null;
            contentItem.UniverseOrder = null;
            return;
        }

        int newUniverseId = requestedUniverseId.Value;

        if (currentUniverseId == newUniverseId)
        {
            if (!currentUniverseOrder.HasValue)
            {
                int itemCount = await _context.ContentItems
                    .CountAsync(
                        item => item.UniverseId == newUniverseId && item.Id != contentItem.Id,
                        cancellationToken);

                int insertOrder = ResolveInsertOrder(requestedUniverseOrder, itemCount);
                await MakeRoomForInsertAsync(newUniverseId, insertOrder, cancellationToken);
                contentItem.UniverseOrder = insertOrder;
                return;
            }

            int currentItemCount = await _context.ContentItems
                .CountAsync(item => item.UniverseId == newUniverseId, cancellationToken);

            int targetOrder = requestedUniverseOrder ?? currentUniverseOrder.Value;
            ValidateExistingOrder(targetOrder, currentItemCount);
            await MoveTrackedContentAsync(contentItem, targetOrder, cancellationToken);
            return;
        }

        if (currentUniverseId.HasValue && currentUniverseOrder.HasValue)
        {
            await CloseOrderGapAsync(
                currentUniverseId.Value,
                currentUniverseOrder.Value,
                contentItem.Id,
                cancellationToken);
        }

        int newUniverseItemCount = await _context.ContentItems
            .CountAsync(item => item.UniverseId == newUniverseId, cancellationToken);

        int newOrder = ResolveInsertOrder(requestedUniverseOrder, newUniverseItemCount);
        await MakeRoomForInsertAsync(newUniverseId, newOrder, cancellationToken);

        contentItem.UniverseId = newUniverseId;
        contentItem.UniverseOrder = newOrder;
    }

    private async Task MoveTrackedContentAsync(
        ContentItem contentItem,
        int targetOrder,
        CancellationToken cancellationToken)
    {
        if (!contentItem.UniverseId.HasValue || !contentItem.UniverseOrder.HasValue)
        {
            throw new BusinessRuleException("Standalone content cannot be reordered within a universe.");
        }

        int universeId = contentItem.UniverseId.Value;
        int currentOrder = contentItem.UniverseOrder.Value;

        int itemCount = await _context.ContentItems
            .CountAsync(item => item.UniverseId == universeId, cancellationToken);

        ValidateExistingOrder(targetOrder, itemCount);

        if (targetOrder == currentOrder)
        {
            return;
        }

        if (targetOrder < currentOrder)
        {
            List<ContentItem> affectedItems = await _context.ContentItems
                .Where(item =>
                    item.Id != contentItem.Id &&
                    item.UniverseId == universeId &&
                    item.UniverseOrder.HasValue &&
                    item.UniverseOrder.Value >= targetOrder &&
                    item.UniverseOrder.Value < currentOrder)
                .ToListAsync(cancellationToken);

            foreach (ContentItem item in affectedItems)
            {
                item.UniverseOrder = item.UniverseOrder!.Value + 1;
            }
        }
        else
        {
            List<ContentItem> affectedItems = await _context.ContentItems
                .Where(item =>
                    item.Id != contentItem.Id &&
                    item.UniverseId == universeId &&
                    item.UniverseOrder.HasValue &&
                    item.UniverseOrder.Value > currentOrder &&
                    item.UniverseOrder.Value <= targetOrder)
                .ToListAsync(cancellationToken);

            foreach (ContentItem item in affectedItems)
            {
                item.UniverseOrder = item.UniverseOrder!.Value - 1;
            }
        }

        contentItem.UniverseOrder = targetOrder;
    }

    private async Task MakeRoomForInsertAsync(
        int universeId,
        int targetOrder,
        CancellationToken cancellationToken)
    {
        List<ContentItem> affectedItems = await _context.ContentItems
            .Where(item =>
                item.UniverseId == universeId &&
                item.UniverseOrder.HasValue &&
                item.UniverseOrder.Value >= targetOrder)
            .ToListAsync(cancellationToken);

        foreach (ContentItem item in affectedItems)
        {
            item.UniverseOrder = item.UniverseOrder!.Value + 1;
        }
    }

    private async Task CloseOrderGapAsync(
        int universeId,
        int removedOrder,
        int excludedContentItemId,
        CancellationToken cancellationToken)
    {
        List<ContentItem> affectedItems = await _context.ContentItems
            .Where(item =>
                item.Id != excludedContentItemId &&
                item.UniverseId == universeId &&
                item.UniverseOrder.HasValue &&
                item.UniverseOrder.Value > removedOrder)
            .ToListAsync(cancellationToken);

        foreach (ContentItem item in affectedItems)
        {
            item.UniverseOrder = item.UniverseOrder!.Value - 1;
        }
    }

    private void ApplyTypeAndSeasonChanges(
        ContentItem contentItem,
        ContentType newType,
        int seasonCount)
    {
        if (contentItem.Type == newType)
        {
            if (newType == ContentType.Series)
            {
                SynchronizeSeasons(contentItem, seasonCount);
            }

            return;
        }

        List<Season> oldSeasons = contentItem.Seasons.ToList();

        if (oldSeasons.Count > 0)
        {
            _context.Seasons.RemoveRange(oldSeasons);
            contentItem.Seasons.Clear();
        }

        contentItem.Type = newType;
        contentItem.IsWatched = false;

        if (newType == ContentType.Series)
        {
            AddNewSeasons(contentItem, 1, seasonCount);
        }
    }

    private void SynchronizeSeasons(
        ContentItem contentItem,
        int requestedSeasonCount)
    {
        HashSet<int> existingNumbers = contentItem.Seasons
            .Select(season => season.SeasonNumber)
            .ToHashSet();

        for (int seasonNumber = 1; seasonNumber <= requestedSeasonCount; seasonNumber++)
        {
            if (!existingNumbers.Contains(seasonNumber))
            {
                contentItem.Seasons.Add(new Season
                {
                    SeasonNumber = seasonNumber,
                    IsWatched = false
                });
            }
        }

        List<Season> seasonsToDelete = contentItem.Seasons
            .Where(season => season.SeasonNumber > requestedSeasonCount)
            .ToList();

        if (seasonsToDelete.Count > 0)
        {
            foreach (Season season in seasonsToDelete)
            {
                contentItem.Seasons.Remove(season);
            }

            _context.Seasons.RemoveRange(seasonsToDelete);
        }

        contentItem.IsWatched =
            contentItem.Seasons.Count > 0 &&
            contentItem.Seasons.All(season => season.IsWatched);
    }

    private static void AddNewSeasons(
        ContentItem contentItem,
        int firstSeasonNumber,
        int lastSeasonNumber)
    {
        for (int seasonNumber = firstSeasonNumber; seasonNumber <= lastSeasonNumber; seasonNumber++)
        {
            contentItem.Seasons.Add(new Season
            {
                SeasonNumber = seasonNumber,
                IsWatched = false
            });
        }
    }

    private async Task EnsureUniverseExistsAsync(
        int universeId,
        CancellationToken cancellationToken)
    {
        bool universeExists = await _context.Universes
            .AnyAsync(universe => universe.Id == universeId, cancellationToken);

        if (!universeExists)
        {
            throw new BusinessRuleException("The selected universe was not found.");
        }
    }

    private static string NormalizeTitle(string? title)
    {
        string normalizedTitle = title?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new BusinessRuleException("Title is required.");
        }

        if (normalizedTitle.Length > MaximumTitleLength)
        {
            throw new BusinessRuleException(
                $"Title cannot exceed {MaximumTitleLength} characters.");
        }

        return normalizedTitle;
    }

    private static int ValidateAndGetSeasonCount(
        ContentType contentType,
        int? seasonCount)
    {
        if (!Enum.IsDefined(typeof(ContentType), contentType))
        {
            throw new BusinessRuleException("Invalid content type.");
        }

        if (contentType == ContentType.Movie)
        {
            return 0;
        }

        if (!seasonCount.HasValue ||
            seasonCount.Value < 1 ||
            seasonCount.Value > MaximumSeasonCount)
        {
            throw new BusinessRuleException(
                $"Season count must be between 1 and {MaximumSeasonCount}.");
        }

        return seasonCount.Value;
    }

    private static int ResolveInsertOrder(
        int? requestedOrder,
        int currentItemCount)
    {
        int maximumOrder = currentItemCount + 1;
        int targetOrder = requestedOrder ?? maximumOrder;

        if (targetOrder < 1 || targetOrder > maximumOrder)
        {
            throw new BusinessRuleException(
                $"Watch order must be between 1 and {maximumOrder}.");
        }

        return targetOrder;
    }

    private static void ValidateExistingOrder(
        int targetOrder,
        int currentItemCount)
    {
        if (targetOrder < 1 || targetOrder > currentItemCount)
        {
            throw new BusinessRuleException(
                $"Target position must be between 1 and {currentItemCount}.");
        }
    }
}
