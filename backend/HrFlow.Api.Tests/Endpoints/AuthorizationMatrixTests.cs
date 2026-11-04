using System.Net;
using HrFlow.Api.Tests.Infrastructure;
namespace HrFlow.Api.Tests.Endpoints;



[Collection("api")]
public class AuthorizationMatrixTests(HrFlowApiFactory factory)
{
    [Theory]
    [InlineData("/api/health", Role.Anonymous, HttpStatusCode.OK)]
    [InlineData("/api/health", Role.Admin, HttpStatusCode.OK)]
    [InlineData("/api/health", Role.Rh, HttpStatusCode.OK)]
    [InlineData("/api/health", Role.Manager, HttpStatusCode.OK)]
    [InlineData("/api/health", Role.Employee, HttpStatusCode.OK)]

    [InlineData("/api/users", Role.Anonymous, HttpStatusCode.Unauthorized)]
    [InlineData("/api/users", Role.Admin, HttpStatusCode.OK)]
    [InlineData("/api/users", Role.Rh, HttpStatusCode.Forbidden)]
    [InlineData("/api/users", Role.Manager, HttpStatusCode.Forbidden)]
    [InlineData("/api/users", Role.Employee, HttpStatusCode.Forbidden)]

    [InlineData("/api/audit", Role.Anonymous, HttpStatusCode.Unauthorized)]
    [InlineData("/api/audit", Role.Admin, HttpStatusCode.OK)]
    [InlineData("/api/audit", Role.Rh, HttpStatusCode.Forbidden)]
    [InlineData("/api/audit", Role.Manager, HttpStatusCode.Forbidden)]
    [InlineData("/api/audit", Role.Employee, HttpStatusCode.Forbidden)]

    [InlineData("/api/me/space", Role.Anonymous, HttpStatusCode.Unauthorized)]
    [InlineData("/api/me/space", Role.Admin, HttpStatusCode.Forbidden)]
    [InlineData("/api/me/space", Role.Rh, HttpStatusCode.Forbidden)]
    [InlineData("/api/me/space", Role.Manager, HttpStatusCode.Forbidden)]
    [InlineData("/api/me/space", Role.Employee, HttpStatusCode.OK)]

    [InlineData("/api/team", Role.Anonymous, HttpStatusCode.Unauthorized)]
    [InlineData("/api/team", Role.Admin, HttpStatusCode.Forbidden)]
    [InlineData("/api/team", Role.Rh, HttpStatusCode.Forbidden)]
    [InlineData("/api/team", Role.Manager, HttpStatusCode.OK)]
    [InlineData("/api/team", Role.Employee, HttpStatusCode.Forbidden)]

    [InlineData("/api/employees", Role.Anonymous, HttpStatusCode.Unauthorized)]
    [InlineData("/api/employees", Role.Admin, HttpStatusCode.OK)]
    [InlineData("/api/employees", Role.Rh, HttpStatusCode.OK)]
    [InlineData("/api/employees", Role.Manager, HttpStatusCode.OK)]
    [InlineData("/api/employees", Role.Employee, HttpStatusCode.Forbidden)]


    public async Task Get_endpoint_returns_expected_status(string url, Role role, HttpStatusCode expectedStatus)
    {
        var client = await factory.CreateClientAsAsync(role);
        var response = await client.GetAsync(url);
        Assert.Equal(expectedStatus, response.StatusCode);
    }

}