using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<ApplicationUser>(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmploymentEvent> EmploymentEvents => Set<EmploymentEvent>();
    public DbSet<EmploymentContract> Contracts => Set<EmploymentContract>();
    public DbSet<EmployeeDocument> Documents => Set<EmployeeDocument>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<PerformanceGoal> PerformanceGoals => Set<PerformanceGoal>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Role).HasMaxLength(32).IsRequired().HasDefaultValue(AppRoles.Employee);
            entity.Property(user => user.IsApproved).HasDefaultValue(true);
            entity.HasIndex(user => user.Role);
            entity.HasIndex(user => user.IsApproved);
        });

        builder.Entity<Department>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(120).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(1000);
            entity.HasIndex(d => d.Name).IsUnique();
        });

        builder.Entity<Position>(entity =>
        {
            entity.Property(p => p.Title).HasMaxLength(120).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.HasIndex(p => new { p.DepartmentId, p.Title }).IsUnique();
            entity.HasOne(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Employee>(entity =>
        {
            entity.Property(e => e.InternalCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ProfessionalEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.ContractType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => e.InternalCode).IsUnique();
            entity.HasIndex(e => e.ProfessionalEmail).IsUnique();
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Position)
                .WithMany(p => p.Employees)
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Manager)
                .WithMany(e => e.DirectReports)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<Employee>(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EmploymentEvent>(entity =>
        {
            entity.Property(e => e.Kind).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.EmployeeId);
            entity.HasOne(e => e.Employee)
                .WithMany(e => e.History)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmploymentContract>(entity =>
        {
            entity.Property(c => c.Type).HasMaxLength(40).IsRequired();
            entity.Property(c => c.Notes).HasMaxLength(1000);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(c => c.EndDate);
            entity.HasIndex(c => c.EmployeeId);
            entity.HasOne(c => c.Employee)
                .WithMany(e => e.Contracts)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeDocument>(entity =>
        {
            entity.Property(d => d.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(d => d.StoredFileName).HasMaxLength(80).IsRequired();
            entity.Property(d => d.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(d => d.UploadedByUserId).HasMaxLength(450).IsRequired();
            entity.HasOne(d => d.Employee)
                .WithMany(e => e.Documents)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Contract)
                .WithMany()
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LeaveType>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(t => t.Name).IsUnique();
        });

        builder.Entity<LeaveBalance>(entity =>
        {
            entity.HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year }).IsUnique();
            entity.HasOne(b => b.Employee)
                .WithMany(e => e.LeaveBalances)
                .HasForeignKey(b => b.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(b => b.LeaveType)
                .WithMany()
                .HasForeignKey(b => b.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LeaveRequest>(entity =>
        {
            entity.Property(r => r.Reason).HasMaxLength(500);
            entity.Property(r => r.DecisionComment).HasMaxLength(500);
            entity.Property(r => r.DecidedByUserId).HasMaxLength(450);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(r => new { r.EmployeeId, r.Status });
            entity.HasIndex(r => new { r.StartDate, r.EndDate });
            entity.HasOne(r => r.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.LeaveType)
                .WithMany()
                .HasForeignKey(r => r.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.Property(a => a.Note).HasMaxLength(500);
            entity.Property(a => a.CorrectedByUserId).HasMaxLength(450);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique();
            entity.HasOne(a => a.Employee)
                .WithMany(e => e.AttendanceRecords)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobOffer>(entity =>
        {
            entity.Property(o => o.Title).HasMaxLength(160).IsRequired();
            entity.Property(o => o.Description).HasMaxLength(4000).IsRequired();
            entity.Property(o => o.Requirements).HasMaxLength(4000).IsRequired();
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(o => o.Position)
                .WithMany()
                .HasForeignKey(o => o.PositionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Candidate>(entity =>
        {
            entity.Property(c => c.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(c => c.LastName).HasMaxLength(80).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(200).IsRequired();
            entity.Property(c => c.CvStoredFileName).HasMaxLength(80);
            entity.Property(c => c.CvOriginalFileName).HasMaxLength(260);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(c => c.JobOfferId);
            entity.HasOne(c => c.JobOffer)
                .WithMany(o => o.Candidates)
                .HasForeignKey(c => c.JobOfferId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PerformanceGoal>(entity =>
        {
            entity.Property(g => g.Title).HasMaxLength(160).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(1000);
            entity.Property(g => g.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(g => g.Employee)
                .WithMany(e => e.Goals)
                .HasForeignKey(g => g.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PerformanceReview>(entity =>
        {
            entity.Property(r => r.Comment).HasMaxLength(2000);
            entity.HasIndex(r => r.ScheduledAt);
            entity.HasOne(r => r.Employee)
                .WithMany(e => e.Reviews)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Reviewer)
                .WithMany()
                .HasForeignKey(r => r.ReviewerEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.RecipientUserId).HasMaxLength(450).IsRequired();
            entity.Property(n => n.Title).HasMaxLength(160).IsRequired();
            entity.Property(n => n.Body).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.DedupKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(n => n.DedupKey).IsUnique();
            entity.HasIndex(n => new { n.RecipientUserId, n.CreatedAt });
            entity.HasOne(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditEntry>(entity =>
        {
            entity.Property(a => a.ActorUserId).HasMaxLength(450).IsRequired();
            entity.Property(a => a.Action).HasMaxLength(80).IsRequired();
            entity.Property(a => a.EntityName).HasMaxLength(80).IsRequired();
            entity.Property(a => a.EntityId).HasMaxLength(80).IsRequired();
            entity.Property(a => a.Details).HasMaxLength(500);
            entity.HasIndex(a => a.OccurredAt);
        });
    }
}

public class AppDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=hrflow;Username=hrflow;Password=hrflow_dev")
            .Options;
        return new AppDbContext(options);
    }
}
