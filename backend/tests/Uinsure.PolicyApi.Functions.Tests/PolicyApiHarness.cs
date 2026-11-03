using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Uinsure.PolicyApi.Core;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Infrastructure;

namespace Uinsure.PolicyApi.Functions.Tests;

/// <summary>
/// Wires up the real application services against an in-memory SQLite database and
/// invokes the function methods the way the Functions host would: one DI scope per request.
/// </summary>
public sealed class PolicyApiHarness : IAsyncDisposable
{
    public static readonly DateOnly Today = new(2026, 6, 15);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // An in-memory SQLite database lives as long as its connection is open.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;

    public PolicyApiHarness()
    {
        _connection.Open();

        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)))
            .AddPolicyCore()
            .AddPolicyInfrastructure(options => options.UseSqlite(_connection))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        _services.EnsurePolicyDatabaseCreatedAsync().GetAwaiter().GetResult();
    }

    public IServiceProvider Services => _services;

    public Task<IActionResult> SellAsync(object body) =>
        SellRawAsync(JsonSerializer.Serialize(body, Json));

    public async Task<IActionResult> SellRawAsync(string body, string contentType = "application/json")
    {
        await using var scope = _services.CreateAsyncScope();
        var request = CreateRequest("POST", "/api/policies", body, contentType);

        return await CreateFunctions(scope).SellPolicy(request, TestContext.Current.CancellationToken);
    }

    public async Task<IActionResult> GetAsync(string reference)
    {
        await using var scope = _services.CreateAsyncScope();
        var request = CreateRequest("GET", $"/api/policies/{reference}");

        return await CreateFunctions(scope).GetPolicy(request, reference, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static PolicyFunctions CreateFunctions(AsyncServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<IPolicyService>(), NullLogger<PolicyFunctions>.Instance);

    private static HttpRequest CreateRequest(string method, string path, string? body = null, string? contentType = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        if (body is not null)
        {
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        return context.Request;
    }

    /// <summary>A valid sell request body, shaped exactly as an API consumer would send it.</summary>
    public static Dictionary<string, object?> ValidBody(string reference = "POL-0001") => new()
    {
        ["reference"] = reference,
        ["insuranceType"] = "BuyToLet",
        ["startDate"] = Today.AddDays(14).ToString("yyyy-MM-dd"),
        ["amount"] = 425.50m,
        ["autoRenew"] = true,
        ["paymentType"] = "DirectDebit",
        ["property"] = new Dictionary<string, object?>
        {
            ["addressLine1"] = "Flat 2",
            ["addressLine2"] = "10 Station Road",
            ["addressLine3"] = "Exampleton",
            ["postcode"] = "ex1 2am",
        },
        ["policyholders"] = new[]
        {
            new Dictionary<string, object?> { ["firstName"] = "Jane", ["lastName"] = "Smith", ["dateOfBirth"] = "1985-04-12" },
            new Dictionary<string, object?> { ["firstName"] = "John", ["lastName"] = "Smith", ["dateOfBirth"] = "1983-11-30" },
        },
    };
}
