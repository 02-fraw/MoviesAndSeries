namespace MoviesAndSeries.ViewModels.Management;

public sealed class UniverseOptionViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ColorHex { get; init; } = "#7C3AED";

    public int ContentCount { get; init; }
}
