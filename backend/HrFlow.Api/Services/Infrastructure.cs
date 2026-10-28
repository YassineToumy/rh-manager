using HrFlow.Api.Dtos;
using HrFlow.Api.Models;

namespace HrFlow.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("E-mail d'apprentissage vers {To} — {Subject}{NewLine}{Body}", to, subject, Environment.NewLine, body);
        return Task.CompletedTask;
    }
}

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string folder, CancellationToken ct);
    Task<StoredRead> OpenReadAsync(string folder, string storedFileName, CancellationToken ct);
}

public record StoredFile(string StoredFileName, string ContentType);
public record StoredRead(Stream Content, string ContentType);

public sealed class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, string folder, CancellationToken ct)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!Allowed.TryGetValue(extension, out var contentType))
            throw new Security.BusinessRuleException("Type de fichier non autorisé. Formats acceptés : pdf, png, jpg, webp, docx.");

        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = DirectoryFor(folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, storedName);
        await using var target = File.Create(path);
        await content.CopyToAsync(target, ct);
        return new StoredFile(storedName, contentType);
    }

    public Task<StoredRead> OpenReadAsync(string folder, string storedFileName, CancellationToken ct)
    {
        var safeName = Path.GetFileName(storedFileName);
        var directory = DirectoryFor(folder);
        var path = Path.GetFullPath(Path.Combine(directory, safeName));
        if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new Security.NotFoundException("Fichier introuvable.");

        var extension = Path.GetExtension(safeName);
        var contentType = Allowed.TryGetValue(extension, out var known) ? known : "application/octet-stream";
        Stream stream = File.OpenRead(path);
        return Task.FromResult(new StoredRead(stream, contentType));
    }

    private string DirectoryFor(string folder)
    {
        var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "storage"));
        var directory = Path.GetFullPath(Path.Combine(root, folder));
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new Security.BusinessRuleException("Dossier de stockage invalide.");
        return directory;
    }
}

public interface IAuditWriter
{
    void Write(string action, string entityName, string entityId, string? details);
}

public interface INotificationService
{
    Task NotifyAsync(string userId, string dedupKey, string title, string body, CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> ListMineAsync(CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
}
