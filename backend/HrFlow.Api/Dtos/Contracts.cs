using System.ComponentModel.DataAnnotations;
using HrFlow.Api.Models;

namespace HrFlow.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public class PageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record UserProfileDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    Guid? EmployeeId);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserProfileDto User);

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public record RegisterResponse(string Status);

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }
}

public class SetActiveRequest
{
    public bool IsActive { get; set; }
}

public class SetRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}

public record DepartmentDto(Guid Id, string Name, string? Description, int EmployeeCount);

public class UpsertDepartmentRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public record PositionDto(Guid Id, string Title, string? Description, Guid DepartmentId, string DepartmentName);

public class UpsertPositionRequest
{
    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }
}

public record OrganizationDto(
    IReadOnlyList<DepartmentDto> Departments,
    IReadOnlyList<PositionDto> Positions,
    IReadOnlyList<EmployeeSummaryDto> Employees);

public record EmployeeSummaryDto(Guid Id, string FullName, string InternalCode, Guid? ManagerId, Guid DepartmentId, string PositionTitle);

public record EmployeeDto(
    Guid Id,
    string InternalCode,
    string FirstName,
    string LastName,
    string ProfessionalEmail,
    string? Phone,
    DateOnly HireDate,
    string ContractType,
    EmployeeStatus Status,
    Guid DepartmentId,
    string DepartmentName,
    Guid PositionId,
    string PositionTitle,
    Guid? ManagerId,
    string? ManagerName,
    string? UserId,
    DateTimeOffset? ArchivedAt);

public record EmploymentEventDto(Guid Id, DateTimeOffset OccurredAt, string Kind, string Description);

public record EmployeeDetailDto(
    EmployeeDto Profile,
    IReadOnlyList<EmploymentEventDto> History,
    IReadOnlyList<ContractDto> Contracts,
    IReadOnlyList<DocumentDto> Documents,
    IReadOnlyList<LeaveBalanceDto> Balances,
    IReadOnlyList<EmployeeSummaryDto> DirectReports);

public class EmployeeListQuery : PageQuery
{
    public string? Search { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IncludeArchived { get; set; }
}

public class UpsertEmployeeRequest
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string ProfessionalEmail { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [Required]
    public DateOnly HireDate { get; set; }

    [Required, MaxLength(40)]
    public string ContractType { get; set; } = "CDI";

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public Guid? ManagerId { get; set; }
}

public record ContractDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string Type,
    ContractStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Notes,
    bool ExpiringSoon);

public class ContractListQuery
{
    public Guid? EmployeeId { get; set; }
    public int? ExpiringWithinDays { get; set; }
}

public class UpsertContractRequest
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(40)]
    public string Type { get; set; } = "CDI";

    [Required]
    public ContractStatus Status { get; set; } = ContractStatus.Active;

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public record DocumentDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid? ContractId,
    string FileName,
    string ContentType,
    DateTimeOffset UploadedAt);

public class UploadDocumentRequest
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? ContractId { get; set; }
}

public record LeaveTypeDto(Guid Id, string Name, int AnnualAllowance);

public record LeaveBalanceDto(Guid Id, Guid LeaveTypeId, string LeaveTypeName, int Year, int AllocatedDays, int UsedDays, int PendingDays, int RemainingDays);

public record LeaveRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    int BusinessDays,
    string? Reason,
    LeaveStatus Status,
    string? DecisionComment,
    DateTimeOffset? DecidedAt,
    DateTimeOffset CreatedAt);

public class LeaveListQuery
{
    public LeaveStatus? Status { get; set; }
    public Guid? EmployeeId { get; set; }
}

public class CreateLeaveRequest
{
    public Guid? EmployeeId { get; set; }

    [Required]
    public Guid LeaveTypeId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class LeaveDecisionRequest
{
    [Required]
    public LeaveStatus Status { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}

public record AttendanceDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid DepartmentId,
    string DepartmentName,
    DateOnly Date,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    AttendanceStatus Status,
    string? Note);

public class AttendanceListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public class CorrectAttendanceRequest
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public AttendanceStatus? Status { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public record AttendanceReportRowDto(Guid DepartmentId, string DepartmentName, int Present, int Late, int Absent);

public record AttendanceReportDto(int Present, int Late, int Absent, IReadOnlyList<AttendanceReportRowDto> ByDepartment);

public class DashboardQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? DepartmentId { get; set; }
}

public record DepartmentCountDto(Guid DepartmentId, string Name, int Count);

public record OfferCandidateCountDto(Guid OfferId, string Title, int CandidateCount);

public record DashboardDto(
    int EmployeeCount,
    int PendingLeaveCount,
    int ExpiringContractCount,
    IReadOnlyList<DepartmentCountDto> ByDepartment,
    IReadOnlyList<LeaveRequestDto> PendingLeaves,
    IReadOnlyList<ContractDto> ExpiringContracts,
    AttendanceReportDto Attendance,
    IReadOnlyList<OfferCandidateCountDto> ApplicationsPerOffer);

public record JobOfferDto(
    Guid Id,
    string Title,
    string Description,
    string Requirements,
    Guid? PositionId,
    string? PositionTitle,
    JobOfferStatus Status,
    DateTimeOffset CreatedAt,
    int CandidateCount);

public class UpsertJobOfferRequest
{
    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Requirements { get; set; } = string.Empty;

    public Guid? PositionId { get; set; }

    [Required]
    public JobOfferStatus Status { get; set; } = JobOfferStatus.Draft;
}

public record CandidateDto(
    Guid Id,
    Guid JobOfferId,
    string FirstName,
    string LastName,
    string Email,
    string? CvFileName,
    CandidateStatus Status,
    DateTimeOffset AppliedAt);

public class CreateCandidateRequest
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}

public class SetCandidateStatusRequest
{
    [Required]
    public CandidateStatus Status { get; set; }
}

public record GoalDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string Title,
    string? Description,
    DateOnly TargetDate,
    int Progress,
    GoalStatus Status);

public class UpsertGoalRequest
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateOnly TargetDate { get; set; }

    [Range(0, 100)]
    public int Progress { get; set; }

    [Required]
    public GoalStatus Status { get; set; } = GoalStatus.Open;
}

public record ReviewDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid ReviewerEmployeeId,
    string ReviewerName,
    DateTimeOffset ScheduledAt,
    string? Comment,
    DateTimeOffset? CompletedAt);

public class CreateReviewRequest
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ReviewerEmployeeId { get; set; }

    [Required]
    public DateTimeOffset ScheduledAt { get; set; }
}

public class CompleteReviewRequest
{
    [Required, MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;
}

public record NotificationDto(Guid Id, string Title, string Body, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public record AuditDto(long Id, DateTimeOffset OccurredAt, string ActorUserId, string Action, string EntityName, string EntityId, string? Details);

public class JwtOptions
{
    public string Issuer { get; set; } = "HrFlow";
    public string Audience { get; set; } = "HrFlow.Web";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 480;
}

public class AttendanceOptions
{
    public string WorkdayStart { get; set; } = "09:00";

    public TimeOnly StartTime => TimeOnly.Parse(WorkdayStart);
}
