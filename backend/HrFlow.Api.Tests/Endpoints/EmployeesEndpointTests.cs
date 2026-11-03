using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HrFlow.Api.Dtos;
using HrFlow.Api.Tests.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HrFlow.Api.Tests.Endpoints;

[Collection("api")]
public class EmployeesEndpointTests(HrFlowApiFactory factory)
{
     private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Rh_can_list_employees()
    {
        // Arrange
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new  LoginRequest  { Email = "rh@hrflow.local", Password = "Rh12345!" } );
        loginResponse.EnsureSuccessStatusCode();

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(Json);
        var token = loginData!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Act
        var response = await client.GetAsync("/api/employees");
      

        // Assert
        var page = await response.Content.ReadFromJsonAsync<PagedResult<EmployeeDto>>(Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(page);

       Assert.Contains(page.Items, e => e.InternalCode == "EMP-0001");

    }
    


}
   