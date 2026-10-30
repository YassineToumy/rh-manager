using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Services;

public interface IUserAdminService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(AccountQuery query, CancellationToken ct);
    Task<AccountDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<AccountDto> UpdateAsync(string userId, UpdateAccountRequest request, CancellationToken ct);
    Task ApproveAsync(string userId, bool isApproved, CancellationToken ct);
    Task DeleteAsync(string userId, CancellationToken ct);
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
    public async Task<IReadOnlyList<AccountDto>> ListAsync(AccountQuery query, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        var accounts = users.Users.Where(user => user.Role != AppRoles.Admin);

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            EnsureStaffRole(query.Role);
            accounts = accounts.Where(user => user.Role == query.Role);
        }

        if (query.Status == "pending")
            accounts = accounts.Where(user => !user.IsApproved);
        else if (query.Status == "approved")
            accounts = accounts.Where(user => user.IsApproved);

        if (query.CreatedFrom is DateOnly from)
            accounts = accounts.Where(user => user.CreatedAt >= new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (query.CreatedTo is DateOnly to)
            accounts = accounts.Where(user => user.CreatedAt < new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var rows = await accounts.OrderByDescending(user => user.CreatedAt).ToListAsync(ct);
        return rows.Select(ToAccount).ToList();
    }

    public async Task<AccountDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        EnsureStaffRole(request.Role);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            IsApproved = true,
            LockoutEnabled = true,
            EmailConfirmed = true,
            Role = request.Role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException(string.Join(" ", created.Errors.Select(error => error.Description)));

        if (request.EmployeeId is Guid employeeId)
            await LinkEmployeeAsync(user.Id, employeeId, ct);

        audit.Write("user.create", "User", user.Id, request.Role);
        await db.SaveChangesAsync(ct);
        return ToAccount(user);
    }

    public async Task<AccountDto> UpdateAsync(string userId, UpdateAccountRequest request, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        EnsureStaffRole(request.Role);
        var user = await RequireStaffAsync(userId);
        var email = request.Email.Trim().ToLowerInvariant();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        user.UserName = email;
        user.Role = request.Role;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));

        audit.Write("user.update", "User", user.Id, request.Role);
        await db.SaveChangesAsync(ct);
        return ToAccount(user);
    }

    public async Task ApproveAsync(string userId, bool isApproved, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        var user = await RequireStaffAsync(userId);
        user.IsApproved = isApproved;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            throw new BusinessRuleException(string.Join(" ", updated.Errors.Select(error => error.Description)));

        audit.Write(isApproved ? "user.approve" : "user.unapprove", "User", user.Id, user.Email);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        if (current.UserId == userId)
            throw new BusinessRuleException("Vous ne pouvez pas supprimer votre propre compte.");

        var user = await RequireStaffAsync(userId);
        var linked = await db.Employees.Where(employee => employee.UserId == user.Id).ToListAsync(ct);
        foreach (var employee in linked)
            employee.UserId = null;

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
            throw new BusinessRuleException(string.Join(" ", deleted.Errors.Select(error => error.Description)));

        audit.Write("user.delete", "User", user.Id, user.Email);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(string userId, bool isActive, CancellationToken ct)
    {
        AccessGuard.Require(HrAccess.CanManageUsers(current.Roles));
        if (current.UserId == userId)
            throw new BusinessRuleException("Vous ne pouvez pas désactiver votre propre compte.");

        var user = await RequireStaffAsync(userId);
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
        EnsureStaffRole(role);
        var user = await RequireStaffAsync(userId);
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

    private async Task<ApplicationUser> RequireStaffAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Compte introuvable.");
        if (user.Role == AppRoles.Admin)
            throw new BusinessRuleException("Un administrateur se gère depuis le terminal.");
        return user;
    }

    private static AccountDto ToAccount(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.Role, user.IsActive, user.IsApproved, user.CreatedAt);

    private static void EnsureStaffRole(string role)
    {
        if (!AppRoles.SelfService.Contains(role))
            throw new BusinessRuleException("Rôle inconnu. Choisissez employé, responsable RH ou manager.");
    }
}
