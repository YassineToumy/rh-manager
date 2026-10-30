using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface ITeamFollowUpService
{
    Task<IReadOnlyList<TeamMemberDto>> ReportsAsync(CancellationToken ct);
    Task<TeamFollowUpDto> FollowUpAsync(Guid employeeId, CancellationToken ct);
    Task<MissionDto> AddMissionAsync(Guid employeeId, CreateMissionRequest request, CancellationToken ct);
    Task<MissionDto> UpdateMissionAsync(Guid missionId, UpdateMissionRequest request, CancellationToken ct);
    Task<WeeklyRatingDto> SetRatingAsync(Guid employeeId, SetWeeklyRatingRequest request, CancellationToken ct);
}

public sealed class TeamFollowUpService(AppDbContext db, ICurrentUser current, IAuditWriter audit) : ITeamFollowUpService
{
    private static readonly string[] Statuses = ["Backlog", "InProgress", "Done"];

    public async Task<IReadOnlyList<TeamMemberDto>> ReportsAsync(CancellationToken ct)
    {
        var me = ManagerId();
        return await db.Employees.AsNoTracking()
            .Where(employee => employee.ManagerId == me && employee.Status == EmployeeStatus.Active)
            .OrderBy(employee => employee.LastName)
            .Select(employee => new TeamMemberDto(employee.Id, employee.FirstName + " " + employee.LastName, employee.Position!.Title))
            .ToListAsync(ct);
    }

    public async Task<TeamFollowUpDto> FollowUpAsync(Guid employeeId, CancellationToken ct)
    {
        await RequireReportAsync(employeeId, ct);
        var missions = await db.WorkMissions.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.WeekStart)
            .Select(item => new MissionDto(item.Id, item.Title, item.Detail, item.Status, item.Comment, item.WeekStart, item.ManagerName, item.CreatedAt))
            .ToListAsync(ct);
        var ratings = await db.WeeklyRatings.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.WeekStart)
            .Select(item => new WeeklyRatingDto(item.Id, item.WeekStart, item.Score, item.Comment, item.ManagerName))
            .ToListAsync(ct);
        return new TeamFollowUpDto(missions, ratings);
    }

    public async Task<MissionDto> AddMissionAsync(Guid employeeId, CreateMissionRequest request, CancellationToken ct)
    {
        await RequireReportAsync(employeeId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mission = new WorkMission
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            Title = request.Title.Trim(),
            Detail = Clean(request.Detail),
            Status = "Backlog",
            Comment = Clean(request.Comment),
            WeekStart = Monday(today),
            ManagerName = await ManagerNameAsync(ct),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.WorkMissions.Add(mission);
        audit.Write("mission.create", "WorkMission", mission.Id.ToString(), mission.Title);
        await db.SaveChangesAsync(ct);
        return ToDto(mission);
    }

    public async Task<MissionDto> UpdateMissionAsync(Guid missionId, UpdateMissionRequest request, CancellationToken ct)
    {
        if (!Statuses.Contains(request.Status))
            throw new BusinessRuleException("Le statut doit être backlog, en cours ou terminée.");
        var mission = await db.WorkMissions.FirstOrDefaultAsync(item => item.Id == missionId, ct)
            ?? throw new NotFoundException("Mission introuvable.");
        await RequireReportAsync(mission.EmployeeId, ct);
        mission.Status = request.Status;
        mission.Comment = Clean(request.Comment);
        audit.Write("mission.update", "WorkMission", mission.Id.ToString(), mission.Status);
        await db.SaveChangesAsync(ct);
        return ToDto(mission);
    }

    public async Task<WeeklyRatingDto> SetRatingAsync(Guid employeeId, SetWeeklyRatingRequest request, CancellationToken ct)
    {
        await RequireReportAsync(employeeId, ct);
        var week = Monday(request.WeekStart);
        var rating = await db.WeeklyRatings.FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.WeekStart == week, ct);
        if (rating is null)
        {
            rating = new WeeklyRating { Id = Guid.NewGuid(), EmployeeId = employeeId, WeekStart = week };
            db.WeeklyRatings.Add(rating);
        }
        rating.Score = request.Score;
        rating.Comment = Clean(request.Comment);
        rating.ManagerName = await ManagerNameAsync(ct);
        audit.Write("rating.set", "WeeklyRating", rating.Id.ToString(), rating.Score.ToString());
        await db.SaveChangesAsync(ct);
        return new WeeklyRatingDto(rating.Id, rating.WeekStart, rating.Score, rating.Comment, rating.ManagerName);
    }

    private Guid ManagerId()
    {
        if (!current.Roles.Contains(AppRoles.Manager) || current.EmployeeId is not Guid me)
            throw new ForbiddenAccessException();
        return me;
    }

    private async Task RequireReportAsync(Guid employeeId, CancellationToken ct)
    {
        var me = ManagerId();
        var allowed = await db.Employees.AnyAsync(employee => employee.Id == employeeId && employee.ManagerId == me, ct);
        if (!allowed)
            throw new ForbiddenAccessException();
    }

    private async Task<string> ManagerNameAsync(CancellationToken ct)
    {
        var me = ManagerId();
        var manager = await db.Employees.AsNoTracking().FirstAsync(employee => employee.Id == me, ct);
        return $"{manager.FirstName} {manager.LastName}";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly Monday(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static MissionDto ToDto(WorkMission mission) =>
        new(mission.Id, mission.Title, mission.Detail, mission.Status, mission.Comment, mission.WeekStart, mission.ManagerName, mission.CreatedAt);
}
