using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Attendance.Application.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>Calls to the auth endpoints, the way the Next.js BFF makes them.</summary>
internal static class AuthApi
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = AttendanceApiFactory.Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, Ct);

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, Ct);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/auth/logout", new { refreshToken }, Ct);

    public static Task<HttpResponseMessage> ChangePasswordAsync(
        this HttpClient client, string accessToken, string currentPassword, string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request, Ct);
    }

    public static Task<HttpResponseMessage> ForgotPasswordAsync(this HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/forgot-password", new { email }, Ct);

    public static Task<HttpResponseMessage> ResetPasswordAsync(this HttpClient client, string token, string newPassword) =>
        client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword }, Ct);

    public static async Task<AuthTokens> SignInAsync(this HttpClient client, TestUser user) =>
        await (await client.LoginAsync(user.Email)).ReadTokensAsync();

    public static Task<HttpResponseMessage> GetWithTokenAsync(this HttpClient client, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request, Ct);
    }

    public static async Task<AuthTokens> ReadTokensAsync(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<AuthTokens>(Ct))!;
    }

    public static async Task<ProblemDetails> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
    }

    /// <summary>The stored form of a refresh token, computed independently of the production code.</summary>
    public static string Sha256Hex(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
