using Microsoft.EntityFrameworkCore;
using MoviesAndSeries.Models.Entities;

namespace MoviesAndSeries.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Universe> Universes { get; set; } = null!;

    public DbSet<ContentItem> ContentItems { get; set; } = null!;

    public DbSet<Season> Seasons { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUniverse(modelBuilder);
        ConfigureContentItem(modelBuilder);
        ConfigureSeason(modelBuilder);
    }

    private static void ConfigureUniverse(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Universe>(entity =>
        {
            entity.Property(universe => universe.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(universe => universe.ColorHex)
                .IsRequired()
                .HasMaxLength(7)
                .IsUnicode(false)
                .HasDefaultValue("#7C3AED");

            entity.Property(universe => universe.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(universe => universe.Name)
                .IsUnique();
        });
    }

    private static void ConfigureContentItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.Property(contentItem => contentItem.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(contentItem => contentItem.Type)
                .IsRequired();

            entity.Property(contentItem => contentItem.IsWatched)
                .HasDefaultValue(false);

            entity.Property(contentItem => contentItem.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(contentItem => contentItem.CreatedAtUtc);

            entity.HasIndex(contentItem => new
            {
                contentItem.UniverseId,
                contentItem.UniverseOrder
            });

            entity.HasOne(contentItem => contentItem.Universe)
                .WithMany(universe => universe.ContentItems)
                .HasForeignKey(contentItem => contentItem.UniverseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSeason(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>(entity =>
        {
            entity.Property(season => season.SeasonNumber)
                .IsRequired();

            entity.Property(season => season.IsWatched)
                .HasDefaultValue(false);

            entity.HasIndex(season => new
            {
                season.ContentItemId,
                season.SeasonNumber
            })
            .IsUnique();

            entity.HasOne(season => season.ContentItem)
                .WithMany(contentItem => contentItem.Seasons)
                .HasForeignKey(season => season.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
