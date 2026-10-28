using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserProfileDto>> ListAsync(CancellationToken ct);
    Task<UserProfileDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task SetActiveAsync(string userId, bool isActive, CancellationToken ct);
    Task SetRoleAsync(string userId, string role, CancellationToken ct);
    Task<IReadOnlyList<AuditDto>> LatestAuditAsync(CancellationToken ct);
}

public sealed class UserAdminService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    ICurrentUser current,
    IAuditWriter audit) : IUserAdminService
{
    public async Task<IReadOnlyList<UserProfileDto>> ListAsync(CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        var accounts = await users.Users.OrderBy(user => user.LastName).ThenBy(user => user.FirstName).ToListAsync(ct);
        var result = new List<UserProfileDto>();
        foreach (var account in accounts)
            result.Add(await ToProfileAsync(account, ct));
        return result;
    }

    public async Task<UserProfileDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        EnsureRole(request.Role);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            LockoutEnabled = true,
            EmailConfirmed = true,
            Role = request.Role
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException(string.Join(" ", created.Errors.Select(error => error.Description)));

        if (request.EmployeeId is Guid employeeId)
            await LinkEmployeeAsync(user.Id, employeeId, ct);

        audit.Write("user.create", "User", user.Id, request.Role);
        await db.SaveChangesAsync(ct);
        return await ToProfileAsync(user, ct);
    }

    public async Task SetActiveAsync(string userId, bool isActive, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        if (current.UserId == userId)
            throw new BusinessRuleException("Vous ne pouvez pas désactiver votre propre compte.");

        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
        user.IsActive = isActive;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));

        audit.Write(isActive ? "user.activate" : "user.deactivate", "User", user.Id, user.Email);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetRoleAsync(string userId, string role, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        EnsureRole(role);
        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
        user.Role = role;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));

        audit.Write("user.role", "User", user.Id, role);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditDto>> LatestAuditAsync(CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        return await db.AuditEntries.AsNoTracking()
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(50)
            .Select(entry => new AuditDto(entry.Id, entry.OccurredAt, entry.ActorUserId, entry.Action, entry.EntityName, entry.EntityId, entry.Details))
            .ToListAsync(ct);
    }

    private async Task LinkEmployeeAsync(string userId, Guid employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, ct)
            ?? throw new NotFoundException("Employé introuvable.");
        if (employee.UserId is not null && employee.UserId != userId)
            throw new BusinessRuleException("Cet employé est déjà lié à un autre compte.");

        var other = await db.Employees.FirstOrDefaultAsync(item => item.UserId == userId && item.Id != employeeId, ct);
        if (other is not null)
            other.UserId = null;
        employee.UserId = userId;
    }

    private async Task<UserProfileDto> ToProfileAsync(ApplicationUser user, CancellationToken ct)
    {
        var employeeId = await db.Employees.AsNoTracking()
            .Where(employee => employee.UserId == user.Id)
            .Select(employee => (Guid?)employee.Id)
            .FirstOrDefaultAsync(ct);
        return new UserProfileDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.IsActive, [user.Role], employeeId);
    }

    private static void EnsureRole(string role)
    {
        if (!AppRoles.All.Contains(role))
            throw new BusinessRuleException("Rôle inconnu.");
    }
}
