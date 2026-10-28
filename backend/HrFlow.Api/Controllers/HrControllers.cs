using HrFlow.Api.Dtos;
using HrFlow.Api.Security;
using HrFlow.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organization")]
public class OrganizationController(IOrganizationService organization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrganizationDto>> Get(CancellationToken ct) => Ok(await organization.GetAsync(ct));
}

[ApiController]
[Authorize]
[Route("api/departments")]
public class DepartmentsController(IOrganizationService organization) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(UpsertDepartmentRequest request, CancellationToken ct) =>
        Ok(await organization.CreateDepartmentAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, UpsertDepartmentRequest request, CancellationToken ct) =>
        Ok(await organization.UpdateDepartmentAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/positions")]
public class PositionsController(IOrganizationService organization) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PositionDto>> Create(UpsertPositionRequest request, CancellationToken ct) =>
        Ok(await organization.CreatePositionAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PositionDto>> Update(Guid id, UpsertPositionRequest request, CancellationToken ct) =>
        Ok(await organization.UpdatePositionAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/employees")]
public class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> List([FromQuery] EmployeeListQuery query, CancellationToken ct) =>
        Ok(await employees.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await employees.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(UpsertEmployeeRequest request, CancellationToken ct)
    {
        var created = await employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpsertEmployeeRequest request, CancellationToken ct) =>
        Ok(await employees.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await employees.ArchiveAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/contracts")]
public class ContractsController(IContractService contracts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContractDto>>> List([FromQuery] ContractListQuery query, CancellationToken ct) =>
        Ok(await contracts.ListAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<ContractDto>> Create(UpsertContractRequest request, CancellationToken ct) =>
        Ok(await contracts.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractDto>> Update(Guid id, UpsertContractRequest request, CancellationToken ct) =>
        Ok(await contracts.UpdateAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController(IDocumentService documents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List([FromQuery] Guid? employeeId, CancellationToken ct) =>
        Ok(await documents.ListAsync(employeeId, ct));

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadDocumentRequest request, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
            throw new BusinessRuleException("Le fichier doit peser entre 1 octet et 5 Mo.");
        await using var stream = file.OpenReadStream();
        return Ok(await documents.UploadAsync(request, stream, file.FileName, ct));
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await documents.OpenAsync(id, ct);
        return File(file.Content, file.ContentType);
    }
}

[ApiController]
[Authorize]
[Route("api/leave")]
public class LeaveController(ILeaveService leave) : ControllerBase
{
    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> Types(CancellationToken ct) => Ok(await leave.TypesAsync(ct));

    [HttpGet("balances")]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceDto>>> Balances([FromQuery] Guid? employeeId, CancellationToken ct) =>
        Ok(await leave.BalancesAsync(employeeId, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> List([FromQuery] LeaveListQuery query, CancellationToken ct) =>
        Ok(await leave.ListAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<LeaveRequestDto>> Create(CreateLeaveRequest request, CancellationToken ct) =>
        Ok(await leave.SubmitAsync(request, ct));

    [HttpPost("{id:guid}/decision")]
    public async Task<ActionResult<LeaveRequestDto>> Decide(Guid id, LeaveDecisionRequest request, CancellationToken ct) =>
        Ok(await leave.DecideAsync(id, request, ct));
}

[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController(IAttendanceService attendance) : ControllerBase
{
    [HttpPost("check-in")]
    public async Task<ActionResult<AttendanceDto>> CheckIn(CancellationToken ct) => Ok(await attendance.CheckInAsync(ct));

    [HttpPost("check-out")]
    public async Task<ActionResult<AttendanceDto>> CheckOut(CancellationToken ct) => Ok(await attendance.CheckOutAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> List([FromQuery] AttendanceListQuery query, CancellationToken ct) =>
        Ok(await attendance.ListAsync(query, ct));

    [HttpPut]
    public async Task<ActionResult<AttendanceDto>> Correct(CorrectAttendanceRequest request, CancellationToken ct) =>
        Ok(await attendance.CorrectAsync(request, ct));

    [HttpGet("report")]
    public async Task<ActionResult<AttendanceReportDto>> Report([FromQuery] AttendanceListQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = query.To ?? today;
        return Ok(await attendance.ReportAsync(from, to, query.DepartmentId, ct));
    }
}

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DashboardQuery query, CancellationToken ct) =>
        Ok(await dashboard.GetAsync(query, ct));
}
