using System.ComponentModel.DataAnnotations;

namespace MoviesAndSeries.Models.Entities;

public sealed class Season
{
    public int Id { get; set; }

    public int ContentItemId { get; set; }

    public ContentItem ContentItem { get; set; } = null!;

    [Range(1, 25)]
    public int SeasonNumber { get; set; }

    public bool IsWatched { get; set; }
}
