using HrFlow.Api.Data;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public sealed class ReminderScanner(AppDbContext db, INotificationService notifications)
{
    public async Task ScanAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = await db.Contracts
            .Where(contract => contract.Status == ContractStatus.Active && contract.EndDate != null && contract.EndDate < today)
            .ToListAsync(ct);
        foreach (var contract in expired)
            contract.Status = ContractStatus.Expired;
        if (expired.Count > 0)
            await db.SaveChangesAsync(ct);

        var soon = today.AddDays(30);
        var expiring = await db.Contracts.AsNoTracking()
            .Include(contract => contract.Employee)
            .Where(contract => contract.Status == ContractStatus.Active
                && contract.EndDate != null
                && contract.EndDate >= today
                && contract.EndDate <= soon)
            .ToListAsync(ct);
        var rhUsers = await db.Users.AsNoTracking()
            .Where(user => user.Role == AppRoles.Rh && user.IsActive)
            .ToListAsync(ct);
        foreach (var contract in expiring)
        {
            foreach (var rh in rhUsers)
            {
                await notifications.NotifyAsync(
                    rh.Id,
                    $"contract-expiry:{contract.Id}:{rh.Id}",
                    "Contrat bientôt terminé",
                    $"{contract.Employee?.FirstName} {contract.Employee?.LastName} — échéance le {contract.EndDate:yyyy-MM-dd}.",
                    ct);
            }
        }

        var windowStart = DateTimeOffset.UtcNow.AddDays(-7);
        var windowEnd = DateTimeOffset.UtcNow.AddDays(7);
        var reviews = await db.PerformanceReviews.AsNoTracking()
            .Include(review => review.Employee)
            .Include(review => review.Reviewer)
            .Where(review => review.CompletedAt == null && review.ScheduledAt >= windowStart && review.ScheduledAt <= windowEnd)
            .ToListAsync(ct);
        foreach (var review in reviews)
        {
            if (review.Reviewer?.UserId is not string reviewerUserId)
                continue;
            await notifications.NotifyAsync(
                reviewerUserId,
                $"review-due:{review.Id}:{reviewerUserId}",
                "Évaluation à venir",
                $"Entretien avec {review.Employee?.FirstName} {review.Employee?.LastName} prévu le {review.ScheduledAt:yyyy-MM-dd}.",
                ct);
        }
    }
}

public sealed class ReminderBackgroundService(
    IServiceScopeFactory scopes,
    IHostEnvironment environment,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var scanner = scope.ServiceProvider.GetRequiredService<ReminderScanner>();
                await scanner.ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Le balayage des rappels a échoué.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
