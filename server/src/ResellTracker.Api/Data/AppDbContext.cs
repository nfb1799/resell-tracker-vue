using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellTracker.Domain;

namespace ResellTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemPlatform> ItemPlatforms => Set<ItemPlatform>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<ItemPhoto> ItemPhotos => Set<ItemPhoto>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<PlatformFeeSetting> PlatformFeeSettings => Set<PlatformFeeSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every money column is decimal(10,2); floating point never touches stored money.
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> item)
    {
        // Ids are client-generated GUIDs (so offline replays are idempotent), which
        // would fragment a clustered index. Every query is scoped to one owner, so
        // cluster on (OwnerId, CreatedAt) instead: a user's list is one range scan.
        item.HasKey(i => i.Id).IsClustered(false);
        item.Property(i => i.Id).ValueGeneratedNever();
        item.HasIndex(i => new { i.OwnerId, i.CreatedAt }).IsClustered();

        item.HasOne<AppUser>().WithMany().HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.Cascade);

        item.Property(i => i.Status).HasConversion(new StatusConverter()).HasMaxLength(10).IsUnicode(false);
        item.Property(i => i.Title).HasMaxLength(ItemFields.MaxTitle);
        item.Property(i => i.Brand).HasMaxLength(ItemFields.MaxBrand);
        item.Property(i => i.Category).HasMaxLength(ItemFields.MaxCategory);
        item.Property(i => i.Size).HasMaxLength(ItemFields.MaxSize);
        item.Property(i => i.Source).HasMaxLength(ItemFields.MaxSource);
        item.Property(i => i.Notes).HasMaxLength(ItemFields.MaxNotes);
        item.Property(i => i.Condition).HasMaxLength(20).IsUnicode(false);
        item.Property(i => i.ThumbnailJpeg).HasMaxLength(8000);
        item.Property(i => i.RowVersion).IsRowVersion();
        item.Ignore(i => i.PlatformIds);

        item.HasMany(i => i.Platforms).WithOne().HasForeignKey(p => p.ItemId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne(i => i.Sale).WithOne().HasForeignKey<Sale>(s => s.ItemId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne(i => i.Donation).WithOne().HasForeignKey<Donation>(d => d.ItemId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne(i => i.Photo).WithOne().HasForeignKey<ItemPhoto>(p => p.ItemId).OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Stores statuses as their lowercase names ("inventory"), readable in the table.</summary>
    private sealed class StatusConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<ItemStatus, string>(
        s => s.ToString().ToLowerInvariant(),
        s => Enum.Parse<ItemStatus>(s, true));
}

internal sealed class ItemPlatformConfiguration : IEntityTypeConfiguration<ItemPlatform>
{
    public void Configure(EntityTypeBuilder<ItemPlatform> platform)
    {
        platform.HasKey(p => new { p.ItemId, p.PlatformId });
        platform.Property(p => p.PlatformId).HasMaxLength(20).IsUnicode(false);
    }
}

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> sale)
    {
        sale.HasKey(s => s.ItemId);
        sale.Property(s => s.Platform).HasMaxLength(20).IsUnicode(false);
    }
}

internal sealed class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> donation)
    {
        donation.HasKey(d => d.ItemId);
        donation.Property(d => d.Org).HasMaxLength(ItemFields.MaxDonationOrg);
    }
}

internal sealed class ItemPhotoConfiguration : IEntityTypeConfiguration<ItemPhoto>
{
    public void Configure(EntityTypeBuilder<ItemPhoto> photo) => photo.HasKey(p => p.ItemId);
}

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> settings)
    {
        settings.HasKey(s => s.UserId);
        settings.HasOne<AppUser>().WithOne().HasForeignKey<UserSettings>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        settings.Property(s => s.DisplayName).HasMaxLength(100);
        settings.Property(s => s.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength();
        settings.Property(s => s.Theme).HasMaxLength(10).IsUnicode(false);
    }
}

internal sealed class PlatformFeeSettingConfiguration : IEntityTypeConfiguration<PlatformFeeSetting>
{
    public void Configure(EntityTypeBuilder<PlatformFeeSetting> fee)
    {
        fee.HasKey(f => new { f.OwnerId, f.PlatformId });
        fee.HasOne<AppUser>().WithMany().HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.Cascade);
        fee.Property(f => f.PlatformId).HasMaxLength(20).IsUnicode(false);
        fee.Property(f => f.Percent).HasPrecision(5, 2);
    }
}
