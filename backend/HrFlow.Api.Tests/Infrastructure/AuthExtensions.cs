
using System.Net.Http.Json;   
using System.Net.Http.Headers;
using HrFlow.Api.Dtos;


namespace HrFlow.Api.Tests.Infrastructure;

public static class AuthExtensions
{
    public static async Task<HttpClient> CreateClientAsAsync(this HrFlowApiFactory factory, TestUser user)
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = user.Password });
        loginResponse.EnsureSuccessStatusCode();

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);

        var token = loginData!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}