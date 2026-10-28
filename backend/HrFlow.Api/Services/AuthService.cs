using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HrFlow.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<UserProfileDto> MeAsync(CancellationToken ct);
}

public sealed class AuthService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    ICurrentUser current,
    IEmailSender email,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            throw new BusinessRuleException("Identifiants invalides.");

        if (await users.IsLockedOutAsync(user))
            throw new ForbiddenAccessException("Compte temporairement bloqué après plusieurs échecs.");

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            throw new BusinessRuleException("Identifiants invalides.");
        }

        if (!user.IsApproved)
            throw new ForbiddenAccessException(AppRoles.WaitingForApproval);

        if (!user.IsActive)
            throw new ForbiddenAccessException("Ce compte est désactivé.");

        await users.ResetAccessFailedCountAsync(user);
        var profile = await BuildProfileAsync(user, ct);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, profile.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", $"{user.FirstName} {user.LastName}".Trim())
        };
        if (profile.EmployeeId is Guid employeeId)
            claims.Add(new Claim("employee_id", employeeId.ToString()));
        claims.Add(new Claim("role", user.Role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, profile);
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!AppRoles.SelfService.Contains(request.Role))
            throw new BusinessRuleException("Ce rôle ne peut pas s'inscrire. Un administrateur est créé depuis le terminal.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            IsApproved = false,
            LockoutEnabled = true,
            Role = request.Role
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException(string.Join(" ", created.Errors.Select(error => error.Description)));

        return new RegisterResponse("pending");
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await email.SendAsync(
            user.Email,
            "Réinitialisation du mot de passe HRFlow",
            "Copiez ce jeton dans le formulaire de réinitialisation (démonstration locale, aucun e-mail réel n'est envoyé) : " + token,
            ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim())
            ?? throw new BusinessRuleException("Réinitialisation impossible.");
        var result = await users.ResetPasswordAsync(user, request.Token.Trim(), request.NewPassword);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }

    public async Task<UserProfileDto> MeAsync(CancellationToken ct)
    {
        var userId = current.UserId ?? throw new ForbiddenAccessException();
        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
        return await BuildProfileAsync(user, ct);
    }

    private async Task<UserProfileDto> BuildProfileAsync(ApplicationUser user, CancellationToken ct)
    {
        var employeeId = await db.Employees.AsNoTracking()
            .Where(employee => employee.UserId == user.Id)
            .Select(employee => (Guid?)employee.Id)
            .FirstOrDefaultAsync(ct);
        return new UserProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.IsActive,
            [user.Role],
            employeeId);
    }
}
