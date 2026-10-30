using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity;

namespace HrFlow.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Faux tant qu'un administrateur n'a pas accepté l'inscription.
    /// </summary>
    public bool IsApproved { get; set; } = true;

    /// <summary>Moment de la création du compte, utilisé par le filtre administrateur.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Un seul rôle par compte : Administrateur, ResponsableRH, Manager ou Employe.
    /// </summary>
    public string Role { get; set; } = AppRoles.Employee;

    public byte[]? Photo { get; set; }

    public string? PhotoContentType { get; set; }
}
