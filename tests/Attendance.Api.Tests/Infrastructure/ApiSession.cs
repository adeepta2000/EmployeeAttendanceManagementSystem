using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>A signed-in caller: every request carries their access token as a bearer token.</summary>
public sealed class ApiSession(HttpClient client, string accessToken, TestUser user) : IDisposable
{
    /// <summary>The API's JSON settings: web defaults, with enums as names.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public TestUser User => user;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path, content: null);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) =>
        SendAsync(HttpMethod.Post, path, body is null ? null : JsonContent.Create(body, options: Json));

    public Task<HttpResponseMessage> PutAsync(string path, object body) =>
        SendAsync(HttpMethod.Put, path, JsonContent.Create(body, options: Json));

    public Task<HttpResponseMessage> PostFileAsync(string path, byte[] file, string fileName, string contentType)
    {
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var form = new MultipartFormDataContent { { part, "file", fileName } };
        return SendAsync(HttpMethod.Post, path, form);
    }

    public void Dispose() => client.Dispose();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request, Ct);
    }
}

internal static class ApiResponseExtensions
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expectedStatus = HttpStatusCode.OK)
    {
        response.StatusCode.Should().Be(expectedStatus, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiSession.Json, Ct))!;
    }

    /// <summary>The <c>code</c> extension of a ProblemDetails response, after checking the status.</summary>
    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        var problem = await response.ReadProblemAsync(expectedStatus);
        return problem.Extensions.TryGetValue("code", out var code) ? code?.ToString() : null;
    }

    /// <summary>The field names of a validation ProblemDetails response.</summary>
    public static async Task<IReadOnlyCollection<string>> ReadValidationErrorKeysAsync(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(ApiSession.Json, Ct);
        return problem!.Errors.Keys;
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
