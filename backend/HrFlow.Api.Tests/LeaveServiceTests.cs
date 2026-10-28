using HrFlow.Api.Data;
using HrFlow.Api.Dtos;
using HrFlow.Api.Models;
using HrFlow.Api.Security;
using HrFlow.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrFlow.Api.Tests;

public class LeaveServiceTests
{
    [Fact]
    public async Task Submit_persists_a_pending_request()
    {
        await using var scope = await ScopeAsync();
        var (service, db, employee, type) = await SeedAsync(scope, allowance: 25);

        var created = await service.SubmitAsync(Request(employee.Id, type.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)), CancellationToken.None);

        Assert.Equal(LeaveStatus.Pending, created.Status);
        Assert.Equal(2, created.BusinessDays);
        Assert.Equal(1, await db.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task Submit_rejects_an_overlap()
    {
        await using var scope = await ScopeAsync();
        var (service, _, employee, type) = await SeedAsync(scope, allowance: 25);
        await service.SubmitAsync(Request(employee.Id, type.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4)), CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(Request(employee.Id, type.Id, new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 5)), CancellationToken.None));

        Assert.Contains("chevauche", error.Message);
    }

    [Fact]
    public async Task Submit_rejects_an_insufficient_balance()
    {
        await using var scope = await ScopeAsync();
        var (service, _, employee, type) = await SeedAsync(scope, allowance: 1);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(Request(employee.Id, type.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)), CancellationToken.None));

        Assert.Contains("solde", error.Message);
    }

    [Fact]
    public async Task Approval_consumes_the_balance()
    {
        await using var scope = await ScopeAsync();
        var (service, db, employee, type) = await SeedAsync(scope, allowance: 25);
        var created = await service.SubmitAsync(Request(employee.Id, type.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)), CancellationToken.None);
        var current = scope.ServiceProvider.GetRequiredService<TestUser>();
        current.UserId = "rh-user";
        current.EmployeeId = null;
        current.Roles = new HashSet<string> { AppRoles.Rh };

        var decided = await service.DecideAsync(created.Id, new LeaveDecisionRequest { Status = LeaveStatus.Approved, Comment = "OK" }, CancellationToken.None);

        Assert.Equal(LeaveStatus.Approved, decided.Status);
        var balance = await db.LeaveBalances.SingleAsync();
        Assert.Equal(2, balance.UsedDays);
    }

    private static CreateLeaveRequest Request(Guid employeeId, Guid typeId, DateOnly start, DateOnly end) => new()
    {
        EmployeeId = employeeId,
        LeaveTypeId = typeId,
        StartDate = start,
        EndDate = end,
        Reason = "Test"
    };

    private static async Task<AsyncServiceScope> ScopeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton<TestUser>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<TestUser>());
        services.AddSingleton<IEmailSender, NullEmailSender>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<LeaveService>();
        services.AddScoped<ILeaveService>(provider => provider.GetRequiredService<LeaveService>());
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        return scope;
    }

    private static async Task<(LeaveService Service, AppDbContext Db, Employee Employee, LeaveType Type)> SeedAsync(AsyncServiceScope scope, int allowance)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var department = new Department { Id = Guid.NewGuid(), Name = "Lab" };
        var position = new Position { Id = Guid.NewGuid(), Title = "Testeur", DepartmentId = department.Id };
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            InternalCode = "EMP-9000",
            FirstName = "Test",
            LastName = "User",
            ProfessionalEmail = $"{Guid.NewGuid():N}@hrflow.local",
            HireDate = new DateOnly(2026, 1, 1),
            ContractType = "CDI",
            DepartmentId = department.Id,
            PositionId = position.Id,
            Status = EmployeeStatus.Active
        };
        var type = new LeaveType { Id = Guid.NewGuid(), Name = "Congé payé", AnnualAllowance = allowance };
        db.AddRange(department, position, employee, type);
        db.LeaveBalances.Add(new LeaveBalance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            LeaveTypeId = type.Id,
            Year = 2026,
            AllocatedDays = allowance
        });
        await db.SaveChangesAsync();

        var current = scope.ServiceProvider.GetRequiredService<TestUser>();
        current.EmployeeId = employee.Id;
        current.Roles = new HashSet<string> { AppRoles.Employee };
        current.UserId = "employee-user";
        return (scope.ServiceProvider.GetRequiredService<LeaveService>(), db, employee, type);
    }

    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken ct) => Task.CompletedTask;
    }
}

public sealed class TestUser : ICurrentUser
{
    public string? UserId { get; set; }
    public Guid? EmployeeId { get; set; }
    public IReadOnlySet<string> Roles { get; set; } = new HashSet<string>();
    public bool IsAuthenticated { get; set; } = true;
}
