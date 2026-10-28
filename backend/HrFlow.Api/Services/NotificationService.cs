using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public sealed class AuditWriter(AppDbContext db, ICurrentUser current) : IAuditWriter
{
    public void Write(string action, string entityName, string entityId, string? details)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = current.UserId ?? "system",
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details
        });
    }
}

public sealed class NotificationService(
    AppDbContext db,
    ICurrentUser current,
    IEmailSender email) : INotificationService
{
    public async Task NotifyAsync(string userId, string dedupKey, string title, string body, CancellationToken ct)
    {
        if (await db.Notifications.AnyAsync(n => n.DedupKey == dedupKey, ct))
            return;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = userId,
            Title = title,
            Body = body,
            DedupKey = dedupKey,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Notifications.Add(notification);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(notification).State = EntityState.Detached;
            return;
        }

        var address = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(address))
            return;

        await email.SendAsync(address, title, body, ct);
        notification.EmailDispatched = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListMineAsync(CancellationToken ct)
    {
        var userId = current.UserId ?? throw new ForbiddenAccessException();
        return await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new ForbiddenAccessException();
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId, ct)
            ?? throw new NotFoundException("Notification introuvable.");
        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
