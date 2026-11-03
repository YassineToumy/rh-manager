
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using HrFlow.Api.Data; 
using HrFlow.Api.Services;

namespace HrFlow.Api.Tests.Infrastructure;

public class HrFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Use the PostgreSQL container instead of the normal database.
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            _postgres.GetConnectionString()
        );

        builder.ConfigureTestServices(services =>
        {
            // Remove the reminder background service.
            var descriptor = services.FirstOrDefault(
                d => d.ImplementationType == typeof(ReminderBackgroundService)
            );

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }
        });
    }

    public async Task InitializeAsync()
    {
        // Start PostgreSQL first.
        await _postgres.StartAsync();

        // Create a scope to resolve scoped services.
        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        // Apply database migrations.
        await dbContext.Database.MigrateAsync();

        // Insert initial data.
        await DbSeeder.SeedAsync(scope.ServiceProvider);

        // Ensure employee workspace exists.
        await DbSeeder.EnsureEmployeeSpaceAsync(scope.ServiceProvider);
    }

    public new async Task DisposeAsync()
    {
         await base.DisposeAsync();
        await _postgres.DisposeAsync();
       
    }
}
