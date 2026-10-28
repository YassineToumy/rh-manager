using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IOrganizationService
{
    Task<OrganizationDto> GetAsync(CancellationToken ct);
    Task<DepartmentDto> CreateDepartmentAsync(UpsertDepartmentRequest request, CancellationToken ct);
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, UpsertDepartmentRequest request, CancellationToken ct);
    Task<PositionDto> CreatePositionAsync(UpsertPositionRequest request, CancellationToken ct);
    Task<PositionDto> UpdatePositionAsync(Guid id, UpsertPositionRequest request, CancellationToken ct);
}

public sealed class OrganizationService(AppDbContext db, ICurrentUser current, IAuditWriter audit) : IOrganizationService
{
    public async Task<OrganizationDto> GetAsync(CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking()
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentDto(
                department.Id,
                department.Name,
                department.Description,
                department.Employees.Count(employee => employee.Status == EmployeeStatus.Active)))
            .ToListAsync(ct);

        var positions = await db.Positions.AsNoTracking()
            .OrderBy(position => position.Title)
            .Select(position => new PositionDto(position.Id, position.Title, position.Description, position.DepartmentId, position.Department!.Name))
            .ToListAsync(ct);

        var employees = await EmployeeScope.Visible(db.Employees.AsNoTracking(), current)
            .Where(employee => employee.Status == EmployeeStatus.Active)
            .OrderBy(employee => employee.LastName)
            .Select(employee => new EmployeeSummaryDto(
                employee.Id,
                employee.FirstName + " " + employee.LastName,
                employee.InternalCode,
                employee.ManagerId,
                employee.DepartmentId,
                employee.Position!.Title))
            .ToListAsync(ct);

        return new OrganizationDto(departments, positions, employees);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(UpsertDepartmentRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageOrganization(current.Roles));
        var name = request.Name.Trim();
        if (await db.Departments.AnyAsync(department => department.Name == name, ct))
            throw new BusinessRuleException("Un département porte déjà ce nom.");

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description?.Trim()
        };
        db.Departments.Add(department);
        audit.Write("department.create", "Department", department.Id.ToString(), department.Name);
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(department.Id, department.Name, department.Description, 0);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, UpsertDepartmentRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageOrganization(current.Roles));
        var department = await db.Departments.Include(item => item.Employees).FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Département introuvable.");
        var name = request.Name.Trim();
        if (await db.Departments.AnyAsync(item => item.Id != id && item.Name == name, ct))
            throw new BusinessRuleException("Un département porte déjà ce nom.");

        department.Name = name;
        department.Description = request.Description?.Trim();
        audit.Write("department.update", "Department", department.Id.ToString(), department.Name);
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(department.Id, department.Name, department.Description, department.Employees.Count(employee => employee.Status == EmployeeStatus.Active));
    }

    public async Task<PositionDto> CreatePositionAsync(UpsertPositionRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageOrganization(current.Roles));
        var department = await db.Departments.FirstOrDefaultAsync(item => item.Id == request.DepartmentId, ct)
            ?? throw new NotFoundException("Département introuvable.");
        var title = request.Title.Trim();
        if (await db.Positions.AnyAsync(item => item.DepartmentId == department.Id && item.Title == title, ct))
            throw new BusinessRuleException("Ce poste existe déjà dans le département.");

        var position = new Position
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = request.Description?.Trim(),
            DepartmentId = department.Id
        };
        db.Positions.Add(position);
        audit.Write("position.create", "Position", position.Id.ToString(), position.Title);
        await db.SaveChangesAsync(ct);
        return new PositionDto(position.Id, position.Title, position.Description, department.Id, department.Name);
    }

    public async Task<PositionDto> UpdatePositionAsync(Guid id, UpsertPositionRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageOrganization(current.Roles));
        var position = await db.Positions.Include(item => item.Department).FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Poste introuvable.");
        if (position.DepartmentId != request.DepartmentId
            && await db.Employees.AnyAsync(employee => employee.PositionId == position.Id, ct))
            throw new BusinessRuleException("Des employés occupent ce poste : changez d'abord leur affectation.");

        var department = await db.Departments.FirstOrDefaultAsync(item => item.Id == request.DepartmentId, ct)
            ?? throw new NotFoundException("Département introuvable.");
        var title = request.Title.Trim();
        if (await db.Positions.AnyAsync(item => item.Id != id && item.DepartmentId == department.Id && item.Title == title, ct))
            throw new BusinessRuleException("Ce poste existe déjà dans le département.");

        position.Title = title;
        position.Description = request.Description?.Trim();
        position.DepartmentId = department.Id;
        audit.Write("position.update", "Position", position.Id.ToString(), position.Title);
        await db.SaveChangesAsync(ct);
        return new PositionDto(position.Id, position.Title, position.Description, department.Id, department.Name);
    }
}
