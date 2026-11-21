using System.ComponentModel.DataAnnotations;

namespace MoviesAndSeries.ViewModels.Universes;

public sealed class MoveUniverseContentViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Invalid content item.")]
    public int ContentItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Invalid target position.")]
    public int TargetOrder { get; set; }
}
