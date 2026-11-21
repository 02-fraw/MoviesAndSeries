using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Data;
using MoviesAndSeries.Models.Entities;
using MoviesAndSeries.Services.Exceptions;
using MoviesAndSeries.Services.Interfaces;
using MoviesAndSeries.Services.Models;

namespace MoviesAndSeries.Services;

public sealed partial class UniverseService : IUniverseService
{
    private const int MaximumNameLength = 100;

    private readonly ApplicationDbContext _context;

    public UniverseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Universe> CreateAsync(
        SaveUniverseRequest request,
        CancellationToken cancellationToken = default)
    {
        string name = NormalizeName(request.Name);
        string colorHex = NormalizeColor(request.ColorHex);

        bool nameExists = await _context.Universes
            .AnyAsync(universe => universe.Name == name, cancellationToken);

        if (nameExists)
        {
            throw new BusinessRuleException("A universe with this name already exists.");
        }

        var universe = new Universe
        {
            Name = name,
            ColorHex = colorHex,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Universes.Add(universe);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BusinessRuleException("A universe with this name already exists.");
        }

        return universe;
    }

    public async Task UpdateAsync(
        int universeId,
        SaveUniverseRequest request,
        CancellationToken cancellationToken = default)
    {
        Universe? universe = await _context.Universes
            .SingleOrDefaultAsync(item => item.Id == universeId, cancellationToken);

        if (universe is null)
        {
            throw new BusinessRuleException("The requested universe was not found.");
        }

        string name = NormalizeName(request.Name);
        string colorHex = NormalizeColor(request.ColorHex);

        bool nameExists = await _context.Universes
            .AnyAsync(
                item => item.Id != universeId && item.Name == name,
                cancellationToken);

        if (nameExists)
        {
            throw new BusinessRuleException("Another universe with this name already exists.");
        }

        universe.Name = name;
        universe.ColorHex = colorHex;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BusinessRuleException("Another universe with this name already exists.");
        }
    }

    public async Task DeleteAsync(
        int universeId,
        CancellationToken cancellationToken = default)
    {
        Universe? universe = await _context.Universes
            .Include(item => item.ContentItems)
            .SingleOrDefaultAsync(item => item.Id == universeId, cancellationToken);

        if (universe is null)
        {
            throw new BusinessRuleException("The requested universe was not found.");
        }

        foreach (ContentItem contentItem in universe.ContentItems.ToList())
        {
            contentItem.UniverseId = null;
            contentItem.UniverseOrder = null;
            contentItem.Universe = null;
        }

        _context.Universes.Remove(universe);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string? name)
    {
        string normalizedName = name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new BusinessRuleException("Universe name is required.");
        }

        if (normalizedName.Length > MaximumNameLength)
        {
            throw new BusinessRuleException(
                $"Universe name cannot exceed {MaximumNameLength} characters.");
        }

        return normalizedName;
    }

    private static string NormalizeColor(string? colorHex)
    {
        string normalizedColor = colorHex?.Trim() ?? string.Empty;

        if (!HexColorRegex().IsMatch(normalizedColor))
        {
            throw new BusinessRuleException(
                "Universe color must follow the #RRGGBB format.");
        }

        return normalizedColor.ToUpperInvariant();
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();
}
