namespace HrFlow.Api.Models;

public class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class Position
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class Employee
{
    public Guid Id { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ProfessionalEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly HireDate { get; set; }
    public string ContractType { get; set; } = "CDI";
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid PositionId { get; set; }
    public Position? Position { get; set; }
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
    public ICollection<EmploymentEvent> History { get; set; } = new List<EmploymentEvent>();
    public ICollection<EmploymentContract> Contracts { get; set; } = new List<EmploymentContract>();
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<PerformanceGoal> Goals { get; set; } = new List<PerformanceGoal>();
    public ICollection<PerformanceReview> Reviews { get; set; } = new List<PerformanceReview>();
}

public class EmploymentEvent
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class EmploymentContract
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Type { get; set; } = "CDI";
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
}

public class EmployeeDocument
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid? ContractId { get; set; }
    public EmploymentContract? Contract { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTimeOffset UploadedAt { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
}

public class LeaveType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AnnualAllowance { get; set; }
}

public class LeaveBalance
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public int Year { get; set; }
    public int AllocatedDays { get; set; }
    public int UsedDays { get; set; }
}

public class LeaveRequest
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int BusinessDays { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? DecisionComment { get; set; }
    public string? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
    public string? CorrectedByUserId { get; set; }
}

public class JobOffer
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public Guid? PositionId { get; set; }
    public Position? Position { get; set; }
    public JobOfferStatus Status { get; set; } = JobOfferStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}

public class Candidate
{
    public Guid Id { get; set; }
    public Guid JobOfferId { get; set; }
    public JobOffer? JobOffer { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CvStoredFileName { get; set; }
    public string? CvOriginalFileName { get; set; }
    public CandidateStatus Status { get; set; } = CandidateStatus.Received;
    public DateTimeOffset AppliedAt { get; set; }
}

public class PerformanceGoal
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly TargetDate { get; set; }
    public int Progress { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Open;
}

public class PerformanceReview
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid ReviewerEmployeeId { get; set; }
    public Employee? Reviewer { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public ApplicationUser? Recipient { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string DedupKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool EmailDispatched { get; set; }
}

public class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ActorUserId { get; set; } = "system";
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
}
