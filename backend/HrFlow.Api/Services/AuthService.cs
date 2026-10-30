using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using QRCoder;
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
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<AccountSettingsDto> SettingsAsync(CancellationToken ct);
    Task<UserProfileDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct);
    Task SavePhotoAsync(Stream content, string contentType, long length, CancellationToken ct);
    Task<(byte[] Bytes, string ContentType)?> PhotoAsync(CancellationToken ct);
    Task RemovePhotoAsync(CancellationToken ct);
    Task<TwoFactorSetupDto> StartTwoFactorAsync(CancellationToken ct);
    Task EnableTwoFactorAsync(string code, CancellationToken ct);
    Task DisableTwoFactorAsync(string password, string code, CancellationToken ct);
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

        if (user.TwoFactorEnabled)
        {
            var profileForChallenge = await BuildProfileAsync(user, ct);
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                return new LoginResponse(string.Empty, DateTime.UtcNow, profileForChallenge, true);

            var validCode = await users.VerifyTwoFactorTokenAsync(
                user,
                users.Options.Tokens.AuthenticatorTokenProvider,
                request.TwoFactorCode.Trim());
            if (!validCode)
            {
                await users.AccessFailedAsync(user);
                throw new BusinessRuleException("Code d'authentification invalide.");
            }
        }

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
            Role = request.Role,
            CreatedAt = DateTimeOffset.UtcNow
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

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new ForbiddenAccessException();
        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
            throw new BusinessRuleException(string.Join(" ", changed.Errors.Select(error => error.Description)));
    }

    public async Task<UserProfileDto> MeAsync(CancellationToken ct)
    {
        var userId = current.UserId ?? throw new ForbiddenAccessException();
        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
        return await BuildProfileAsync(user, ct);
    }

    public async Task<AccountSettingsDto> SettingsAsync(CancellationToken ct)
    {
        var user = await RequireUserAsync();
        return new AccountSettingsDto(user.FirstName, user.LastName, user.Email ?? string.Empty, user.TwoFactorEnabled, user.Photo is { Length: > 0 });
    }

    public async Task<UserProfileDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));

        var linked = await db.Employees.Where(employee => employee.UserId == user.Id).ToListAsync(ct);
        foreach (var employee in linked)
        {
            employee.FirstName = user.FirstName;
            employee.LastName = user.LastName;
        }
        await db.SaveChangesAsync(ct);
        return await BuildProfileAsync(user, ct);
    }

    public async Task SavePhotoAsync(Stream content, string contentType, long length, CancellationToken ct)
    {
        if (length <= 0 || length > 1_500_000)
            throw new BusinessRuleException("La photo doit faire moins de 1,5 Mo.");
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new BusinessRuleException("Utilisez une image JPEG, PNG ou WebP.");

        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        if (!LooksLikeImage(bytes, contentType))
            throw new BusinessRuleException("Le fichier n'est pas une image valide.");

        var user = await RequireUserAsync();
        user.Photo = bytes;
        user.PhotoContentType = contentType;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));
    }

    public async Task<(byte[] Bytes, string ContentType)?> PhotoAsync(CancellationToken ct)
    {
        var user = await RequireUserAsync();
        if (user.Photo is not { Length: > 0 } || string.IsNullOrWhiteSpace(user.PhotoContentType))
            return null;
        return (user.Photo, user.PhotoContentType);
    }

    public async Task RemovePhotoAsync(CancellationToken ct)
    {
        var user = await RequireUserAsync();
        user.Photo = null;
        user.PhotoContentType = null;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));
    }

    public async Task<TwoFactorSetupDto> StartTwoFactorAsync(CancellationToken ct)
    {
        var user = await RequireUserAsync();
        if (await users.GetTwoFactorEnabledAsync(user))
            throw new BusinessRuleException("La double authentification est déjà active.");

        await users.ResetAuthenticatorKeyAsync(user);
        var key = await users.GetAuthenticatorKeyAsync(user)
            ?? throw new BusinessRuleException("Impossible de préparer l'authentificateur.");
        var email = user.Email ?? user.UserName ?? "compte";
        var uri = $"otpauth://totp/HRFlow:{Uri.EscapeDataString(email)}?secret={key}&issuer=HRFlow&digits=6";
        return new TwoFactorSetupDto(FormatKey(key), QrDataUrl(uri));
    }

    public async Task EnableTwoFactorAsync(string code, CancellationToken ct)
    {
        var user = await RequireUserAsync();
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code.Trim());
        if (!valid)
            throw new BusinessRuleException("Code d'authentification invalide.");
        await users.SetTwoFactorEnabledAsync(user, true);
    }

    public async Task DisableTwoFactorAsync(string password, string code, CancellationToken ct)
    {
        var user = await RequireUserAsync();
        if (!await users.CheckPasswordAsync(user, password))
            throw new BusinessRuleException("Mot de passe incorrect.");
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code.Trim());
        if (!valid)
            throw new BusinessRuleException("Code d'authentification invalide.");
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
    }

    private async Task<ApplicationUser> RequireUserAsync()
    {
        var userId = current.UserId ?? throw new ForbiddenAccessException();
        return await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
    }

    private static string FormatKey(string key)
    {
        var parts = new List<string>();
        for (var index = 0; index < key.Length; index += 4)
            parts.Add(key.Substring(index, Math.Min(4, key.Length - index)));
        return string.Join(' ', parts);
    }

    private static string QrDataUrl(string uri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(5);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    private static bool LooksLikeImage(byte[] bytes, string contentType)
    {
        if (bytes.Length < 12)
            return false;
        if (contentType == "image/jpeg")
            return bytes[0] == 0xFF && bytes[1] == 0xD8;
        if (contentType == "image/png")
            return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        return bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
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
