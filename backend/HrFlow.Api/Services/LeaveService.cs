using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface ILeaveService
{
    Task<IReadOnlyList<LeaveTypeDto>> TypesAsync(CancellationToken ct);
    Task<IReadOnlyList<LeaveBalanceDto>> BalancesAsync(Guid? employeeId, CancellationToken ct);
    Task<IReadOnlyList<LeaveRequestDto>> ListAsync(LeaveListQuery query, CancellationToken ct);
    Task<LeaveRequestDto> SubmitAsync(CreateLeaveRequest request, CancellationToken ct);
    Task<LeaveRequestDto> DecideAsync(Guid id, LeaveDecisionRequest request, CancellationToken ct);
}

public sealed class LeaveService(
    AppDbContext db,
    ICurrentUser current,
    INotificationService notifications,
    IAuditWriter audit) : ILeaveService
{
    public async Task<IReadOnlyList<LeaveTypeDto>> TypesAsync(CancellationToken ct) =>
        await db.LeaveTypes.AsNoTracking()
            .OrderBy(type => type.Name)
            .Select(type => new LeaveTypeDto(type.Id, type.Name, type.AnnualAllowance))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LeaveBalanceDto>> BalancesAsync(Guid? employeeId, CancellationToken ct)
    {
        var target = await ResolveReadableEmployeeAsync(employeeId, ct);
        var year = DateTime.UtcNow.Year;
        await EnsureBalancesAsync(target.Id, year, ct);
        await db.SaveChangesAsync(ct);
        return await BalanceDtosAsync(target.Id, year, ct);
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> ListAsync(LeaveListQuery query, CancellationToken ct)
    {
        var visible = EmployeeScope.Visible(db.Employees.AsNoTracking(), current).Select(employee => employee.Id);
        var requests = db.LeaveRequests.AsNoTracking()
            .Include(request => request.Employee)
            .Include(request => request.LeaveType)
            .Where(request => visible.Contains(request.EmployeeId));
        if (query.Status is LeaveStatus status)
            requests = requests.Where(request => request.Status == status);
        if (query.EmployeeId is Guid employeeId)
            requests = requests.Where(request => request.EmployeeId == employeeId);
        var items = await requests.OrderByDescending(request => request.CreatedAt).ToListAsync(ct);
        return items.Select(Mapping.ToDto).ToArray();
    }

    public async Task<LeaveRequestDto> SubmitAsync(CreateLeaveRequest request, CancellationToken ct)
    {
        var employee = await ResolveWritableEmployeeAsync(request.EmployeeId, ct);
        if (employee.Status != EmployeeStatus.Active)
            throw new BusinessRuleException("Un employé archivé ne peut pas poser de congés.");
        if (!LeaveRules.IsDateRangeValid(request.StartDate, request.EndDate))
            throw new BusinessRuleException("La date de fin doit être postérieure ou égale à la date de début.");
        if (!LeaveRules.IsSameCalendarYear(request.StartDate, request.EndDate))
            throw new BusinessRuleException("Une demande ne peut pas chevaucher deux années civiles.");

        var days = LeaveRules.CountBusinessDays(request.StartDate, request.EndDate);
        if (days <= 0)
            throw new BusinessRuleException("La période ne contient aucun jour ouvré.");

        var type = await db.LeaveTypes.FirstOrDefaultAsync(item => item.Id == request.LeaveTypeId, ct)
            ?? throw new NotFoundException("Type de congé introuvable.");

        var overlap = await db.LeaveRequests.AnyAsync(item =>
            item.EmployeeId == employee.Id
            && (item.Status == LeaveStatus.Pending || item.Status == LeaveStatus.Approved)
            && item.StartDate <= request.EndDate
            && request.StartDate <= item.EndDate, ct);
        if (overlap)
            throw new BusinessRuleException("Cette période chevauche une demande déjà en cours ou acceptée.");

        await EnsureBalancesAsync(employee.Id, request.StartDate.Year, ct);
        var balance = await db.LeaveBalances.FirstAsync(
            item => item.EmployeeId == employee.Id && item.LeaveTypeId == type.Id && item.Year == request.StartDate.Year, ct);
        var pending = await PendingDaysAsync(employee.Id, type.Id, request.StartDate.Year, ct);
        if (!LeaveRules.HasEnoughBalance(balance.AllocatedDays, balance.UsedDays, pending, days))
            throw new BusinessRuleException("Le solde de congés est insuffisant pour cette demande.");

        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            LeaveTypeId = type.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            BusinessDays = days,
            Reason = request.Reason?.Trim(),
            Status = LeaveStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LeaveRequests.Add(leave);
        audit.Write("leave.submit", "LeaveRequest", leave.Id.ToString(), $"{days} jours");
        await db.SaveChangesAsync(ct);

        await NotifyApproversAsync(employee, leave, type.Name, ct);
        leave.Employee = employee;
        leave.LeaveType = type;
        return Mapping.ToDto(leave);
    }

    public async Task<LeaveRequestDto> DecideAsync(Guid id, LeaveDecisionRequest request, CancellationToken ct)
    {
        if (request.Status is not (LeaveStatus.Approved or LeaveStatus.Rejected))
            throw new BusinessRuleException("La décision doit être une acceptation ou un refus.");

        var leave = await db.LeaveRequests
            .Include(item => item.Employee)
            .Include(item => item.LeaveType)
            .FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Demande introuvable.");
        if (leave.Status != LeaveStatus.Pending)
            throw new BusinessRuleException("Cette demande a déjà été traitée.");
        if (leave.Employee?.UserId is not null && leave.Employee.UserId == current.UserId)
            throw new ForbiddenAccessException("Vous ne pouvez pas décider de votre propre demande.");

        var isManager = current.EmployeeId is Guid me && leave.Employee?.ManagerId == me;
        AccessGuard.Require(HrAccess.CanApproveLeave(current.Roles, isManager));

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        leave.Status = request.Status;
        leave.DecisionComment = request.Comment?.Trim();
        leave.DecidedByUserId = current.UserId;
        leave.DecidedAt = DateTimeOffset.UtcNow;

        if (request.Status == LeaveStatus.Approved)
        {
            var balance = await db.LeaveBalances.FirstOrDefaultAsync(item =>
                item.EmployeeId == leave.EmployeeId
                && item.LeaveTypeId == leave.LeaveTypeId
                && item.Year == leave.StartDate.Year, ct)
                ?? throw new BusinessRuleException("Aucun solde n'est ouvert pour cette année.");
            var pendingWithoutThis = await PendingDaysAsync(leave.EmployeeId, leave.LeaveTypeId, leave.StartDate.Year, ct) - leave.BusinessDays;
            if (!LeaveRules.HasEnoughBalance(balance.AllocatedDays, balance.UsedDays, Math.Max(pendingWithoutThis, 0), leave.BusinessDays))
                throw new BusinessRuleException("Le solde ne couvre plus cette demande.");
            balance.UsedDays += leave.BusinessDays;
        }

        audit.Write("leave.decide", "LeaveRequest", leave.Id.ToString(), request.Status.ToString());
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        if (leave.Employee?.UserId is string employeeUserId)
        {
            var verb = request.Status == LeaveStatus.Approved ? "acceptée" : "refusée";
            await notifications.NotifyAsync(
                employeeUserId,
                $"leave-decision:{leave.Id}:{employeeUserId}",
                "Décision de congé",
                $"Votre demande du {leave.StartDate:yyyy-MM-dd} au {leave.EndDate:yyyy-MM-dd} a été {verb}.",
                ct);
        }

        return Mapping.ToDto(leave);
    }

    private async Task<Employee> ResolveReadableEmployeeAsync(Guid? employeeId, CancellationToken ct)
    {
        var id = employeeId ?? current.EmployeeId ?? throw new BusinessRuleException("Aucun employé n'est associé à ce compte.");
        return await EmployeeScope.Visible(db.Employees, current).FirstOrDefaultAsync(employee => employee.Id == id, ct)
            ?? throw new NotFoundException("Employé introuvable.");
    }

    private async Task<Employee> ResolveWritableEmployeeAsync(Guid? employeeId, CancellationToken ct)
    {
        var id = employeeId ?? current.EmployeeId ?? throw new BusinessRuleException("Aucun employé n'est associé à ce compte.");
        if (!HrAccess.CanManageEmployees(current.Roles) && current.EmployeeId != id)
            throw new ForbiddenAccessException();
        return await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id, ct)
            ?? throw new NotFoundException("Employé introuvable.");
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

    private async Task<IReadOnlyList<LeaveBalanceDto>> BalanceDtosAsync(Guid employeeId, int year, CancellationToken ct)
    {
        var balances = await db.LeaveBalances.AsNoTracking()
            .Where(balance => balance.EmployeeId == employeeId && balance.Year == year)
            .Include(balance => balance.LeaveType)
            .ToListAsync(ct);
        var result = new List<LeaveBalanceDto>();
        foreach (var balance in balances)
        {
            var pending = await PendingDaysAsync(employeeId, balance.LeaveTypeId, year, ct);
            result.Add(new LeaveBalanceDto(
                balance.Id,
                balance.LeaveTypeId,
                balance.LeaveType?.Name ?? string.Empty,
                balance.Year,
                balance.AllocatedDays,
                balance.UsedDays,
                pending,
                balance.AllocatedDays - balance.UsedDays - pending));
        }

        return result;
    }

    private async Task<int> PendingDaysAsync(Guid employeeId, Guid leaveTypeId, int year, CancellationToken ct) =>
        await db.LeaveRequests
            .Where(request => request.EmployeeId == employeeId
                && request.LeaveTypeId == leaveTypeId
                && request.Status == LeaveStatus.Pending
                && request.StartDate.Year == year)
            .SumAsync(request => (int?)request.BusinessDays, ct) ?? 0;

    private async Task NotifyApproversAsync(Employee employee, LeaveRequest leave, string typeName, CancellationToken ct)
    {
        var recipients = new HashSet<string>();
        if (employee.ManagerId is Guid managerId)
        {
            var managerUserId = await db.Employees.Where(item => item.Id == managerId).Select(item => item.UserId).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(managerUserId))
                recipients.Add(managerUserId);
        }

        var rhIds = await db.Users.AsNoTracking()
            .Where(user => user.Role == AppRoles.Rh && user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(ct);
        foreach (var rhId in rhIds)
            recipients.Add(rhId);

        recipients.Remove(current.UserId ?? string.Empty);
        var body = $"{Mapping.FullName(employee)} demande {leave.BusinessDays} jour(s) de {typeName}, du {leave.StartDate:yyyy-MM-dd} au {leave.EndDate:yyyy-MM-dd}.";
        foreach (var userId in recipients)
        {
            await notifications.NotifyAsync(userId, $"leave-submitted:{leave.Id}:{userId}", "Nouvelle demande de congé", body, ct);
        }
    }
}
