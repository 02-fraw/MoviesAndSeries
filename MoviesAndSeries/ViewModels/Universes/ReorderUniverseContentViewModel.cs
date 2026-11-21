using System.ComponentModel.DataAnnotations;

namespace MoviesAndSeries.ViewModels.Universes;

public sealed class ReorderUniverseContentViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Invalid universe.")]
    public int UniverseId { get; set; }

    public List<int> OrderedContentItemIds { get; set; } = new();
}
