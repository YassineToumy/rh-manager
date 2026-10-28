using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IPerformanceService
{
    Task<IReadOnlyList<GoalDto>> ListGoalsAsync(Guid? employeeId, CancellationToken ct);
    Task<GoalDto> CreateGoalAsync(UpsertGoalRequest request, CancellationToken ct);
    Task<GoalDto> UpdateGoalAsync(Guid id, UpsertGoalRequest request, CancellationToken ct);
    Task<IReadOnlyList<ReviewDto>> ListReviewsAsync(Guid? employeeId, CancellationToken ct);
    Task<ReviewDto> CreateReviewAsync(CreateReviewRequest request, CancellationToken ct);
    Task<ReviewDto> CompleteReviewAsync(Guid id, CompleteReviewRequest request, CancellationToken ct);
}

public sealed class PerformanceService(AppDbContext db, ICurrentUser current, IAuditWriter audit) : IPerformanceService
{
    public async Task<IReadOnlyList<GoalDto>> ListGoalsAsync(Guid? employeeId, CancellationToken ct)
    {
        var visible = EmployeeScope.Visible(db.Employees.AsNoTracking(), current).Select(employee => employee.Id);
        var goals = db.PerformanceGoals.AsNoTracking().Include(goal => goal.Employee).Where(goal => visible.Contains(goal.EmployeeId));
        if (employeeId is Guid id)
            goals = goals.Where(goal => goal.EmployeeId == id);
        return (await goals.OrderBy(goal => goal.TargetDate).ToListAsync(ct)).Select(ToDto).ToArray();
    }

    public async Task<GoalDto> CreateGoalAsync(UpsertGoalRequest request, CancellationToken ct)
    {
        var employee = await RequireManagedEmployeeAsync(request.EmployeeId, ct);
        var goal = new PerformanceGoal
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            TargetDate = request.TargetDate,
            Progress = request.Progress,
            Status = request.Status
        };
        db.PerformanceGoals.Add(goal);
        audit.Write("goal.create", "PerformanceGoal", goal.Id.ToString(), goal.Title);
        await db.SaveChangesAsync(ct);
        goal.Employee = employee;
        return ToDto(goal);
    }

    public async Task<GoalDto> UpdateGoalAsync(Guid id, UpsertGoalRequest request, CancellationToken ct)
    {
        var goal = await db.PerformanceGoals.Include(item => item.Employee).FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Objectif introuvable.");
        await RequireManagedEmployeeAsync(goal.EmployeeId, ct);
        if (goal.EmployeeId != request.EmployeeId)
            throw new BusinessRuleException("Un objectif ne change pas d'employé.");
        goal.Title = request.Title.Trim();
        goal.Description = request.Description?.Trim();
        goal.TargetDate = request.TargetDate;
        goal.Progress = request.Progress;
        goal.Status = request.Status;
        audit.Write("goal.update", "PerformanceGoal", goal.Id.ToString(), $"{goal.Progress}%");
        await db.SaveChangesAsync(ct);
        return ToDto(goal);
    }

    public async Task<IReadOnlyList<ReviewDto>> ListReviewsAsync(Guid? employeeId, CancellationToken ct)
    {
        var visible = EmployeeScope.Visible(db.Employees.AsNoTracking(), current).Select(employee => employee.Id);
        var reviews = db.PerformanceReviews.AsNoTracking()
            .Include(review => review.Employee)
            .Include(review => review.Reviewer)
            .Where(review => visible.Contains(review.EmployeeId));
        if (employeeId is Guid id)
            reviews = reviews.Where(review => review.EmployeeId == id);
        return (await reviews.OrderByDescending(review => review.ScheduledAt).ToListAsync(ct)).Select(ToDto).ToArray();
    }

    public async Task<ReviewDto> CreateReviewAsync(CreateReviewRequest request, CancellationToken ct)
    {
        var employee = await RequireManagedEmployeeAsync(request.EmployeeId, ct);
        var reviewer = await db.Employees.FirstOrDefaultAsync(item => item.Id == request.ReviewerEmployeeId && item.Status == EmployeeStatus.Active, ct)
            ?? throw new NotFoundException("Évaluateur introuvable.");
        var review = new PerformanceReview
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            ReviewerEmployeeId = reviewer.Id,
            ScheduledAt = request.ScheduledAt
        };
        db.PerformanceReviews.Add(review);
        audit.Write("review.create", "PerformanceReview", review.Id.ToString(), employee.InternalCode);
        await db.SaveChangesAsync(ct);
        review.Employee = employee;
        review.Reviewer = reviewer;
        return ToDto(review);
    }

    public async Task<ReviewDto> CompleteReviewAsync(Guid id, CompleteReviewRequest request, CancellationToken ct)
    {
        var review = await db.PerformanceReviews.Include(item => item.Employee).Include(item => item.Reviewer)
            .FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Évaluation introuvable.");
        await RequireManagedEmployeeAsync(review.EmployeeId, ct);
        if (review.CompletedAt is not null)
            throw new BusinessRuleException("Cette évaluation est déjà terminée.");
        review.Comment = request.Comment.Trim();
        review.CompletedAt = DateTimeOffset.UtcNow;
        audit.Write("review.complete", "PerformanceReview", review.Id.ToString(), null);
        await db.SaveChangesAsync(ct);
        return ToDto(review);
    }

    private async Task<Employee> RequireManagedEmployeeAsync(Guid employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, ct)
            ?? throw new NotFoundException("Employé introuvable.");
        var isManager = current.EmployeeId is Guid me && (employee.Id == me || employee.ManagerId == me);
        var canManage = HrAccess.CanManageEmployees(current.Roles)
            || (current.Roles.Contains(AppRoles.Manager) && isManager);
        AccessGuard.Require(canManage);
        if (current.Roles.Contains(AppRoles.Employee)
            && !current.Roles.Contains(AppRoles.Manager)
            && !HrAccess.CanManageEmployees(current.Roles)
            && current.EmployeeId != employee.Id)
            throw new ForbiddenAccessException();
        return employee;
    }

    private static GoalDto ToDto(PerformanceGoal goal) => new(
        goal.Id,
        goal.EmployeeId,
        goal.Employee is null ? string.Empty : Mapping.FullName(goal.Employee),
        goal.Title,
        goal.Description,
        goal.TargetDate,
        goal.Progress,
        goal.Status);

    private static ReviewDto ToDto(PerformanceReview review) => new(
        review.Id,
        review.EmployeeId,
        review.Employee is null ? string.Empty : Mapping.FullName(review.Employee),
        review.ReviewerEmployeeId,
        review.Reviewer is null ? string.Empty : Mapping.FullName(review.Reviewer),
        review.ScheduledAt,
        review.Comment,
        review.CompletedAt);
}
