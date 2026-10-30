using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Data;

public static class DbSeeder
{
    public const string AdminPassword = "Admin123!";
    public const string RhPassword = "Rh12345!";
    public const string ManagerPassword = "Manager123!";
    public const string EmployeePassword = "Employe123!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await users.Users.AnyAsync())
            return;

        var engineering = new Department { Id = Guid.NewGuid(), Name = "Ingénierie", Description = "Produit et développement." };
        var hr = new Department { Id = Guid.NewGuid(), Name = "Ressources humaines", Description = "Administration du personnel." };
        db.Departments.AddRange(engineering, hr);

        var managerPosition = new Position { Id = Guid.NewGuid(), Title = "Manager d'équipe", DepartmentId = engineering.Id };
        var developer = new Position { Id = Guid.NewGuid(), Title = "Développeur", DepartmentId = engineering.Id };
        var recruiter = new Position { Id = Guid.NewGuid(), Title = "Chargé de recrutement", DepartmentId = hr.Id };
        db.Positions.AddRange(managerPosition, developer, recruiter);

        var paid = new LeaveType { Id = Guid.NewGuid(), Name = "Congé payé", AnnualAllowance = 25 };
        var rtt = new LeaveType { Id = Guid.NewGuid(), Name = "RTT", AnnualAllowance = 10 };
        db.LeaveTypes.AddRange(paid, rtt);

        var claireUser = await CreateUserAsync(users, "manager@hrflow.local", ManagerPassword, "Claire", "Martin", AppRoles.Manager);
        var adamUser = await CreateUserAsync(users, "employe@hrflow.local", EmployeePassword, "Adam", "Bernard", AppRoles.Employee);
        var sofiaUser = await CreateUserAsync(users, "sofia@hrflow.local", EmployeePassword, "Sofia", "Nguyen", AppRoles.Employee);
        var inesUser = await CreateUserAsync(users, "rh@hrflow.local", RhPassword, "Inès", "Morel", AppRoles.Rh);
        await CreateUserAsync(users, "admin@hrflow.local", AdminPassword, "Alex", "Admin", AppRoles.Admin);

        var claire = EmployeeOf("EMP-0001", "Claire", "Martin", "claire.martin@hrflow.local", new DateOnly(2022, 3, 1), "CDI", engineering.Id, managerPosition.Id, null, claireUser.Id);
        var adam = EmployeeOf("EMP-0002", "Adam", "Bernard", "adam.bernard@hrflow.local", new DateOnly(2024, 1, 15), "CDI", engineering.Id, developer.Id, claire.Id, adamUser.Id);
        var sofia = EmployeeOf("EMP-0003", "Sofia", "Nguyen", "sofia.nguyen@hrflow.local", new DateOnly(2026, 1, 6), "CDD", engineering.Id, developer.Id, claire.Id, sofiaUser.Id);
        var ines = EmployeeOf("EMP-0004", "Inès", "Morel", "ines.morel@hrflow.local", new DateOnly(2023, 6, 1), "CDI", hr.Id, recruiter.Id, null, inesUser.Id);
        db.Employees.AddRange(claire, adam, sofia, ines);

