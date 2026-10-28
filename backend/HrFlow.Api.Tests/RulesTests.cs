using HrFlow.Api.Models;
using HrFlow.Api.Security;

namespace HrFlow.Api.Tests;

public class LeaveRulesTests
{
    [Fact]
    public void End_date_must_not_precede_start()
    {
        var start = new DateOnly(2026, 11, 4);
        var end = new DateOnly(2026, 11, 2);
        Assert.False(LeaveRules.IsDateRangeValid(start, end));
        Assert.True(LeaveRules.IsDateRangeValid(end, start));
    }

    [Fact]
    public void Business_days_skip_the_weekend()
    {
        var friday = new DateOnly(2026, 11, 6);
        var monday = new DateOnly(2026, 11, 9);
        Assert.Equal(2, LeaveRules.CountBusinessDays(friday, monday));
        Assert.Equal(0, LeaveRules.CountBusinessDays(monday, friday));
    }

    [Fact]
    public void Overlap_detects_a_shared_day()
    {
        Assert.True(LeaveRules.RangesOverlap(
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 6)));
        Assert.False(LeaveRules.RangesOverlap(
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3),
            new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 5)));
    }

    [Fact]
    public void Balance_keeps_pending_days_reserved()
    {
        Assert.True(LeaveRules.HasEnoughBalance(allocated: 10, used: 4, pending: 3, requested: 3));
        Assert.False(LeaveRules.HasEnoughBalance(allocated: 10, used: 4, pending: 3, requested: 4));
    }
}

public class AttendanceRulesTests
{
    private static readonly TimeOnly Start = new(9, 0);

    [Theory]
    [InlineData(8, 50, AttendanceStatus.Present)]
    [InlineData(9, 0, AttendanceStatus.Present)]
    [InlineData(9, 1, AttendanceStatus.Late)]
    public void Check_in_after_the_workday_start_is_late(int hour, int minute, AttendanceStatus expected)
    {
        Assert.Equal(expected, AttendanceRules.ResolveStatus(new TimeOnly(hour, minute), Start));
    }

    [Fact]
    public void Missing_check_in_is_an_absence()
    {
        Assert.Equal(AttendanceStatus.Absent, AttendanceRules.ResolveStatus(null, Start));
    }

    [Fact]
    public void Checkout_cannot_precede_checkin()
    {
        Assert.False(AttendanceRules.IsCheckoutValid(new TimeOnly(9, 30), new TimeOnly(9, 0)));
        Assert.True(AttendanceRules.IsCheckoutValid(new TimeOnly(9, 0), new TimeOnly(17, 30)));
    }
}

public class HrAccessTests
{
    [Fact]
    public void Employee_cannot_manage_accounts()
    {
        var roles = new HashSet<string> { AppRoles.Employee };
        Assert.False(HrAccess.CanManageUsers(roles));
        Assert.False(HrAccess.CanManageEmployees(roles));
    }

    [Fact]
    public void Manager_approves_only_their_team()
    {
        var roles = new HashSet<string> { AppRoles.Manager };
        Assert.True(HrAccess.CanApproveLeave(roles, isManagerOfEmployee: true));
        Assert.False(HrAccess.CanApproveLeave(roles, isManagerOfEmployee: false));
    }

    [Fact]
    public void Rh_can_approve_without_being_the_manager()
    {
        var roles = new HashSet<string> { AppRoles.Rh };
        Assert.True(HrAccess.CanApproveLeave(roles, isManagerOfEmployee: false));
        Assert.True(HrAccess.CanManageRecruitment(roles));
    }

    [Fact]
    public void Employee_sees_self_and_manager_sees_direct_reports()
    {
        var self = Guid.NewGuid();
        var report = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var employeeRoles = new HashSet<string> { AppRoles.Employee };
        var managerRoles = new HashSet<string> { AppRoles.Manager };

        Assert.True(HrAccess.CanViewEmployee(employeeRoles, self, null, self));
        Assert.False(HrAccess.CanViewEmployee(employeeRoles, stranger, self, self));
        Assert.True(HrAccess.CanViewEmployee(managerRoles, report, self, self));
        Assert.False(HrAccess.CanViewEmployee(managerRoles, stranger, Guid.NewGuid(), self));
    }
}
