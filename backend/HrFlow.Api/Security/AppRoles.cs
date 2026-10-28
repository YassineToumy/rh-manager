namespace HrFlow.Api.Security;

public static class AppRoles
{
    public const string Admin = "Administrateur";
    public const string Rh = "ResponsableRH";
    public const string Manager = "Manager";
    public const string Employee = "Employe";

    public const string Staff = Admin + "," + Rh;
    public const string LeaveDeciders = Admin + "," + Rh + "," + Manager;

    public static readonly string[] All = [Admin, Rh, Manager, Employee];

    public static readonly string[] SelfService = [Rh, Manager, Employee];

    public const string WaitingForApproval = "wait until admin approves your request";
}
