using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Data;
using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Models.Enums;
using MoviesAndSeries.Services.Exceptions;
using MoviesAndSeries.Services.Interfaces;
using MoviesAndSeries.Services.Models;
using MoviesAndSeries.ViewModels.Universes;

namespace MoviesAndSeries.Controllers;

public sealed partial class UniversesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IUniverseService _universeService;
    private readonly IContentService _contentService;

    public UniversesController(
        ApplicationDbContext context,
        IUniverseService universeService,
        IContentService contentService)
    {
        _context = context;
        _universeService = universeService;
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        List<UniverseListItemViewModel> universes = await _context.Universes
            .AsNoTracking()
            .OrderBy(universe => universe.Name)
            .Select(universe => new UniverseListItemViewModel
            {
                Id = universe.Id,
                Name = universe.Name,
                ColorHex = universe.ColorHex,
                ContentCount = universe.ContentItems.Count,
                MovieCount = universe.ContentItems.Count(item => item.Type == ContentType.Movie),
                SeriesCount = universe.ContentItems.Count(item => item.Type == ContentType.Series),
                CreatedAtUtc = universe.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        universes = universes
            .Select(universe => new UniverseListItemViewModel
            {
                Id = universe.Id,
                Name = universe.Name,
                ColorHex = SanitizeHexColor(universe.ColorHex),
                ContentCount = universe.ContentCount,
                MovieCount = universe.MovieCount,
                SeriesCount = universe.SeriesCount,
                CreatedAtUtc = universe.CreatedAtUtc
            })
            .OrderBy(universe => universe.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(universe => universe.Id)
            .ToList();

        var contentTotals = await _context.ContentItems
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                MovieCount = group.Count(item => item.Type == ContentType.Movie),
                SeriesCount = group.Count(item => item.Type == ContentType.Series)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var viewModel = new UniverseIndexViewModel
        {
            Universes = universes,
            UniverseCount = universes.Count,
            TotalContentCount = contentTotals?.TotalCount ?? 0,
            MovieCount = contentTotals?.MovieCount ?? 0,
            SeriesCount = contentTotals?.SeriesCount ?? 0
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetUniverse(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return BadRequest(new { success = false, message = "Invalid universe identifier." });
        }

        var universe = await _context.Universes
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                colorHex = item.ColorHex
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (universe is null)
        {
            return NotFound(new { success = false, message = "Universe not found." });
        }

        return Json(new
        {
            success = true,
            universe = new
            {
                universe.id,
                universe.name,
                colorHex = SanitizeHexColor(universe.colorHex)
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return BadRequest(new { success = false, message = "Invalid universe identifier." });
        }

        var universeInfo = await _context.Universes
            .AsNoTracking()
            .Where(universe => universe.Id == id)
            .Select(universe => new
            {
                universe.Id,
                universe.Name,
                universe.ColorHex
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (universeInfo is null)
        {
            return NotFound(new { success = false, message = "Universe not found." });
        }

        List<UniverseContentItemViewModel> contentItems = await _context.ContentItems
            .AsNoTracking()
            .Where(item => item.UniverseId == id)
            .OrderBy(item => item.UniverseOrder)
            .ThenBy(item => item.Id)
            .Select(item => new UniverseContentItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Type = item.Type,
                UniverseOrder = item.UniverseOrder ?? 0,
                IsWatched = item.IsWatched,
                SeasonCount = item.Seasons.Count(),
                WatchedSeasonCount = item.Seasons.Count(season => season.IsWatched)
            })
            .ToListAsync(cancellationToken);

        var detail = new UniverseDetailViewModel
        {
            Id = universeInfo.Id,
            Name = universeInfo.Name,
            ColorHex = SanitizeHexColor(universeInfo.ColorHex),
            ContentCount = contentItems.Count,
            MovieCount = contentItems.Count(item => item.Type == ContentType.Movie),
            SeriesCount = contentItems.Count(item => item.Type == ContentType.Series),
            ContentItems = contentItems
        };

        return Json(new { success = true, universe = detail });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        UniverseFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = GetModelStateErrorMessage() });
        }

        try
        {
            Universe createdUniverse = await _universeService.CreateAsync(
                CreateServiceRequest(model),
                cancellationToken);

            return Ok(new
            {
                success = true,
                id = createdUniverse.Id,
                message = "Universe created successfully."
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
        UniverseFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!model.Id.HasValue || model.Id.Value < 1)
        {
            return BadRequest(new { success = false, message = "Invalid universe." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = GetModelStateErrorMessage() });
        }

        try
        {
            await _universeService.UpdateAsync(
                model.Id.Value,
                CreateServiceRequest(model),
                cancellationToken);

            return Ok(new { success = true, message = "Universe updated successfully." });
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
            return BadRequest(new { success = false, message = "Invalid universe identifier." });
        }

        try
        {
            await _universeService.DeleteAsync(id, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Universe deleted. Its titles are now standalone."
            });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveContent(
        MoveUniverseContentViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = GetModelStateErrorMessage() });
        }

        try
        {
            await _contentService.MoveWithinUniverseAsync(
                model.ContentItemId,
                model.TargetOrder,
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = $"Moved to position #{model.TargetOrder}."
            });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(
        ReorderUniverseContentViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = GetModelStateErrorMessage() });
        }

        if (model.OrderedContentItemIds.Count == 0)
        {
            return BadRequest(new { success = false, message = "No order changes to save." });
        }

        try
        {
            await _contentService.ReorderUniverseAsync(
                model.UniverseId,
                model.OrderedContentItemIds,
                cancellationToken);

            return Ok(new { success = true, message = "Universe order saved." });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveContent(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return BadRequest(new { success = false, message = "Invalid content identifier." });
        }

        try
        {
            await _contentService.RemoveFromUniverseAsync(id, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Removed from universe and converted to standalone."
            });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContent(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return BadRequest(new { success = false, message = "Invalid content identifier." });
        }

        try
        {
            await _contentService.DeleteAsync(id, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Content permanently deleted."
            });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    private static SaveUniverseRequest CreateServiceRequest(UniverseFormViewModel model) =>
        new()
        {
            Name = model.Name,
            ColorHex = model.ColorHex
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

    private static string SanitizeHexColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return "#7C3AED";
        }

        return HexColorRegex().IsMatch(colorHex) ? colorHex : "#7C3AED";
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();
}
