using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Data;
using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Models.Enums;
using MoviesAndSeries.Services.Exceptions;
using MoviesAndSeries.Services.Interfaces;
using MoviesAndSeries.Services.Models;
using MoviesAndSeries.ViewModels.Management;

namespace MoviesAndSeries.Controllers;

public sealed partial class ManagementController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IContentService _contentService;

    public ManagementController(
        ApplicationDbContext context,
        IContentService contentService)
    {
        _context = context;
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        List<ManagementContentItemViewModel> contentItems = await _context.ContentItems
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new ManagementContentItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Type = item.Type,
                IsWatched = item.IsWatched,
                SeasonCount = item.Seasons.Count(),
                WatchedSeasonCount = item.Seasons.Count(season => season.IsWatched),
                UniverseId = item.UniverseId,
                UniverseName = item.Universe == null ? null : item.Universe.Name,
                UniverseColorHex = item.Universe == null ? null : item.Universe.ColorHex,
                UniverseOrder = item.UniverseOrder,
                CreatedAtUtc = item.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        contentItems = contentItems
            .Select(item => new ManagementContentItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Type = item.Type,
                IsWatched = item.IsWatched,
                SeasonCount = item.SeasonCount,
                WatchedSeasonCount = item.WatchedSeasonCount,
                UniverseId = item.UniverseId,
                UniverseName = item.UniverseName,
                UniverseColorHex = SanitizeHexColor(item.UniverseColorHex),
                UniverseOrder = item.UniverseOrder,
                CreatedAtUtc = item.CreatedAtUtc
            })
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();

        List<UniverseOptionViewModel> universes = await _context.Universes
            .AsNoTracking()
            .OrderBy(universe => universe.Name)
            .Select(universe => new UniverseOptionViewModel
            {
                Id = universe.Id,
                Name = universe.Name,
                ColorHex = universe.ColorHex,
                ContentCount = universe.ContentItems.Count
            })
            .ToListAsync(cancellationToken);

        var viewModel = new ManagementIndexViewModel
        {
            ContentItems = contentItems,
            Universes = universes,
            SeasonOptions = Enumerable.Range(1, 25).ToArray(),
            TotalContentCount = contentItems.Count,
            MovieCount = contentItems.Count(item => item.Type == ContentType.Movie),
            SeriesCount = contentItems.Count(item => item.Type == ContentType.Series),
            UniverseCount = universes.Count
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetContent(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return BadRequest(new { success = false, message = "Invalid content identifier." });
        }

        var contentItem = await _context.ContentItems
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                id = item.Id,
                title = item.Title,
                type = (int)item.Type,
                seasonCount = item.Seasons.Count(),
                universeId = item.UniverseId,
                universeOrder = item.UniverseOrder
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (contentItem is null)
        {
            return NotFound(new { success = false, message = "Movie or series not found." });
        }

        return Json(new { success = true, contentItem });
    }

    [HttpGet]
    public async Task<IActionResult> GetUniverseOrderOptions(
        int universeId,
        int? contentItemId,
        CancellationToken cancellationToken)
    {
        if (universeId < 1)
        {
            return BadRequest(new { success = false, message = "Invalid universe identifier." });
        }

        bool universeExists = await _context.Universes
            .AsNoTracking()
            .AnyAsync(universe => universe.Id == universeId, cancellationToken);

        if (!universeExists)
        {
            return NotFound(new { success = false, message = "Selected universe not found." });
        }

        var universeItems = await _context.ContentItems
            .AsNoTracking()
            .Where(item => item.UniverseId == universeId)
            .OrderBy(item => item.UniverseOrder)
            .ThenBy(item => item.Id)
            .Select(item => new
            {
                id = item.Id,
                title = item.Title,
                type = (int)item.Type,
                order = item.UniverseOrder
            })
            .ToListAsync(cancellationToken);

        bool itemAlreadyBelongsToUniverse =
            contentItemId.HasValue &&
            universeItems.Any(item => item.id == contentItemId.Value);

        int maximumOrder = itemAlreadyBelongsToUniverse
            ? universeItems.Count
            : universeItems.Count + 1;

        int? currentOrder = itemAlreadyBelongsToUniverse
            ? universeItems.Single(item => item.id == contentItemId!.Value).order
            : null;

        var positions = Enumerable
            .Range(1, maximumOrder)
            .Select(order => new
            {
                value = order,
                label = GetOrderLabel(order, maximumOrder, itemAlreadyBelongsToUniverse)
            })
            .ToArray();

        return Json(new
        {
            success = true,
            maximumOrder,
            currentOrder,
            positions,
            universeItems
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ContentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = GetModelStateErrorMessage() });
        }

        try
        {
            ContentItem createdItem = await _contentService.CreateAsync(
                CreateServiceRequest(model),
                cancellationToken);

            return Ok(new
            {
                success = true,
                id = createdItem.Id,
                message = model.Type == ContentType.Movie
                    ? "Movie added successfully."
                    : "Series added successfully."
            });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        ContentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!model.Id.HasValue || model.Id.Value < 1)
        {
            return BadRequest(new { success = false, message = "Invalid content item." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = GetModelStateErrorMessage() });
        }

        try
        {
            await _contentService.UpdateAsync(
                model.Id.Value,
                CreateServiceRequest(model),
                cancellationToken);

            return Ok(new { success = true, message = "Content updated successfully." });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return BadRequest(new { success = false, message = "Invalid content identifier." });
        }

        try
        {
            await _contentService.DeleteAsync(id, cancellationToken);
            return Ok(new { success = true, message = "Content permanently deleted." });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    private static SaveContentRequest CreateServiceRequest(ContentFormViewModel model) =>
        new()
        {
            Title = model.Title,
            Type = model.Type,
            SeasonCount = model.SeasonCount,
            UniverseId = model.UniverseId,
            UniverseOrder = model.UniverseOrder
        };

    private string GetModelStateErrorMessage()
    {
        string[] errors = ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "The submitted data is invalid."
                : error.ErrorMessage)
            .Distinct()
            .ToArray();

        return errors.Length == 0
            ? "The submitted data is invalid."
            : string.Join(" ", errors);
    }

    private static string GetOrderLabel(
        int order,
        int maximumOrder,
        bool itemAlreadyBelongsToUniverse)
    {
        if (order == 1)
        {
            return "#1 — First";
        }

        if (order == maximumOrder)
        {
            return itemAlreadyBelongsToUniverse
                ? $"#{order} — Move to end"
                : $"#{order} — Add to end";
        }

        return $"#{order}";
    }

    private static string? SanitizeHexColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return null;
        }

        return HexColorRegex().IsMatch(colorHex) ? colorHex : "#7C3AED";
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();
}
