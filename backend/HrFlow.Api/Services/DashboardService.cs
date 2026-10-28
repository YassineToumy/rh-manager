using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(DashboardQuery query, CancellationToken ct);
}

public sealed class DashboardService(AppDbContext db, ICurrentUser current, IAttendanceService attendance) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(DashboardQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = query.To ?? today;
        var visible = EmployeeScope.Visible(db.Employees.AsNoTracking(), current);
        if (query.DepartmentId is Guid departmentId)
            visible = visible.Where(employee => employee.DepartmentId == departmentId);

        var active = visible.Where(employee => employee.Status == EmployeeStatus.Active);
        var employeeCount = await active.CountAsync(ct);
        var byDepartment = await active
            .GroupBy(employee => new { employee.DepartmentId, employee.Department!.Name })
            .Select(group => new DepartmentCountDto(group.Key.DepartmentId, group.Key.Name, group.Count()))
            .OrderBy(item => item.Name)
            .ToListAsync(ct);

        var visibleIds = visible.Select(employee => employee.Id);
        var pendingQuery = db.LeaveRequests.AsNoTracking()
            .Where(request => request.Status == LeaveStatus.Pending && visibleIds.Contains(request.EmployeeId));
        var pendingCount = await pendingQuery.CountAsync(ct);
        var pending = await db.LeaveRequests.AsNoTracking()
            .Include(request => request.Employee)
            .Include(request => request.LeaveType)
            .Where(request => request.Status == LeaveStatus.Pending && visibleIds.Contains(request.EmployeeId))
            .OrderBy(request => request.StartDate)
            .Take(8)
            .ToListAsync(ct);

        var limit = today.AddDays(30);
        var expiringQuery = db.Contracts.AsNoTracking()
            .Where(contract => visibleIds.Contains(contract.EmployeeId)
                && contract.Status == ContractStatus.Active
                && contract.EndDate != null
                && contract.EndDate >= today
                && contract.EndDate <= limit);
        var expiringCount = await expiringQuery.CountAsync(ct);
        var expiring = await db.Contracts.AsNoTracking()
            .Include(contract => contract.Employee)
            .Where(contract => visibleIds.Contains(contract.EmployeeId)
                && contract.Status == ContractStatus.Active
                && contract.EndDate != null
                && contract.EndDate >= today
                && contract.EndDate <= limit)
            .OrderBy(contract => contract.EndDate)
            .Take(8)
            .ToListAsync(ct);

        var report = await attendance.ReportAsync(from, to, query.DepartmentId, ct);
        var applications = HrAccess.CanManageRecruitment(current.Roles)
            ? await db.JobOffers.AsNoTracking()
                .Where(offer => offer.Status == JobOfferStatus.Published)
                .Select(offer => new OfferCandidateCountDto(offer.Id, offer.Title, offer.Candidates.Count))
                .ToListAsync(ct)
            : [];

        return new DashboardDto(
            employeeCount,
            pendingCount,
            expiringCount,
            byDepartment,
            pending.Select(Mapping.ToDto).ToArray(),
            expiring.Select(contract => Mapping.ToDto(contract, today)).ToArray(),
            report,
            applications);
    }
}
