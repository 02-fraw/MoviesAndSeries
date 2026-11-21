using System.ComponentModel.DataAnnotations;
using MoviesAndSeries.Models.Enums;

namespace MoviesAndSeries.Models.Entities;

public sealed class ContentItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public ContentType Type { get; set; } = ContentType.Movie;

    public bool IsWatched { get; set; }

    public int? UniverseId { get; set; }

    public Universe? Universe { get; set; }

    [Range(1, int.MaxValue)]
    public int? UniverseOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Season> Seasons { get; set; } = new List<Season>();
}
