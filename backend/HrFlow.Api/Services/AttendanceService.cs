using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrFlow.Api.Services;

public interface IAttendanceService
{
    Task<AttendanceDto> CheckInAsync(CancellationToken ct);
    Task<AttendanceDto> CheckOutAsync(CancellationToken ct);
    Task<IReadOnlyList<AttendanceDto>> ListAsync(AttendanceListQuery query, CancellationToken ct);
    Task<AttendanceDto> CorrectAsync(CorrectAttendanceRequest request, CancellationToken ct);
    Task<AttendanceReportDto> ReportAsync(DateOnly from, DateOnly to, Guid? departmentId, CancellationToken ct);
}

public sealed class AttendanceService(
    AppDbContext db,
    ICurrentUser current,
    IOptions<AttendanceOptions> options,
    IAuditWriter audit) : IAttendanceService
{
    private readonly TimeOnly _workdayStart = options.Value.StartTime;

    public async Task<AttendanceDto> CheckInAsync(CancellationToken ct)
    {
        var employee = await CurrentEmployeeAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await db.AttendanceRecords.AnyAsync(record => record.EmployeeId == employee.Id && record.Date == today, ct))
            throw new BusinessRuleException("Une présence est déjà enregistrée pour aujourd'hui.");

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Date = today,
            CheckIn = now,
            Status = AttendanceRules.ResolveStatus(now, _workdayStart)
        };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return await MapAsync(record.Id, ct);
    }

    public async Task<AttendanceDto> CheckOutAsync(CancellationToken ct)
    {
        var employee = await CurrentEmployeeAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(item => item.EmployeeId == employee.Id && item.Date == today, ct)
            ?? throw new BusinessRuleException("Aucun pointage d'arrivée pour aujourd'hui.");
        if (record.CheckOut is not null)
            throw new BusinessRuleException("Le départ est déjà enregistré.");

        var now = TimeOnly.FromDateTime(DateTime.Now);
        if (!AttendanceRules.IsCheckoutValid(record.CheckIn, now))
            throw new BusinessRuleException("L'heure de départ ne peut pas précéder l'arrivée.");
        record.CheckOut = now;
        await db.SaveChangesAsync(ct);
        return await MapAsync(record.Id, ct);
    }

    public async Task<IReadOnlyList<AttendanceDto>> ListAsync(AttendanceListQuery query, CancellationToken ct)
    {
        var (from, to) = Range(query.From, query.To);
        var visible = EmployeeScope.Visible(db.Employees.AsNoTracking(), current).Select(employee => employee.Id);
        var records = db.AttendanceRecords.AsNoTracking()
            .Include(record => record.Employee)!.ThenInclude(employee => employee!.Department)
            .Where(record => visible.Contains(record.EmployeeId) && record.Date >= from && record.Date <= to);
        if (query.EmployeeId is Guid employeeId)
            records = records.Where(record => record.EmployeeId == employeeId);
        if (query.DepartmentId is Guid departmentId)
            records = records.Where(record => record.Employee!.DepartmentId == departmentId);
        var items = await records.OrderByDescending(record => record.Date).ThenBy(record => record.Employee!.LastName).ToListAsync(ct);
        return items.Select(ToDto).ToArray();
    }

    public async Task<AttendanceDto> CorrectAsync(CorrectAttendanceRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == request.EmployeeId, ct)
            ?? throw new NotFoundException("Employé introuvable.");
        var isManager = current.EmployeeId is Guid me && employee.ManagerId == me;
        AccessGuard.Require(HrAccess.CanCorrectAttendance(current.Roles, isManager));
        if (!AttendanceRules.IsCheckoutValid(request.CheckIn, request.CheckOut))
            throw new BusinessRuleException("L'heure de départ ne peut pas précéder l'arrivée.");

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(
            item => item.EmployeeId == employee.Id && item.Date == request.Date, ct);
        if (record is null)
        {
            record = new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = employee.Id, Date = request.Date };
            db.AttendanceRecords.Add(record);
        }

        record.CheckIn = request.Status == AttendanceStatus.Absent ? null : request.CheckIn;
        record.CheckOut = request.Status == AttendanceStatus.Absent ? null : request.CheckOut;
        record.Status = request.Status ?? AttendanceRules.ResolveStatus(record.CheckIn, _workdayStart);
        record.Note = request.Note?.Trim();
        record.CorrectedByUserId = current.UserId;
        audit.Write("attendance.correct", "Attendance", record.Id.ToString(), request.Date.ToString());
        await db.SaveChangesAsync(ct);
        return await MapAsync(record.Id, ct);
    }

    public async Task<AttendanceReportDto> ReportAsync(DateOnly from, DateOnly to, Guid? departmentId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (to > today)
            to = today;
        if (to < from)
            return new AttendanceReportDto(0, 0, 0, []);

        var employees = EmployeeScope.Visible(db.Employees.AsNoTracking(), current)
            .Include(employee => employee.Department)
            .Where(employee => employee.Status == EmployeeStatus.Active);
        if (departmentId is Guid department)
            employees = employees.Where(employee => employee.DepartmentId == department);
        var people = await employees.ToListAsync(ct);
        var ids = people.Select(employee => employee.Id).ToArray();
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(record => ids.Contains(record.EmployeeId) && record.Date >= from && record.Date <= to)
            .ToListAsync(ct);
        var byEmployeeDate = records.ToDictionary(record => (record.EmployeeId, record.Date));

        var rows = new Dictionary<Guid, (string Name, int Present, int Late, int Absent)>();
        foreach (var employee in people)
        {
            if (!rows.ContainsKey(employee.DepartmentId))
                rows[employee.DepartmentId] = (employee.Department?.Name ?? string.Empty, 0, 0, 0);

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    continue;
                var status = byEmployeeDate.TryGetValue((employee.Id, day), out var record)
                    ? record.Status
                    : AttendanceStatus.Absent;
                var currentRow = rows[employee.DepartmentId];
                rows[employee.DepartmentId] = status switch
                {
                    AttendanceStatus.Present => (currentRow.Name, currentRow.Present + 1, currentRow.Late, currentRow.Absent),
                    AttendanceStatus.Late => (currentRow.Name, currentRow.Present, currentRow.Late + 1, currentRow.Absent),
                    _ => (currentRow.Name, currentRow.Present, currentRow.Late, currentRow.Absent + 1)
                };
            }
        }

        var reportRows = rows.Select(pair => new AttendanceReportRowDto(pair.Key, pair.Value.Name, pair.Value.Present, pair.Value.Late, pair.Value.Absent)).ToArray();
        return new AttendanceReportDto(reportRows.Sum(row => row.Present), reportRows.Sum(row => row.Late), reportRows.Sum(row => row.Absent), reportRows);
    }

    private async Task<Employee> CurrentEmployeeAsync(CancellationToken ct)
    {
        if (current.EmployeeId is not Guid id)
            throw new BusinessRuleException("Aucun dossier employé n'est lié à ce compte.");
        return await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id && employee.Status == EmployeeStatus.Active, ct)
            ?? throw new BusinessRuleException("Le dossier employé lié à ce compte est introuvable ou archivé.");
    }

    private async Task<AttendanceDto> MapAsync(Guid id, CancellationToken ct)
    {
        var record = await db.AttendanceRecords.AsNoTracking()
            .Include(item => item.Employee)!.ThenInclude(employee => employee!.Department)
            .FirstAsync(item => item.Id == id, ct);
        return ToDto(record);
    }

    private static AttendanceDto ToDto(AttendanceRecord record) => new(
        record.Id,
        record.EmployeeId,
        record.Employee is null ? string.Empty : Mapping.FullName(record.Employee),
        record.Employee?.DepartmentId ?? Guid.Empty,
        record.Employee?.Department?.Name ?? string.Empty,
        record.Date,
        record.CheckIn,
        record.CheckOut,
        record.Status,
        record.Note);

    private static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
    }
}
