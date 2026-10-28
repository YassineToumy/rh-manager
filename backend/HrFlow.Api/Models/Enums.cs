namespace HrFlow.Api.Models;

public enum EmployeeStatus
{
    Active,
    Archived
}

public enum ContractStatus
{
    Draft,
    Active,
    Expired,
    Terminated
}

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected
}

public enum AttendanceStatus
{
    Present,
    Late,
    Absent
}

public enum JobOfferStatus
{
    Draft,
    Published,
    Closed
}

public enum CandidateStatus
{
    Received,
    InReview,
    Interview,
    Accepted,
    Rejected
}

public enum GoalStatus
{
    Open,
    InProgress,
    Done,
    Cancelled
}
