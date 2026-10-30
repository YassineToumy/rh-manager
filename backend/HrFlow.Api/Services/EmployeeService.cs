using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct);
    Task<EmployeeDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<EmployeeDto> CreateAsync(UpsertEmployeeRequest request, CancellationToken ct);
    Task<EmployeeDto> UpdateAsync(Guid id, UpsertEmployeeRequest request, CancellationToken ct);
    Task ArchiveAsync(Guid id, CancellationToken ct);
}

public sealed class EmployeeService(AppDbContext db, ICurrentUser current, IAuditWriter audit) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct)
    {
        RequireDirectory();
        var employees = Visible().AsNoTracking()
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .Include(employee => employee.Manager)
            .AsQueryable();

        if (!query.IncludeArchived)
            employees = employees.Where(employee => employee.Status == EmployeeStatus.Active);
        if (query.DepartmentId is Guid departmentId)
            employees = employees.Where(employee => employee.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            employees = employees.Where(employee =>
                employee.FirstName.ToLower().Contains(term)
                || employee.LastName.ToLower().Contains(term)
                || employee.InternalCode.ToLower().Contains(term)
                || employee.ProfessionalEmail.ToLower().Contains(term));
        }

        employees = employees.OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName);
        var page = await employees.ToPagedAsync(query.Page, query.PageSize, ct);
        return new PagedResult<EmployeeDto>(page.Items.Select(Mapping.ToDto).ToArray(), page.Total, page.Page, page.PageSize);
    }

    public async Task<EmployeeDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        RequireDirectory();
        var employee = await Visible()
            .AsNoTracking()
            .Include(item => item.Department)
            .Include(item => item.Position)
            .Include(item => item.Manager)
            .Include(item => item.History)
            .Include(item => item.Contracts)
            .Include(item => item.Documents)
            .Include(item => item.DirectReports)
            .FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Employé introuvable.");

        var year = DateTime.UtcNow.Year;
        await EnsureBalancesAsync(employee.Id, year, ct);
        await db.SaveChangesAsync(ct);

        var balances = await db.LeaveBalances.AsNoTracking()
            .Where(balance => balance.EmployeeId == employee.Id && balance.Year == year)
            .Include(balance => balance.LeaveType)
            .ToListAsync(ct);
        var pending = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.EmployeeId == employee.Id && request.Status == LeaveStatus.Pending && request.StartDate.Year == year)
            .GroupBy(request => request.LeaveTypeId)
            .Select(group => new { TypeId = group.Key, Days = group.Sum(request => request.BusinessDays) })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var balanceDtos = balances.Select(balance =>
        {
            var held = pending.FirstOrDefault(item => item.TypeId == balance.LeaveTypeId)?.Days ?? 0;
            return new LeaveBalanceDto(
                balance.Id,
                balance.LeaveTypeId,
                balance.LeaveType?.Name ?? string.Empty,
                balance.Year,
                balance.AllocatedDays,
                balance.UsedDays,
                held,
                balance.AllocatedDays - balance.UsedDays - held);
        }).ToArray();

        return new EmployeeDetailDto(
            Mapping.ToDto(employee),
            employee.History.OrderByDescending(item => item.OccurredAt)
                .Select(item => new EmploymentEventDto(item.Id, item.OccurredAt, item.Kind, item.Description)).ToArray(),
            employee.Contracts.OrderByDescending(item => item.StartDate).Select(item => Mapping.ToDto(item, today)).ToArray(),
            employee.Documents.OrderByDescending(item => item.UploadedAt).Select(Mapping.ToDto).ToArray(),
            balanceDtos,
            employee.DirectReports.Where(item => item.Status == EmployeeStatus.Active)
                .Select(item => new EmployeeSummaryDto(item.Id, Mapping.FullName(item), item.InternalCode, item.ManagerId, item.DepartmentId, string.Empty))
                .ToArray());
    }

    public async Task<EmployeeDto> CreateAsync(UpsertEmployeeRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageEmployees(current.Roles));
        var (department, position, manager) = await LoadAssignmentAsync(request, null, ct);
        var email = request.ProfessionalEmail.Trim().ToLowerInvariant();
        if (await db.Employees.AnyAsync(employee => employee.ProfessionalEmail == email, ct))
            throw new BusinessRuleException("Cet e-mail professionnel est déjà utilisé.");

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            InternalCode = await NextCodeAsync(ct),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            ProfessionalEmail = email,
            Phone = Normalize(request.Phone),
            HireDate = request.HireDate,
            ContractType = request.ContractType.Trim(),
            DepartmentId = department.Id,
            PositionId = position.Id,
            ManagerId = manager?.Id,
            Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        AddHistory(employee.Id, "Embauche", $"Embauche au poste {position.Title}, département {department.Name}.");
        await EnsureBalancesAsync(employee.Id, DateTime.UtcNow.Year, ct);
        audit.Write("employee.create", "Employee", employee.Id.ToString(), employee.InternalCode);
        await db.SaveChangesAsync(ct);
        employee.Department = department;
        employee.Position = position;
        employee.Manager = manager;
        return Mapping.ToDto(employee);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, UpsertEmployeeRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageEmployees(current.Roles));
        var employee = await db.Employees
            .Include(item => item.Department)
            .Include(item => item.Position)
            .Include(item => item.Manager)
            .FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Employé introuvable.");
        if (employee.Status == EmployeeStatus.Archived)
            throw new BusinessRuleException("Un dossier archivé ne se modifie pas.");

        var (department, position, manager) = await LoadAssignmentAsync(request, employee.Id, ct);
        var email = request.ProfessionalEmail.Trim().ToLowerInvariant();
        if (await db.Employees.AnyAsync(item => item.Id != id && item.ProfessionalEmail == email, ct))
            throw new BusinessRuleException("Cet e-mail professionnel est déjà utilisé.");

        if (employee.DepartmentId != department.Id || employee.PositionId != position.Id)
            AddHistory(employee.Id, "Affectation", $"Passage vers {position.Title} ({department.Name}).");
        if (employee.ManagerId != manager?.Id)
            AddHistory(employee.Id, "Responsable", manager is null ? "Responsable retiré." : $"Nouveau responsable : {Mapping.FullName(manager)}.");

        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.ProfessionalEmail = email;
        employee.Phone = Normalize(request.Phone);
        employee.HireDate = request.HireDate;
        employee.ContractType = request.ContractType.Trim();
        employee.DepartmentId = department.Id;
        employee.PositionId = position.Id;
        employee.ManagerId = manager?.Id;
        audit.Write("employee.update", "Employee", employee.Id.ToString(), employee.InternalCode);
        await db.SaveChangesAsync(ct);
        employee.Department = department;
        employee.Position = position;
        employee.Manager = manager;
        return Mapping.ToDto(employee);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageEmployees(current.Roles));
        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Employé introuvable.");
        if (employee.Status == EmployeeStatus.Archived)
            throw new BusinessRuleException("Ce dossier est déjà archivé.");

        var reports = await db.Employees.CountAsync(item => item.ManagerId == id && item.Status == EmployeeStatus.Active, ct);
        if (reports > 0)
            throw new BusinessRuleException("Réaffectez les collaborateurs de cette personne avant d'archiver son dossier.");

        employee.Status = EmployeeStatus.Archived;
        employee.ArchivedAt = DateTimeOffset.UtcNow;
        AddHistory(employee.Id, "Archivage", "Dossier archivé. Les données sont conservées.");
        audit.Write("employee.archive", "Employee", employee.Id.ToString(), employee.InternalCode);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Employee> Visible() => EmployeeScope.Visible(db.Employees, current);

    private void RequireDirectory() =>
        AccessGuard.Require(
            current.Roles.Contains(AppRoles.Admin)
            || current.Roles.Contains(AppRoles.Rh)
            || current.Roles.Contains(AppRoles.Manager));

    private async Task<(Department Department, Position Position, Employee? Manager)> LoadAssignmentAsync(
        UpsertEmployeeRequest request,
        Guid? employeeId,
        CancellationToken ct)
    {
        var department = await db.Departments.FirstOrDefaultAsync(item => item.Id == request.DepartmentId, ct)
            ?? throw new NotFoundException("Département introuvable.");
        var position = await db.Positions.FirstOrDefaultAsync(item => item.Id == request.PositionId, ct)
            ?? throw new NotFoundException("Poste introuvable.");
        if (position.DepartmentId != department.Id)
            throw new BusinessRuleException("Le poste n'appartient pas au département choisi.");

        Employee? manager = null;
        if (request.ManagerId is Guid managerId)
        {
            if (employeeId == managerId)
                throw new BusinessRuleException("Un employé ne peut pas être son propre responsable.");
            manager = await db.Employees.FirstOrDefaultAsync(item => item.Id == managerId && item.Status == EmployeeStatus.Active, ct)
                ?? throw new BusinessRuleException("Le responsable indiqué est introuvable ou archivé.");
        }

        return (department, position, manager);
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var last = await db.Employees.OrderByDescending(employee => employee.InternalCode)
            .Select(employee => employee.InternalCode)
            .FirstOrDefaultAsync(ct);
        var next = 1;
        if (last is not null && last.StartsWith("EMP-") && int.TryParse(last[4..], out var current))
            next = current + 1;
        return $"EMP-{next:0000}";
    }

    private async Task EnsureBalancesAsync(Guid employeeId, int year, CancellationToken ct)
    {
        var types = await db.LeaveTypes.AsNoTracking().ToListAsync(ct);
        var existing = await db.LeaveBalances
            .Where(balance => balance.EmployeeId == employeeId && balance.Year == year)
            .Select(balance => balance.LeaveTypeId)
            .ToListAsync(ct);
        foreach (var type in types.Where(type => !existing.Contains(type.Id)))
        {
            db.LeaveBalances.Add(new LeaveBalance
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                LeaveTypeId = type.Id,
                Year = year,
                AllocatedDays = type.AnnualAllowance
            });
        }
    }

    private void AddHistory(Guid employeeId, string kind, string description)
    {
        db.EmploymentEvents.Add(new EmploymentEvent
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            OccurredAt = DateTimeOffset.UtcNow,
            Kind = kind,
            Description = description
        });
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
