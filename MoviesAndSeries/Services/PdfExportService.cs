using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Data;
using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Models.Enums;
using MoviesAndSeries.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MoviesAndSeries.Services;

public sealed partial class PdfExportService : IPdfExportService
{
    private const string TextColor = "#172033";
    private const string MutedTextColor = "#64748B";
    private const string PrimaryColor = "#5968E9";
    private const string CyanColor = "#0891B2";
    private const string BorderColor = "#DCE4F0";
    private const string PanelColor = "#F7F9FD";
    private const string SuccessColor = "#15805F";
    private const string PendingColor = "#B45309";

    private readonly ApplicationDbContext _context;

    public PdfExportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var universes = await _context.Universes
            .AsNoTracking()
            .Include(universe => universe.ContentItems)
            .ThenInclude(contentItem => contentItem.Seasons)
            .ToListAsync(cancellationToken);

        var standaloneItems = await _context.ContentItems
            .AsNoTracking()
            .Where(contentItem => contentItem.UniverseId == null)
            .Include(contentItem => contentItem.Seasons)
            .ToListAsync(cancellationToken);

        var universeGroups = universes
            .OrderBy(universe => universe.Name, StringComparer.OrdinalIgnoreCase)
            .Select(universe => new PdfUniverseGroup(
                universe.Name,
                NormalizeColor(universe.ColorHex),
                universe.ContentItems
                    .OrderBy(contentItem => contentItem.UniverseOrder ?? int.MaxValue)
                    .ThenBy(contentItem => contentItem.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(MapContentItem)
                    .ToList()))
            .ToList();

        var standaloneMovies = standaloneItems
            .Where(contentItem => contentItem.Type == ContentType.Movie)
            .OrderBy(contentItem => contentItem.Title, StringComparer.OrdinalIgnoreCase)
            .Select(MapContentItem)
            .ToList();

        var standaloneSeries = standaloneItems
            .Where(contentItem => contentItem.Type == ContentType.Series)
            .OrderBy(contentItem => contentItem.Title, StringComparer.OrdinalIgnoreCase)
            .Select(MapContentItem)
            .ToList();

        var allContentItems = universeGroups
            .SelectMany(group => group.ContentItems)
            .Concat(standaloneMovies)
            .Concat(standaloneSeries)
            .ToList();

        var archive = new PdfArchive(
            universeGroups,
            standaloneMovies,
            standaloneSeries,
            allContentItems.Count,
            allContentItems.Count(item => item.Type == ContentType.Movie),
            allContentItems.Count(item => item.Type == ContentType.Series),
            DateTime.UtcNow);

        return CreateDocument(archive).GeneratePdf();
    }

    private static IDocument CreateDocument(PdfArchive archive)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor(Colors.White);

                page.DefaultTextStyle(style => style
                    .FontSize(10)
                    .FontColor(TextColor));

                page.Header()
                    .Element(container => ComposeHeader(container, archive));

                page.Content()
                    .PaddingVertical(18)
                    .Column(column =>
                    {
                        column.Spacing(18);

                        column.Item()
                            .Element(container => ComposeSectionLabel(
                                container,
                                "UNIVERSES",
                                FormatCount(archive.Universes.Count, "universe", "universes")));

                        if (archive.Universes.Count == 0)
                        {
                            column.Item()
                                .Element(container => ComposeEmptyMessage(
                                    container,
                                    "No universes created yet."));
                        }
                        else
                        {
                            foreach (var universe in archive.Universes)
                            {
                                column.Item()
                                    .Element(container => ComposeUniverse(container, universe));
                            }
                        }

                        column.Item()
                            .Element(container => ComposeSectionLabel(
                                container,
                                "STANDALONE MOVIES",
                                FormatCount(archive.StandaloneMovies.Count, "movie", "movies")));

                        column.Item()
                            .Element(container => ComposeContentList(
                                container,
                                archive.StandaloneMovies,
                                "No standalone movies added yet."));

                        column.Item()
                            .Element(container => ComposeSectionLabel(
                                container,
                                "STANDALONE SERIES",
                                FormatCount(archive.StandaloneSeries.Count, "series", "series")));

                        column.Item()
                            .Element(container => ComposeContentList(
                                container,
                                archive.StandaloneSeries,
                                "No standalone series added yet."));
                    });

