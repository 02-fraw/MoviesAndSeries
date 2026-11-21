using System.ComponentModel.DataAnnotations;
using MoviesAndSeries.Models.Enums;

namespace MoviesAndSeries.ViewModels.Management;

public sealed class ContentFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [EnumDataType(typeof(ContentType), ErrorMessage = "Please select a valid content type.")]
    public ContentType Type { get; set; } = ContentType.Movie;

    [Range(1, 25, ErrorMessage = "Season count must be between 1 and 25.")]
    public int? SeasonCount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid universe.")]
    public int? UniverseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid watch order.")]
    public int? UniverseOrder { get; set; }
}
