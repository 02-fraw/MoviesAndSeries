using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Services.Models;

namespace MoviesAndSeries.Services.Interfaces;

public interface IUniverseService
{
    Task<Universe> CreateAsync(
        SaveUniverseRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        int universeId,
        SaveUniverseRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int universeId,
        CancellationToken cancellationToken = default);
}
