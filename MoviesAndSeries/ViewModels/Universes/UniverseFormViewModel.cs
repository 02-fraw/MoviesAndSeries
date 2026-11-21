using System.ComponentModel.DataAnnotations;

namespace MoviesAndSeries.ViewModels.Universes;

public sealed class UniverseFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Universe name is required.")]
    [StringLength(100, ErrorMessage = "Universe name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Universe color is required.")]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color code must follow the #RRGGBB format.")]
    public string ColorHex { get; set; } = "#7C3AED";
}
