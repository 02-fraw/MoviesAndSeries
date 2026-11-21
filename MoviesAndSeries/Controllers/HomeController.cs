using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Data;
using MoviesAndSeries.Models;
using MoviesAndSeries.Models.Enums;
using MoviesAndSeries.Services.Exceptions;
using MoviesAndSeries.Services.Interfaces;
using MoviesAndSeries.ViewModels.Home;

namespace MoviesAndSeries.Controllers;

public sealed partial class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IContentService _contentService;
    private readonly IPdfExportService _pdfExportService;

    public HomeController(
        ApplicationDbContext context,
        IContentService contentService,
        IPdfExportService pdfExportService)
    {
        _context = context;
        _contentService = contentService;
        _pdfExportService = pdfExportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var contentItems = await _context.ContentItems
            .AsNoTracking()
            .Include(item => item.Universe)
            .Include(item => item.Seasons)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var universes = await _context.Universes
            .AsNoTracking()
            .OrderBy(universe => universe.Name)
            .Select(universe => new HomeUniverseOptionViewModel
            {
                Id = universe.Id,
                Name = universe.Name
            })
            .ToListAsync(cancellationToken);

        var viewModel = new HomeIndexViewModel
        {
            ContentItems = contentItems
                .Select(item => new HomeContentItemViewModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Type = item.Type,
                    IsWatched = item.IsWatched,
                    UniverseId = item.UniverseId,
                    UniverseName = item.Universe?.Name,
                    UniverseColorHex = SanitizeHexColor(item.Universe?.ColorHex),
                    UniverseOrder = item.UniverseOrder,
                    CreatedAtUtc = item.CreatedAtUtc,
                    Seasons = item.Seasons
                        .OrderBy(season => season.SeasonNumber)
                        .Select(season => new HomeSeasonViewModel
                        {
                            Id = season.Id,
                            SeasonNumber = season.SeasonNumber,
                            IsWatched = season.IsWatched
                        })
                        .ToList()
                })
                .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id)
                .ToList(),

            Universes = universes,
            TotalCount = contentItems.Count,
            MovieCount = contentItems.Count(item => item.Type == ContentType.Movie),
            SeriesCount = contentItems.Count(item => item.Type == ContentType.Series),
            UniverseCount = universes.Count
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMovieWatched(
        int contentItemId,
        bool isWatched,
        CancellationToken cancellationToken)
    {
        if (contentItemId < 1)
        {
            return BadRequest(new { success = false, message = "Invalid movie identifier." });
        }

        try
        {
            bool updatedState = await _contentService.SetMovieWatchedAsync(
                contentItemId,
                isWatched,
                cancellationToken);

            return Json(new
            {
                success = true,
                isWatched = updatedState,
                message = updatedState
                    ? "Movie marked as watched."
                    : "Movie marked as unwatched."
            });
        }
        catch (BusinessRuleException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSeasonWatched(
        int seasonId,
        bool isWatched,
        CancellationToken cancellationToken)
    {
        if (seasonId < 1)
        {
            return BadRequest(new { success = false, message = "Invalid season identifier." });
        }

        try
        {
            var result = await _contentService.SetSeasonWatchedAsync(
                seasonId,
                isWatched,
                cancellationToken);

            return Json(new
            {
                success = true,
                isWatched = result.IsWatched,
                seriesIsWatched = result.SeriesIsWatched,
                message = result.IsWatched
                    ? $"Season {result.SeasonNumber} marked as watched."
                    : $"Season {result.SeasonNumber} marked as unwatched."
            });
        }
        catch (BusinessRuleException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(CancellationToken cancellationToken)
    {
        byte[] pdfBytes = await _pdfExportService.GenerateAsync(cancellationToken);
        string fileName = $"Movies-and-Series-{DateTime.UtcNow:yyyy-MM-dd}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
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