                page.Footer()
                    .Element(container => ComposeFooter(container, archive));
            });
        });
    }

    private static void ComposeHeader(IContainer container, PdfArchive archive)
    {
        container
            .BorderBottom(1)
            .BorderColor(BorderColor)
            .PaddingBottom(14)
            .Column(column =>
            {
                column.Spacing(12);

                column.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Column(titleColumn =>
                        {
                            titleColumn.Item()
                                .Text("MOVIES & SERIES")
                                .FontSize(18)
                                .Bold()
                                .FontColor(PrimaryColor);

                            titleColumn.Item()
                                .PaddingTop(2)
                                .Text("Personal Watch Archive")
                                .FontSize(9)
                                .FontColor(MutedTextColor);
                        });

                    row.AutoItem()
                        .AlignBottom()
                        .Text(archive.GeneratedAtUtc.ToString(
                            "yyyy-MM-dd HH:mm 'UTC'",
                            CultureInfo.InvariantCulture))
                        .FontSize(8)
                        .FontColor(MutedTextColor);
                });

                column.Item().Row(row =>
                {
                    row.Spacing(8);

                    row.RelativeItem()
                        .Element(box => ComposeSummaryBox(box, "Total", archive.TotalCount));

                    row.RelativeItem()
                        .Element(box => ComposeSummaryBox(box, "Movies", archive.MovieCount));

                    row.RelativeItem()
                        .Element(box => ComposeSummaryBox(box, "Series", archive.SeriesCount));

                    row.RelativeItem()
                        .Element(box => ComposeSummaryBox(box, "Universes", archive.Universes.Count));
                });
            });
    }

    private static void ComposeSummaryBox(IContainer container, string label, int value)
    {
        container
            .Background(PanelColor)
            .Border(1)
            .BorderColor(BorderColor)
            .PaddingVertical(7)
            .PaddingHorizontal(9)
            .Row(row =>
            {
                row.RelativeItem()
                    .Text(label)
                    .FontSize(8)
                    .FontColor(MutedTextColor);

                row.AutoItem()
                    .Text(value.ToString(CultureInfo.InvariantCulture))
                    .FontSize(11)
                    .SemiBold()
                    .FontColor(TextColor);
            });
    }

    private static void ComposeSectionLabel(IContainer container, string title, string countText)
    {
        container
            .BorderBottom(1)
            .BorderColor(BorderColor)
            .PaddingBottom(6)
            .Row(row =>
            {
                row.RelativeItem()
                    .Text(title)
                    .FontSize(10)
                    .Bold()
                    .FontColor(CyanColor);

                row.AutoItem()
                    .Text(countText)
                    .FontSize(8)
                    .FontColor(MutedTextColor);
            });
    }

    private static void ComposeUniverse(IContainer container, PdfUniverseGroup universe)
    {
        container.Column(column =>
        {
            column.Item()
                .BorderLeft(5)
                .BorderColor(universe.ColorHex)
                .Background(PanelColor)
                .Padding(11)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text(universe.Name)
                        .FontSize(12)
                        .SemiBold()
                        .FontColor(TextColor);

                    row.AutoItem()
                        .Text(FormatCount(universe.ContentItems.Count, "title", "titles"))
                        .FontSize(8)
                        .FontColor(MutedTextColor);
                });

            if (universe.ContentItems.Count == 0)
            {
                column.Item()
                    .Element(content => ComposeEmptyMessage(
                        content,
                        "No titles in this universe yet."));

                return;
            }

            for (int index = 0; index < universe.ContentItems.Count; index++)
            {
                var contentItem = universe.ContentItems[index];

                column.Item()
                    .Element(content => ComposeContentRow(content, contentItem, index + 1));
            }
        });
    }

    private static void ComposeContentList(
        IContainer container,
        IReadOnlyList<PdfContentItem> contentItems,
        string emptyMessage)
    {
        if (contentItems.Count == 0)
        {
            ComposeEmptyMessage(container, emptyMessage);
            return;
        }

        container.Column(column =>
        {
            for (int index = 0; index < contentItems.Count; index++)
            {
                var contentItem = contentItems[index];

                column.Item()
                    .Element(content => ComposeContentRow(content, contentItem, index + 1));
            }
        });
    }

    private static void ComposeContentRow(
        IContainer container,
        PdfContentItem contentItem,
        int displayOrder)
    {
        container
            .BorderBottom(1)
            .BorderColor(BorderColor)
            .PaddingVertical(8)
            .Row(row =>
            {
                row.ConstantItem(34)
                    .Text(displayOrder.ToString("00", CultureInfo.InvariantCulture))
                    .FontSize(8)
                    .SemiBold()
                    .FontColor(PrimaryColor);

                row.RelativeItem()
                    .Column(contentColumn =>
                    {
                        contentColumn.Item()
                            .Text(contentItem.Title)
                            .FontSize(10)
                            .SemiBold()
                            .FontColor(TextColor);

                        if (contentItem.Type == ContentType.Series)
                        {
                            contentColumn.Item()
                                .PaddingTop(3)
                                .Text(CreateSeasonSummary(contentItem.Seasons))
                                .FontSize(7.5f)
                                .FontColor(MutedTextColor);
                        }
                    });

                row.ConstantItem(52)
                    .AlignRight()
                    .Text(contentItem.Type == ContentType.Movie ? "Movie" : "Series")
                    .FontSize(8)
                    .FontColor(contentItem.Type == ContentType.Movie ? PrimaryColor : SuccessColor);

                row.ConstantItem(94)
                    .AlignRight()
                    .Text(CreateWatchStatus(contentItem))
                    .FontSize(8)
                    .SemiBold()
                    .FontColor(IsFullyWatched(contentItem) ? SuccessColor : PendingColor);
            });
    }

    private static void ComposeEmptyMessage(IContainer container, string message)
    {
        container
            .Background(PanelColor)
            .Border(1)
            .BorderColor(BorderColor)
            .Padding(12)
            .Text(message)
            .FontSize(8)
            .FontColor(MutedTextColor);
    }

    private static void ComposeFooter(IContainer container, PdfArchive archive)
    {
        container
            .BorderTop(1)
            .BorderColor(BorderColor)
            .PaddingTop(8)
            .Row(row =>
            {
                row.RelativeItem()
                    .Text($"Generated: {archive.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}")
                    .FontSize(7.5f)
                    .FontColor(MutedTextColor);

                row.AutoItem()
                    .Text(text =>
                    {
                        text.DefaultTextStyle(style => style
                            .FontSize(7.5f)
                            .FontColor(MutedTextColor));

                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
            });
    }

    private static PdfContentItem MapContentItem(ContentItem contentItem)
    {
        return new PdfContentItem(
            contentItem.Title,
            contentItem.Type,
            contentItem.IsWatched,
            contentItem.Seasons
                .OrderBy(season => season.SeasonNumber)
                .Select(season => new PdfSeason(season.SeasonNumber, season.IsWatched))
                .ToList());
    }

    private static string CreateSeasonSummary(IReadOnlyList<PdfSeason> seasons)
    {
        if (seasons.Count == 0)
        {
            return "No season data";
        }

        return string.Join(
            "  |  ",
            seasons.Select(season =>
                $"S{season.SeasonNumber}: {(season.IsWatched ? "Watched" : "Unwatched")}"));
    }

    private static string CreateWatchStatus(PdfContentItem contentItem)
    {
        if (contentItem.Type == ContentType.Movie)
        {
            return contentItem.IsWatched ? "Watched" : "Unwatched";
        }

        int watchedSeasonCount = contentItem.Seasons.Count(season => season.IsWatched);
        return $"{watchedSeasonCount}/{contentItem.Seasons.Count} watched";
    }

    private static bool IsFullyWatched(PdfContentItem contentItem)
    {
        if (contentItem.Type == ContentType.Movie)
        {
            return contentItem.IsWatched;
        }

        return contentItem.Seasons.Count > 0 &&
               contentItem.Seasons.All(season => season.IsWatched);
    }

    private static string FormatCount(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";

    private static string NormalizeColor(string? colorHex)
    {
        if (!string.IsNullOrWhiteSpace(colorHex) && HexColorRegex().IsMatch(colorHex))
        {
            return colorHex.ToUpperInvariant();
        }

        return MutedTextColor;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    private sealed record PdfArchive(
        IReadOnlyList<PdfUniverseGroup> Universes,
        IReadOnlyList<PdfContentItem> StandaloneMovies,
        IReadOnlyList<PdfContentItem> StandaloneSeries,
        int TotalCount,
        int MovieCount,
        int SeriesCount,
        DateTime GeneratedAtUtc);

    private sealed record PdfUniverseGroup(
        string Name,
        string ColorHex,
        IReadOnlyList<PdfContentItem> ContentItems);

    private sealed record PdfContentItem(
        string Title,
        ContentType Type,
        bool IsWatched,
        IReadOnlyList<PdfSeason> Seasons);

    private sealed record PdfSeason(
        int SeasonNumber,
        bool IsWatched);
}
