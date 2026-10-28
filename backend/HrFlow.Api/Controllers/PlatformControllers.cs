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
}

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/users")]
public class UsersController(IUserAdminService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserProfileDto>>> List(CancellationToken ct) => Ok(await users.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        Ok(await users.CreateAsync(request, ct));

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
