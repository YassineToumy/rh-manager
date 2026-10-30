using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IEmployeeSpaceService
{
    Task<EmployeeSpaceDto> GetAsync(CancellationToken ct);
    Task<LeaveRequestDto> RequestLeaveAsync(CreateLeaveRequest request, CancellationToken ct);
    Task<SalaryAdvanceDto> RequestAdvanceAsync(CreateAdvanceRequest request, CancellationToken ct);
}

public sealed class EmployeeSpaceService(AppDbContext db, ICurrentUser current, ILeaveService leave, IAuditWriter audit) : IEmployeeSpaceService
{
    public async Task<EmployeeSpaceDto> GetAsync(CancellationToken ct)
    {
        var employee = await OwnEmployeeAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var compensation = await db.EmployeeCompensations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.EmployeeId == employee.Id, ct);
        var deposit = await db.SalaryDeposits.AsNoTracking()
            .FirstOrDefaultAsync(item => item.EmployeeId == employee.Id && item.Year == today.Year && item.Month == today.Month, ct);
        var advances = await db.SalaryAdvances.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new SalaryAdvanceDto(item.Id, item.Amount, item.Reason, item.Status, item.CreatedAt))
            .ToListAsync(ct);
        var ratings = await db.WeeklyRatings.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id)
            .OrderByDescending(item => item.WeekStart)
            .Select(item => new WeeklyRatingDto(item.Id, item.WeekStart, item.Score, item.Comment, item.ManagerName))
            .ToListAsync(ct);
        var missions = await db.WorkMissions.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id)
            .OrderByDescending(item => item.WeekStart)
            .ThenBy(item => item.CreatedAt)
            .Select(item => new MissionDto(item.Id, item.Title, item.Detail, item.Status, item.Comment, item.WeekStart, item.ManagerName, item.CreatedAt))
            .ToListAsync(ct);
        var accrual = await AccrualAsync(employee, today, ct);

        return new EmployeeSpaceDto(
            compensation?.PositionNumber ?? employee.InternalCode,
            employee.ProfessionalEmail,
            compensation?.BankRib ?? "",
            compensation?.BaseSalary ?? 0,
            compensation?.Bonus ?? 0,
            deposit?.IsPaid ?? false,
            today.Year,
            today.Month,
            await HoursThisMonthAsync(employee.Id, today, ct),
            await leave.BalancesAsync(employee.Id, ct),
            await leave.ListAsync(new LeaveListQuery { EmployeeId = employee.Id }, ct),
            advances,
            ratings,
            compensation?.ChipNumber,
            compensation?.ChipCredit,
            accrual,
            await WeekHoursAsync(employee.Id, today, ct),
            missions,
            await SalaryCutAsync(employee.Id, compensation?.BaseSalary ?? 0, today, ct));
    }

    public async Task<LeaveRequestDto> RequestLeaveAsync(CreateLeaveRequest request, CancellationToken ct)
    {
        var employee = await OwnEmployeeAsync(ct);
        if (employee.Status != EmployeeStatus.Active)
            throw new BusinessRuleException("Un employé archivé ne peut pas poser de congés.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("Indiquez pourquoi vous demandez ce congé.");
        if (!LeaveRules.IsDateRangeValid(request.StartDate, request.EndDate))
            throw new BusinessRuleException("La date de fin doit être postérieure ou égale à la date de début.");
        if (!LeaveRules.IsSameCalendarYear(request.StartDate, request.EndDate))
            throw new BusinessRuleException("Une demande ne peut pas chevaucher deux années civiles.");

        var halfDay = request.HalfDay;
        string? part = null;
        if (halfDay)
        {
            if (request.StartDate != request.EndDate)
                throw new BusinessRuleException("Une demi-journée concerne un seul jour.");
            part = request.HalfDayPart?.Trim();
            if (part is not ("Morning" or "Afternoon"))
                throw new BusinessRuleException("Choisissez le matin ou l'après-midi.");
        }

        var businessDays = LeaveRules.CountBusinessDays(request.StartDate, request.EndDate);
        var requested = halfDay ? 0.5m : businessDays;
        if (halfDay && businessDays == 0)
            throw new BusinessRuleException("La période ne contient aucun jour ouvré.");
        if (requested <= 0)
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var accrual = await AccrualAsync(employee, today, ct);
        var (_, unpaid) = LeaveRules.Charge(accrual.RemainingDays, requested);
        var leaveRequest = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            LeaveTypeId = type.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            BusinessDays = halfDay ? 0 : businessDays,
            IsHalfDay = halfDay,
            HalfDayPart = part,
            UnpaidDays = unpaid,
            Reason = request.Reason.Trim(),
            Status = LeaveStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LeaveRequests.Add(leaveRequest);
        audit.Write("leave.submit", "LeaveRequest", leaveRequest.Id.ToString(), unpaid > 0 ? $"{requested} j dont {unpaid} non couverts" : $"{requested} j");
        await db.SaveChangesAsync(ct);
        leaveRequest.Employee = employee;
        leaveRequest.LeaveType = type;
        return Mapping.ToDto(leaveRequest);
    }

    public async Task<SalaryAdvanceDto> RequestAdvanceAsync(CreateAdvanceRequest request, CancellationToken ct)
    {
        var employee = await OwnEmployeeAsync(ct);
        var advance = new SalaryAdvance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Amount = request.Amount,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.SalaryAdvances.Add(advance);
        audit.Write("advance.request", "SalaryAdvance", advance.Id.ToString(), advance.Amount.ToString());
        await db.SaveChangesAsync(ct);
        return new SalaryAdvanceDto(advance.Id, advance.Amount, advance.Reason, advance.Status, advance.CreatedAt);
    }

    private async Task<Employee> OwnEmployeeAsync(CancellationToken ct)
    {
        if (!current.Roles.Contains(AppRoles.Employee) || string.IsNullOrEmpty(current.UserId))
            throw new ForbiddenAccessException();

        var employee = await db.Employees.FirstOrDefaultAsync(item => item.UserId == current.UserId, ct);
        if (employee is null && current.EmployeeId is Guid claimed)
            employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == claimed, ct);
        return employee ?? await OpenDossierAsync(current.UserId, ct);
    }

    private async Task<Employee> OpenDossierAsync(string userId, CancellationToken ct)
    {
        var account = await db.Users.FirstOrDefaultAsync(user => user.Id == userId, ct)
            ?? throw new NotFoundException("Compte introuvable.");
        var department = await db.Departments.OrderBy(item => item.Name).FirstOrDefaultAsync(ct);
        if (department is null)
        {
            department = new Department { Id = Guid.NewGuid(), Name = "Général" };
            db.Departments.Add(department);
        }
        var position = await db.Positions
            .Where(item => item.DepartmentId == department.Id)
            .OrderBy(item => item.Title)
            .FirstOrDefaultAsync(ct);
        if (position is null)
        {
            position = new Position { Id = Guid.NewGuid(), Title = "Collaborateur", DepartmentId = department.Id };
            db.Positions.Add(position);
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            InternalCode = await NextCodeAsync(ct),
            FirstName = account.FirstName,
            LastName = account.LastName,
            ProfessionalEmail = account.Email ?? $"{userId}@hrflow.local",
            HireDate = DateOnly.FromDateTime(account.CreatedAt.UtcDateTime),
            ContractType = "CDI",
            Status = EmployeeStatus.Active,
            DepartmentId = department.Id,
            PositionId = position.Id,
            UserId = userId
        };
        db.Employees.Add(employee);
        db.EmployeeCompensations.Add(new EmployeeCompensation
        {
            EmployeeId = employee.Id,
            PositionNumber = employee.InternalCode,
            BankRib = "",
            BaseSalary = 0,
            Bonus = 0
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return employee;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.Employees.FirstOrDefaultAsync(item => item.UserId == userId, ct)
                ?? throw new BusinessRuleException("Le dossier n'a pas pu être ouvert.");
        }
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var last = await db.Employees.OrderByDescending(employee => employee.InternalCode)
            .Select(employee => employee.InternalCode)
            .FirstOrDefaultAsync(ct);
        var next = 1;
        if (last is not null && last.StartsWith("EMP-") && int.TryParse(last[4..], out var currentCode))
            next = currentCode + 1;
        return $"EMP-{next:0000}";
    }

    private async Task<decimal> HoursThisMonthAsync(Guid employeeId, DateOnly today, CancellationToken ct)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.Date >= start && record.Date <= today)
            .ToListAsync(ct);
        var minutes = 0d;
        foreach (var record in records)
        {
            if (record.CheckIn is TimeOnly checkIn && record.CheckOut is TimeOnly checkOut && checkOut > checkIn)
                minutes += (checkOut - checkIn).TotalMinutes;
        }
        return Math.Round((decimal)(minutes / 60d), 1);
    }

    private async Task<LeaveAccrualDto> AccrualAsync(Employee employee, DateOnly today, CancellationToken ct)
    {
        var createdAt = employee.UserId is null
            ? (DateTimeOffset?)null
            : await db.Users.AsNoTracking()
                .Where(user => user.Id == employee.UserId)
                .Select(user => (DateTimeOffset?)user.CreatedAt)
                .FirstOrDefaultAsync(ct);
        var createdOn = createdAt is DateTimeOffset stamp
            ? DateOnly.FromDateTime(stamp.UtcDateTime)
            : employee.HireDate;
        var months = LeaveRules.CompletedMonths(createdOn, today);
        var accrued = LeaveRules.AccruedLeaveDays(createdOn, today);
        var openLeaves = await db.LeaveRequests.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id && (item.Status == LeaveStatus.Pending || item.Status == LeaveStatus.Approved))
            .Select(item => new { item.IsHalfDay, item.BusinessDays, item.UnpaidDays })
            .ToListAsync(ct);
        var reserved = openLeaves.Sum(item => LeaveRules.CoveredDays(item.IsHalfDay, item.BusinessDays, item.UnpaidDays));
        var remaining = Math.Max(0m, accrued - reserved);
        return new LeaveAccrualDto(createdOn, months, accrued, reserved, remaining);
    }

    private async Task<IReadOnlyList<WeekHoursDto>> WeekHoursAsync(Guid employeeId, DateOnly today, CancellationToken ct)
    {
        var currentWeek = Monday(today);
        var firstWeek = currentWeek.AddDays(-7 * 5);
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.Date >= firstWeek && record.Date <= today)
            .ToListAsync(ct);
        var weeks = new List<WeekHoursDto>();
        for (var week = currentWeek; week >= firstWeek; week = week.AddDays(-7))
        {
            var end = week.AddDays(6);
            var minutes = records
                .Where(record => record.Date >= week && record.Date <= end && record.CheckIn is not null && record.CheckOut is not null && record.CheckOut > record.CheckIn)
                .Sum(record => (record.CheckOut!.Value - record.CheckIn!.Value).TotalMinutes);
            weeks.Add(new WeekHoursDto(week, Math.Round((decimal)(minutes / 60d), 1)));
        }
        return weeks;
    }

    private async Task<SalaryCutDto> SalaryCutAsync(Guid employeeId, decimal baseSalary, DateOnly today, CancellationToken ct)
    {
        var unpaid = await db.LeaveRequests.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId
                && (item.Status == LeaveStatus.Pending || item.Status == LeaveStatus.Approved)
                && item.UnpaidDays > 0
                && item.StartDate.Year == today.Year
                && item.StartDate.Month == today.Month)
            .SumAsync(item => item.UnpaidDays, ct);
        return new SalaryCutDto(
            unpaid,
            LeaveRules.DailyRate(baseSalary, today.Year, today.Month),
            LeaveRules.SalaryDeduction(baseSalary, today.Year, today.Month, unpaid));
    }

    private static DateOnly Monday(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
