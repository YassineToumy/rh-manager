using HrFlow.Api.Data;
namespace HrFlow.Api.Tests.Infrastructure;



public record TestUser(string Email, string Password);
public static class TestUsers
{
    public static readonly TestUser Rh = new("rh@hrflow.local", DbSeeder.RhPassword);
    public static readonly TestUser Employee = new("employe@hrflow.local", DbSeeder.EmployeePassword);
    public static readonly TestUser Manager = new("manager@hrflow.local", DbSeeder.ManagerPassword);
    public static readonly TestUser Admin = new("admin@hrflow.local", DbSeeder.AdminPassword);
    
}