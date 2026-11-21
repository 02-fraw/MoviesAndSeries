using System.ComponentModel.DataAnnotations;

namespace MoviesAndSeries.Models.Entities;

public sealed class Universe
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(7)]
    [RegularExpression(
        @"^#[0-9A-Fa-f]{6}$",
        ErrorMessage = "Color code must follow the #RRGGBB format.")]
    public string ColorHex { get; set; } = "#7C3AED";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ContentItem> ContentItems { get; set; } = new List<ContentItem>();
}
