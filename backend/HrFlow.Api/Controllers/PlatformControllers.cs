using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Security;
using HrFlow.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrFlow.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var connected = false;
        try
        {
            connected = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            connected = false;
        }

        return Ok(new { status = connected ? "ok" : "degraded", database = connected ? "up" : "down" });
    }
}

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        Ok(await auth.RegisterAsync(request, ct));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "Si le compte existe, un jeton a été écrit dans le journal de l'API." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct) => Ok(await auth.MeAsync(ct));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<ActionResult<AccountSettingsDto>> Settings(CancellationToken ct) => Ok(await auth.SettingsAsync(ct));

    [HttpPut("settings")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> UpdateSettings(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await auth.UpdateProfileAsync(request, ct));

    [HttpGet("photo")]
    [Authorize]
    public async Task<IActionResult> Photo(CancellationToken ct)
    {
        var photo = await auth.PhotoAsync(ct);
        return photo is null ? NotFound() : File(photo.Value.Bytes, photo.Value.ContentType);
    }

    [HttpPost("photo")]
    [Authorize]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> UploadPhoto(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest();
        await auth.SavePhotoAsync(file.OpenReadStream(), file.ContentType, file.Length, ct);
        return NoContent();
    }

    [HttpDelete("photo")]
    [Authorize]
    public async Task<IActionResult> DeletePhoto(CancellationToken ct)
    {
        await auth.RemovePhotoAsync(ct);
        return NoContent();
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<TwoFactorSetupDto>> StartTwoFactor(CancellationToken ct) =>
        Ok(await auth.StartTwoFactorAsync(ct));

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken ct)
    {
        await auth.EnableTwoFactorAsync(request.Code, ct);
        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await auth.DisableTwoFactorAsync(request.Password, request.Code, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/users")]
public class UsersController(IUserAdminService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> List([FromQuery] AccountQuery query, CancellationToken ct) =>
        Ok(await users.ListAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        Ok(await users.CreateAsync(request, ct));

    [HttpPut("{id}")]
    public async Task<ActionResult<AccountDto>> Update(string id, UpdateAccountRequest request, CancellationToken ct) =>
        Ok(await users.UpdateAsync(id, request, ct));

    [HttpPut("{id}/approval")]
    public async Task<IActionResult> Approval(string id, SetApprovalRequest request, CancellationToken ct)
    {
        await users.ApproveAsync(id, request.IsApproved, ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id}/activation")]
    public async Task<IActionResult> Activation(string id, SetActiveRequest request, CancellationToken ct)
    {
        await users.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> Role(string id, SetRoleRequest request, CancellationToken ct)
    {
        await users.SetRoleAsync(id, request.Role, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize(Roles = AppRoles.Employee)]
[Route("api/me/space")]
public class EmployeeSpaceController(IEmployeeSpaceService space) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmployeeSpaceDto>> Get(CancellationToken ct) => Ok(await space.GetAsync(ct));

    [HttpPost("leave")]
    public async Task<ActionResult<LeaveRequestDto>> Leave(CreateLeaveRequest request, CancellationToken ct) =>
        Ok(await space.RequestLeaveAsync(request, ct));

    [HttpPost("advances")]
    public async Task<ActionResult<SalaryAdvanceDto>> Advance(CreateAdvanceRequest request, CancellationToken ct) =>
        Ok(await space.RequestAdvanceAsync(request, ct));
}

[ApiController]
[Authorize(Roles = AppRoles.Manager)]
[Route("api/team")]
public class TeamFollowUpController(ITeamFollowUpService team) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamMemberDto>>> Reports(CancellationToken ct) =>
        Ok(await team.ReportsAsync(ct));

    [HttpGet("{employeeId:guid}")]
    public async Task<ActionResult<TeamFollowUpDto>> FollowUp(Guid employeeId, CancellationToken ct) =>
        Ok(await team.FollowUpAsync(employeeId, ct));

    [HttpPost("{employeeId:guid}/missions")]
    public async Task<ActionResult<MissionDto>> AddMission(Guid employeeId, CreateMissionRequest request, CancellationToken ct) =>
        Ok(await team.AddMissionAsync(employeeId, request, ct));

    [HttpPut("missions/{missionId:guid}")]
    public async Task<ActionResult<MissionDto>> UpdateMission(Guid missionId, UpdateMissionRequest request, CancellationToken ct) =>
        Ok(await team.UpdateMissionAsync(missionId, request, ct));

    [HttpPut("{employeeId:guid}/rating")]
    public async Task<ActionResult<WeeklyRatingDto>> Rating(Guid employeeId, SetWeeklyRatingRequest request, CancellationToken ct) =>
        Ok(await team.SetRatingAsync(employeeId, request, ct));
}

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/audit")]
public class AuditController(IUserAdminService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditDto>>> List(CancellationToken ct) => Ok(await users.LatestAuditAsync(ct));
}

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(CancellationToken ct) =>
        Ok(await notifications.ListMineAsync(ct));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }
}
