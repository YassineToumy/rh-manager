using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HrFlow.Api.Security;

public interface ICurrentUser
{
    string? UserId { get; }
    Guid? EmployeeId { get; }
    IReadOnlySet<string> Roles { get; }
    bool IsAuthenticated { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public Guid? EmployeeId =>
        Guid.TryParse(Principal?.FindFirstValue("employee_id"), out var id) ? id : null;

    public IReadOnlySet<string> Roles =>
        Principal?.FindAll("role").Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>();
}
