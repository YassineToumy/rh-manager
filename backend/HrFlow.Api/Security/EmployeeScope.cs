using HrFlow.Api.Models;

namespace HrFlow.Api.Security;

public static class EmployeeScope
{
    public static IQueryable<Employee> Visible(IQueryable<Employee> query, ICurrentUser user)
    {
        if (user.Roles.Contains(AppRoles.Admin) || user.Roles.Contains(AppRoles.Rh))
            return query;

        if (user.EmployeeId is not Guid me)
            return query.Where(employee => false);

        if (user.Roles.Contains(AppRoles.Manager))
            return query.Where(employee => employee.Id == me || employee.ManagerId == me);

        return query.Where(employee => employee.Id == me);
    }
}
