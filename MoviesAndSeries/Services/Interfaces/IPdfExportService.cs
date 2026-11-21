namespace MoviesAndSeries.Services.Interfaces;

public interface IPdfExportService
{
    Task<byte[]> GenerateAsync(
        CancellationToken cancellationToken = default);
}
