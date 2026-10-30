using HrFlow.Api.Models;

namespace HrFlow.Api.Services;

public static class Mapping
{
    public static string FullName(Employee employee) => $"{employee.FirstName} {employee.LastName}";

    public static Dtos.EmployeeDto ToDto(Employee employee) => new(
        employee.Id,
        employee.InternalCode,
        employee.FirstName,
        employee.LastName,
        employee.ProfessionalEmail,
        employee.Phone,
        employee.HireDate,
        employee.ContractType,
        employee.Status,
        employee.DepartmentId,
        employee.Department?.Name ?? string.Empty,
        employee.PositionId,
        employee.Position?.Title ?? string.Empty,
        employee.ManagerId,
        employee.Manager is null ? null : FullName(employee.Manager),
        employee.UserId,
        employee.ArchivedAt);

    public static Dtos.ContractDto ToDto(EmploymentContract contract, DateOnly today)
    {
        var soon = contract.Status == ContractStatus.Active
            && contract.EndDate is DateOnly end
            && end >= today
            && end <= today.AddDays(30);
        return new Dtos.ContractDto(
            contract.Id,
            contract.EmployeeId,
            contract.Employee is null ? string.Empty : FullName(contract.Employee),
            contract.Type,
            contract.Status,
            contract.StartDate,
            contract.EndDate,
            contract.Notes,
            soon);
    }

    public static Dtos.LeaveRequestDto ToDto(LeaveRequest request) => new(
        request.Id,
        request.EmployeeId,
        request.Employee is null ? string.Empty : FullName(request.Employee),
        request.LeaveTypeId,
        request.LeaveType?.Name ?? string.Empty,
        request.StartDate,
        request.EndDate,
        request.BusinessDays,
        request.Reason,
        request.Status,
        request.DecisionComment,
        request.DecidedAt,
        request.CreatedAt,
        request.IsHalfDay,
        request.HalfDayPart,
        request.UnpaidDays);

    public static Dtos.DocumentDto ToDto(EmployeeDocument document) => new(
        document.Id,
        document.EmployeeId,
        document.Employee is null ? string.Empty : FullName(document.Employee),
        document.ContractId,
        document.OriginalFileName,
        document.ContentType,
        document.UploadedAt);
}
