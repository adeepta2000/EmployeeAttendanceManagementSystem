using System.Net;
using System.Net.Http.Json;
using Attendance.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Attendance.Api.Tests.Platform;

/// <summary>Health checks, correlation IDs, error responses and the API document.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformTests(AttendanceApiFactory api)
{
    private const string CorrelationHeader = "X-Correlation-ID";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_public_and_healthy(string path)
    {
        using var client = api.CreateApiClient();

        var response = await client.GetAsync(path, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Be("Healthy");
    }

    [Fact]
    public async Task A_supplied_correlation_id_is_echoed_and_included_in_problem_details()
    {
        using var client = api.CreateApiClient();
        client.DefaultRequestHeaders.Add(CorrelationHeader, "bff-req-42");

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "unknown" }, Ct);

        response.Headers.GetValues(CorrelationHeader).Should().Equal("bff-req-42");
        var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        problem.Extensions["correlationId"]!.ToString().Should().Be("bff-req-42");
    }

    [Fact]
    public void The_OpenAPI_document_generates_and_lists_the_organization_and_scheduling_endpoints()
    {
        var document = api.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        document.Paths.Keys.Should().Contain(
        [
            "/api/departments", "/api/departments/{publicId}", "/api/designations", "/api/work-locations",
            "/api/employees", "/api/employees/{publicId}", "/api/employees/{publicId}/deactivate", "/api/employees/import",
            "/api/shifts", "/api/weekly-off-patterns", "/api/holidays", "/api/rosters", "/api/rosters/{publicId}", "/api/rosters/bulk",
            "/api/employees/{publicId}/schedule", "/api/employees/{publicId}/calendar",
        ]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("has spaces; {braces} & 'quotes'")]
    public async Task A_missing_or_malformed_correlation_id_is_replaced(string? supplied)
    {
        using var client = api.CreateApiClient();
        if (supplied is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationHeader, supplied);
        }

        var response = await client.GetAsync("/health/live", Ct);

        var correlationId = response.Headers.GetValues(CorrelationHeader).Single();
        correlationId.Should().MatchRegex("^[0-9a-f]{32}$");
    }
}
