using System.Net;
using System.Net.Http.Json;
using HrFlow.Api.Dtos;
using HrFlow.Api.Tests.Infrastructure;


namespace HrFlow.Api.Tests.Endpoints;

[Collection("api")]
public class EmployeesEndpointTests(HrFlowApiFactory factory)
{


    [Fact]
    public async Task Rh_can_list_employees()
    {
        // Arrange
        var client = await factory.CreateClientAsAsync(TestUsers.Rh);
        // Act
        var response = await client.GetAsync("/api/employees");


        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<EmployeeDto>>(TestJson.Options);

        Assert.NotNull(page);

        Assert.Contains(page.Items, e => e.InternalCode == "EMP-0001");

    }



}