        foreach (var employee in new[] { claire, adam, sofia, ines })
        {
            db.EmploymentEvents.Add(new EmploymentEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                OccurredAt = employee.HireDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Kind = "Embauche",
                Description = "Dossier ouvert lors de l'initialisation de démonstration."
            });
            foreach (var type in new[] { paid, rtt })
            {
                db.LeaveBalances.Add(new LeaveBalance
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    LeaveTypeId = type.Id,
                    Year = 2026,
                    AllocatedDays = type.AnnualAllowance
                });
            }
        }

        db.Contracts.AddRange(
            Contract(claire.Id, "CDI", new DateOnly(2022, 3, 1), null),
            Contract(adam.Id, "CDI", new DateOnly(2024, 1, 15), null),
            Contract(sofia.Id, "CDD", new DateOnly(2026, 1, 6), new DateOnly(2026, 11, 15)),
            Contract(ines.Id, "CDI", new DateOnly(2023, 6, 1), null));

        db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = adam.Id,
            LeaveTypeId = paid.Id,
            StartDate = new DateOnly(2026, 11, 2),
            EndDate = new DateOnly(2026, 11, 3),
            BusinessDays = 2,
            Reason = "Pont de début novembre.",
            Status = LeaveStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.AttendanceRecords.AddRange(
            new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = claire.Id, Date = today, CheckIn = new TimeOnly(8, 42), Status = AttendanceStatus.Present },
            new AttendanceRecord { Id = Guid.NewGuid(), EmployeeId = adam.Id, Date = today, CheckIn = new TimeOnly(9, 18), Status = AttendanceStatus.Late });

        var offer = new JobOffer
        {
            Id = Guid.NewGuid(),
            Title = "Développeur backend",
            Description = "Conception d'API métier pour la plateforme RH.",
            Requirements = "C#, ASP.NET Core, SQL.",
            PositionId = developer.Id,
            Status = JobOfferStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        db.JobOffers.Add(offer);
        db.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(),
            JobOfferId = offer.Id,
            FirstName = "Lina",
            LastName = "Costa",
            Email = "lina.costa@example.com",
            Status = CandidateStatus.InReview,
            AppliedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        db.PerformanceGoals.Add(new PerformanceGoal
        {
            Id = Guid.NewGuid(),
            EmployeeId = adam.Id,
            Title = "Livrer le module congés",
            Description = "Règles de solde et de chevauchement couvertes.",
            TargetDate = new DateOnly(2026, 12, 15),
            Progress = 40,
            Status = GoalStatus.InProgress
        });
        db.PerformanceReviews.Add(new PerformanceReview
        {
            Id = Guid.NewGuid(),
            EmployeeId = adam.Id,
            ReviewerEmployeeId = claire.Id,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3)
        });

        await db.SaveChangesAsync();
    }

    public static async Task EnsureEmployeeSpaceAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var employees = await db.Employees.Include(employee => employee.Manager).Where(employee => employee.UserId != null).ToListAsync();
        if (employees.Count == 0)
            return;

        var userIds = employees.Select(employee => employee.UserId!).ToArray();
        var employeeUserIds = await db.Users
            .Where(user => userIds.Contains(user.Id) && user.Role == AppRoles.Employee)
            .Select(user => user.Id)
            .ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        foreach (var employee in employees.Where(employee => employeeUserIds.Contains(employee.UserId!)))
        {
            var account = await db.Users.FirstOrDefaultAsync(user => user.Id == employee.UserId);
            if (account is not null && employee.HireDate < DateOnly.FromDateTime(account.CreatedAt.UtcDateTime).AddDays(-60))
                account.CreatedAt = new DateTimeOffset(employee.HireDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            var pay = await db.EmployeeCompensations.FirstOrDefaultAsync(item => item.EmployeeId == employee.Id);
            if (pay is null)
            {
                pay = new EmployeeCompensation
                {
                    EmployeeId = employee.Id,
                    PositionNumber = employee.InternalCode,
                    BankRib = "FR76 3000 6000 0112 3456 7890 189",
                    BaseSalary = 2650m,
                    Bonus = 180m
                };
                db.EmployeeCompensations.Add(pay);
            }
            if (employee.InternalCode == "EMP-0002" && string.IsNullOrEmpty(pay.ChipNumber))
            {
                pay.ChipNumber = "8933012345678901234";
                pay.ChipCredit = 20m;
            }

            if (!await db.SalaryDeposits.AnyAsync(item => item.EmployeeId == employee.Id && item.Year == today.Year && item.Month == today.Month))
            {
                db.SalaryDeposits.Add(new SalaryDeposit
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    Year = today.Year,
                    Month = today.Month,
                    IsPaid = false
                });
            }

            if (!await db.WeeklyRatings.AnyAsync(item => item.EmployeeId == employee.Id))
            {
                var manager = employee.Manager;
                db.WeeklyRatings.Add(new WeeklyRating
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    WeekStart = weekStart,
                    Score = 4,
                    Comment = "Semaine solide sur les livraisons prévues.",
                    ManagerName = manager is null ? "Manager" : $"{manager.FirstName} {manager.LastName}"
                });
            }

            if (!await db.WorkMissions.AnyAsync(item => item.EmployeeId == employee.Id))
            {
                var managerName = employee.Manager is null ? "Manager" : $"{employee.Manager.FirstName} {employee.Manager.LastName}";
                db.WorkMissions.AddRange(
                    new WorkMission
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employee.Id,
                        Title = "Livrer le correctif des présences",
                        Detail = "Fermer les pointages incomplets de la semaine.",
                        Status = "InProgress",
                        Comment = "Priorité de la semaine.",
                        WeekStart = weekStart,
                        ManagerName = managerName,
                        CreatedAt = DateTimeOffset.UtcNow
                    },
                    new WorkMission
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employee.Id,
                        Title = "Documenter l'API congés",
                        Detail = "Décrire le calcul du solde pour l'équipe.",
                        Status = "Backlog",
                        Comment = "À prendre après le correctif.",
                        WeekStart = weekStart,
                        ManagerName = managerName,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
            }

            foreach (var offset in new[] { 0, 2 })
            {
                var day = weekStart.AddDays(offset);
                if (day >= today || await db.AttendanceRecords.AnyAsync(record => record.EmployeeId == employee.Id && record.Date == day))
                    continue;
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    Date = day,
                    CheckIn = new TimeOnly(9, 0),
                    CheckOut = new TimeOnly(17, 0),
                    Status = AttendanceStatus.Present
                });
            }

            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var hasCompletedDay = await db.AttendanceRecords.AnyAsync(record =>
                record.EmployeeId == employee.Id
                && record.Date >= monthStart
                && record.CheckIn != null
                && record.CheckOut != null);
            var sampleDay = monthStart;
            if (!hasCompletedDay && sampleDay < today && !await db.AttendanceRecords.AnyAsync(record => record.EmployeeId == employee.Id && record.Date == sampleDay))
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    Date = sampleDay,
                    CheckIn = new TimeOnly(9, 0),
                    CheckOut = new TimeOnly(17, 30),
                    Status = AttendanceStatus.Present
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> users,
        string email,
        string password,
        string firstName,
        string lastName,
        string role)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            LockoutEnabled = true,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        return user;
    }

    private static Employee EmployeeOf(
        string code,
        string firstName,
        string lastName,
        string email,
        DateOnly hireDate,
        string contractType,
        Guid departmentId,
        Guid positionId,
        Guid? managerId,
        string userId) => new()
    {
        Id = Guid.NewGuid(),
        InternalCode = code,
        FirstName = firstName,
        LastName = lastName,
        ProfessionalEmail = email,
        HireDate = hireDate,
        ContractType = contractType,
        DepartmentId = departmentId,
        PositionId = positionId,
        ManagerId = managerId,
        UserId = userId,
        Status = EmployeeStatus.Active
    };

    private static EmploymentContract Contract(Guid employeeId, string type, DateOnly start, DateOnly? end) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        Type = type,
        Status = ContractStatus.Active,
        StartDate = start,
        EndDate = end
    };
}
