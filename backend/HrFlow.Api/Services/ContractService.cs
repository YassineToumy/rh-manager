using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IContractService
{
    Task<IReadOnlyList<ContractDto>> ListAsync(ContractListQuery query, CancellationToken ct);
    Task<ContractDto> CreateAsync(UpsertContractRequest request, CancellationToken ct);
    Task<ContractDto> UpdateAsync(Guid id, UpsertContractRequest request, CancellationToken ct);
}

public sealed class ContractService(AppDbContext db, ICurrentUser current, IAuditWriter audit) : IContractService
{
    public async Task<IReadOnlyList<ContractDto>> ListAsync(ContractListQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var contracts = db.Contracts.AsNoTracking().Include(contract => contract.Employee).AsQueryable();
        contracts = contracts.Where(contract => EmployeeScope.Visible(db.Employees, current).Select(employee => employee.Id).Contains(contract.EmployeeId));

        if (query.EmployeeId is Guid employeeId)
            contracts = contracts.Where(contract => contract.EmployeeId == employeeId);
        if (query.ExpiringWithinDays is int days)
        {
            var limit = today.AddDays(days);
            contracts = contracts.Where(contract =>
                contract.Status == ContractStatus.Active
                && contract.EndDate != null
                && contract.EndDate >= today
                && contract.EndDate <= limit);
        }

        var items = await contracts.OrderBy(contract => contract.EndDate).ThenByDescending(contract => contract.StartDate).ToListAsync(ct);
        return items.Select(contract => Mapping.ToDto(contract, today)).ToArray();
    }

    public async Task<ContractDto> CreateAsync(UpsertContractRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageEmployees(current.Roles));
        var employee = await RequireEmployeeAsync(request.EmployeeId, ct);
        ValidateDates(request.StartDate, request.EndDate);
        var contract = new EmploymentContract
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Type = request.Type.Trim(),
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes?.Trim()
        };
        db.Contracts.Add(contract);
        if (contract.Status == ContractStatus.Active)
            employee.ContractType = contract.Type;
        db.EmploymentEvents.Add(new EmploymentEvent
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            OccurredAt = DateTimeOffset.UtcNow,
            Kind = "Contrat",
            Description = $"Contrat {contract.Type} à partir du {contract.StartDate:yyyy-MM-dd}."
        });
        audit.Write("contract.create", "Contract", contract.Id.ToString(), employee.InternalCode);
        await db.SaveChangesAsync(ct);
        contract.Employee = employee;
        return Mapping.ToDto(contract, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<ContractDto> UpdateAsync(Guid id, UpsertContractRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageEmployees(current.Roles));
        var contract = await db.Contracts.Include(item => item.Employee).FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Contrat introuvable.");
        if (contract.EmployeeId != request.EmployeeId)
            throw new BusinessRuleException("Un contrat ne change pas d'employé.");
        ValidateDates(request.StartDate, request.EndDate);
        contract.Type = request.Type.Trim();
        contract.Status = request.Status;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.Notes = request.Notes?.Trim();
        if (contract.Status == ContractStatus.Active && contract.Employee is not null)
            contract.Employee.ContractType = contract.Type;
        audit.Write("contract.update", "Contract", contract.Id.ToString(), contract.Status.ToString());
        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(contract, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private async Task<Employee> RequireEmployeeAsync(Guid id, CancellationToken ct) =>
        await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id && employee.Status == EmployeeStatus.Active, ct)
        ?? throw new NotFoundException("Employé introuvable.");

    private static void ValidateDates(DateOnly start, DateOnly? end)
    {
        if (end is DateOnly finish && finish < start)
            throw new BusinessRuleException("La date de fin doit être postérieure ou égale à la date de début.");
    }
}
