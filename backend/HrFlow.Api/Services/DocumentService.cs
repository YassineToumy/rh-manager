using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(Guid? employeeId, CancellationToken ct);
    Task<DocumentDto> UploadAsync(UploadDocumentRequest request, Stream content, string originalFileName, CancellationToken ct);
    Task<StoredRead> OpenAsync(Guid id, CancellationToken ct);
}

public sealed class DocumentService(
    AppDbContext db,
    ICurrentUser current,
    IFileStorage storage,
    IAuditWriter audit) : IDocumentService
{
    public async Task<IReadOnlyList<DocumentDto>> ListAsync(Guid? employeeId, CancellationToken ct)
    {
        var visible = EmployeeScope.Visible(db.Employees.AsNoTracking(), current).Select(employee => employee.Id);
        var documents = db.Documents.AsNoTracking().Include(document => document.Employee).Where(document => visible.Contains(document.EmployeeId));
        if (employeeId is Guid id)
            documents = documents.Where(document => document.EmployeeId == id);
        var items = await documents.OrderByDescending(document => document.UploadedAt).ToListAsync(ct);
        return items.Select(Mapping.ToDto).ToArray();
    }

    public async Task<DocumentDto> UploadAsync(UploadDocumentRequest request, Stream content, string originalFileName, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageEmployees(current.Roles));
        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == request.EmployeeId, ct)
            ?? throw new NotFoundException("Employé introuvable.");
        if (request.ContractId is Guid contractId
            && !await db.Contracts.AnyAsync(contract => contract.Id == contractId && contract.EmployeeId == employee.Id, ct))
            throw new BusinessRuleException("Ce contrat n'appartient pas à l'employé.");

        var stored = await storage.SaveAsync(content, originalFileName, "documents", ct);
        var document = new EmployeeDocument
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            ContractId = request.ContractId,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = stored.StoredFileName,
            ContentType = stored.ContentType,
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedByUserId = current.UserId ?? "system"
        };
        db.Documents.Add(document);
        audit.Write("document.upload", "Document", document.Id.ToString(), document.OriginalFileName);
        await db.SaveChangesAsync(ct);
        document.Employee = employee;
        return Mapping.ToDto(document);
    }

    public async Task<StoredRead> OpenAsync(Guid id, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().Include(item => item.Employee).FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Document introuvable.");
        var employee = document.Employee ?? throw new NotFoundException("Document introuvable.");
        if (!HrAccess.CanViewEmployee(current.Roles, employee.Id, employee.ManagerId, current.EmployeeId))
            throw new ForbiddenAccessException();

        audit.Write("document.download", "Document", document.Id.ToString(), document.OriginalFileName);
        await db.SaveChangesAsync(ct);
        return await storage.OpenReadAsync("documents", document.StoredFileName, ct);
    }
}
