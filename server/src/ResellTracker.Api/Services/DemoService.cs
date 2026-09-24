using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Data;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

/// <summary>
/// "Try the demo": every visitor gets a private account seeded with the sample
/// inventory in shared/demo-items.json, so nobody sees anyone else's edits. The
/// account expires after <see cref="Lifetime"/> and the cleanup job deletes it.
/// </summary>
public sealed class DemoService(AppDbContext db, UserManager<AppUser> users, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private static readonly DemoFile Seed = LoadSeed();

    public async Task<AppUser> CreateAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var id = Guid.CreateVersion7();
        var user = new AppUser
        {
            Id = id,
            UserName = $"demo-{id:N}",
            // .invalid is reserved and never resolves, so nothing is ever mailed here.
            Email = $"demo-{id:N}@demo.invalid",
            IsDemo = true,
            DemoExpiresAt = now + Lifetime,
        };

        var created = await users.CreateAsync(user); // no password: a demo can only be entered through this door
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        db.UserSettings.Add(new UserSettings
        {
            UserId = id,
            DisplayName = Seed.Settings.DisplayName,
            Currency = Seed.Settings.Currency,
            Theme = Seed.Settings.Theme,
            ProfitGoal = Seed.Settings.ProfitGoal,
        });

        foreach (var sample in Seed.Items)
        {
            db.Items.Add(ToItem(sample, id, now, today));
        }

        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Deletes every demo account past its expiry; the database cascades to its data.</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.Users.Where(u => u.IsDemo && u.DemoExpiresAt < now).ExecuteDeleteAsync(ct);
    }

    private static Item ToItem(DemoItem s, Guid ownerId, DateTimeOffset now, DateOnly today)
    {
        DateOnly? DaysAgo(int? days) => days is { } d ? today.AddDays(-d) : null;

        var item = new Item
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = s.Title,
            Brand = s.Brand ?? "",
            Category = s.Category ?? "",
            Size = s.Size ?? "",
            Condition = ItemFields.DefaultCondition,
            Cost = s.Cost,
            Source = s.Source ?? "",
            AcquiredDate = DaysAgo(s.AcquiredDaysAgo),
            ListPrice = s.ListPrice,
            ListedDate = DaysAgo(s.ListedDaysAgo),
            CreatedAt = now.AddDays(-s.CreatedDaysAgo),
            UpdatedAt = now,
            Platforms = [.. s.Platforms.Select((p, i) => new ItemPlatform { PlatformId = p, Position = (byte)i })],
        };

        // The same rules as everywhere else decide the status.
        item.Status = Lifecycle.ApplyPlatforms(ItemStatus.Inventory, s.Platforms, item.ListedDate, today).Status;

        if (s.Sale is { } sale)
        {
            item.Sale = new Sale
            {
                Platform = sale.Platform,
                ListedFor = sale.ListedFor,
                Price = sale.Price,
                Payout = sale.Payout,
                ShippingCharged = sale.ShippingCharged,
                ShippingCost = sale.ShippingCost,
                OtherCosts = sale.OtherCosts,
                Date = today.AddDays(-sale.DaysAgo),
            };
            item.Status = ItemStatus.Sold;
        }
        else if (s.Donation is { } donation)
        {
            item.Donation = new Donation
            {
                Date = today.AddDays(-donation.DaysAgo),
                Org = donation.Org,
                ReceiptValue = donation.ReceiptValue,
            };
            item.Status = ItemStatus.Donated;
        }

        return item;
    }

    private static DemoFile LoadSeed()
    {
        using var stream = typeof(DemoService).Assembly.GetManifestResourceStream("ResellTracker.Api.demo-items.json")
            ?? throw new InvalidOperationException("demo-items.json is not embedded in the API assembly.");
        return JsonSerializer.Deserialize<DemoFile>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("demo-items.json is empty.");
    }

    private sealed record DemoFile(DemoSettings Settings, List<DemoItem> Items);

    private sealed record DemoSettings(string DisplayName, string Currency, string Theme, decimal ProfitGoal);

    private sealed record DemoItem(
        string Title,
        string? Brand,
        string? Category,
        string? Size,
        decimal Cost,
        string? Source,
        List<string> Platforms,
        decimal? ListPrice,
        int CreatedDaysAgo,
        int? AcquiredDaysAgo,
        int? ListedDaysAgo,
        DemoSale? Sale,
        DemoDonation? Donation);

    private sealed record DemoSale(
        string Platform,
        decimal ListedFor,
        decimal Price,
        decimal? Payout,
        decimal ShippingCharged,
        decimal ShippingCost,
        decimal OtherCosts,
        int DaysAgo);

    private sealed record DemoDonation(string Org, decimal? ReceiptValue, int DaysAgo);
}

/// <summary>Deletes expired demo accounts on a timer.</summary>
internal sealed partial class DemoCleanupService(
    IServiceScopeFactory scopes, IConfiguration config, ILogger<DemoCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = config.GetValue("Demo:CleanupInterval", TimeSpan.FromHours(1));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<DemoService>().DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                {
                    LogDeleted(logger, deleted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed sweep shouldn't stop the next.
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} expired demo accounts.")]
    private static partial void LogDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Demo cleanup failed.")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
