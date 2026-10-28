using HrFlow.Api.Models;
using HrFlow.Api.Security;
using Microsoft.AspNetCore.Identity;

namespace HrFlow.Api.Services;

public static class AdminAccountCommand
{
    public const string Name = "create-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var options = ReadOptions(args);
        if (options is null)
        {
            Console.WriteLine("Usage: dotnet run --project backend/HrFlow.Api -- create-admin --email <email> --password <mot de passe> --first-name <prénom> --last-name <nom>");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(options.Email) is not null)
        {
            Console.WriteLine("Un compte existe déjà avec cet e-mail.");
            return 1;
        }

        var admin = new ApplicationUser
        {
            UserName = options.Email,
            Email = options.Email,
            EmailConfirmed = true,
            FirstName = options.FirstName,
            LastName = options.LastName,
            IsActive = true,
            IsApproved = true,
            LockoutEnabled = true,
            Role = AppRoles.Admin
        };
        var created = await users.CreateAsync(admin, options.Password);
        if (!created.Succeeded)
        {
            Console.WriteLine(string.Join(" ", created.Errors.Select(error => error.Description)));
            return 1;
        }

        Console.WriteLine($"Administrateur créé : {options.Email}");
        return 0;
    }

    private static AdminOptions? ReadOptions(string[] args)
    {
        string? email = null;
        string? password = null;
        string? firstName = null;
        string? lastName = null;
        for (var index = 1; index < args.Length - 1; index += 2)
        {
            var value = args[index + 1];
            switch (args[index])
            {
                case "--email": email = value.Trim().ToLowerInvariant(); break;
                case "--password": password = value; break;
                case "--first-name": firstName = value.Trim(); break;
                case "--last-name": lastName = value.Trim(); break;
            }
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return null;

        return new AdminOptions(email, password, firstName, lastName);
    }

    private sealed record AdminOptions(string Email, string Password, string FirstName, string LastName);
}
