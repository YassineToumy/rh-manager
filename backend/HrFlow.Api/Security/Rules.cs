using HrFlow.Api.Models;

namespace HrFlow.Api.Security;

public static class HrAccess
{
    public static bool CanManageOrganization(IReadOnlySet<string> roles) =>
        roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Rh);

    public static bool CanManageEmployees(IReadOnlySet<string> roles) =>
        roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Rh);

    public static bool CanManageUsers(IReadOnlySet<string> roles) =>
        roles.Contains(AppRoles.Admin);

    public static bool CanManageRecruitment(IReadOnlySet<string> roles) =>
        roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Rh);

    public static bool CanApproveLeave(IReadOnlySet<string> roles, bool isManagerOfEmployee) =>
        roles.Contains(AppRoles.Admin)
        || roles.Contains(AppRoles.Rh)
        || (roles.Contains(AppRoles.Manager) && isManagerOfEmployee);

    public static bool CanCorrectAttendance(IReadOnlySet<string> roles, bool isManagerOfEmployee) =>
        CanApproveLeave(roles, isManagerOfEmployee);

    public static bool CanReviewPerformance(IReadOnlySet<string> roles, bool isManagerOfEmployee) =>
        CanApproveLeave(roles, isManagerOfEmployee);

    public static bool CanViewEmployee(
        IReadOnlySet<string> roles,
        Guid targetEmployeeId,
        Guid? targetManagerId,
        Guid? callerEmployeeId)
    {
        if (roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Rh))
            return true;
        if (callerEmployeeId is not Guid me)
            return false;
        if (targetEmployeeId == me)
            return true;
        return roles.Contains(AppRoles.Manager) && targetManagerId == me;
    }
}

public static class LeaveRules
{
    public static bool IsDateRangeValid(DateOnly start, DateOnly end) => end >= start;

    public static bool IsSameCalendarYear(DateOnly start, DateOnly end) => start.Year == end.Year;

    public static int CountBusinessDays(DateOnly start, DateOnly end)
    {
        if (end < start)
            return 0;

        var count = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }

        return count;
    }

    public static bool RangesOverlap(DateOnly aStart, DateOnly aEnd, DateOnly bStart, DateOnly bEnd) =>
        aStart <= bEnd && bStart <= aEnd;

    public static bool HasEnoughBalance(int allocated, int used, int pending, int requested) =>
        allocated - used - pending >= requested;
}

public static class AttendanceRules
{
    public static AttendanceStatus ResolveStatus(TimeOnly? checkIn, TimeOnly workdayStart)
    {
        if (checkIn is null)
            return AttendanceStatus.Absent;
        return checkIn.Value > workdayStart ? AttendanceStatus.Late : AttendanceStatus.Present;
    }

    public static bool IsCheckoutValid(TimeOnly? checkIn, TimeOnly? checkOut) =>
        checkIn is null || checkOut is null || checkOut.Value >= checkIn.Value;
}

public static class AccessGuard
{
    public static void Require(bool allowed)
    {
        if (!allowed)
            throw new ForbiddenAccessException();
    }
}
