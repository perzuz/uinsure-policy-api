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
using Uinsure.PolicyApi.Functions.Policies;
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
            .AddSingleton<TimeProvider>(Clock)
            .AddPolicyCore()
            .AddPolicyInfrastructure(options => options.UseSqlite(_connection))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        _services.EnsurePolicyDatabaseCreatedAsync().GetAwaiter().GetResult();
    }

    /// <summary>The API's clock. Starts at <see cref="Today"/>; tests can move it forward.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero));

    public IServiceProvider Services => _services;

    public void SetToday(DateOnly today) => Clock.SetUtcNow(new DateTimeOffset(today, TimeOnly.MinValue, TimeSpan.Zero));

    public Task<IActionResult> SellAsync(object body) =>
        SellRawAsync(JsonSerializer.Serialize(body, Json));

    public Task<IActionResult> SellRawAsync(string body, string contentType = "application/json") =>
        InvokeAsync("POST", "/api/policies", body, contentType, (scope, request) =>
            new SellPolicyFunction(Service(scope), NullLogger<SellPolicyFunction>.Instance)
                .Run(request, TestContext.Current.CancellationToken));

    public Task<IActionResult> GetAsync(string reference) =>
        InvokeAsync("GET", $"/api/policies/{reference}", null, null, (scope, request) =>
            new GetPolicyFunction(Service(scope)).Run(request, reference, TestContext.Current.CancellationToken));

    public Task<IActionResult> CancelAsync(string reference, object body) =>
        CancelRawAsync(reference, JsonSerializer.Serialize(body, Json));

    public Task<IActionResult> CancelRawAsync(string reference, string body, string contentType = "application/json") =>
        InvokeAsync("POST", $"/api/policies/{reference}/cancel", body, contentType, (scope, request) =>
            new CancelPolicyFunction(Service(scope), NullLogger<CancelPolicyFunction>.Instance)
                .Run(request, reference, TestContext.Current.CancellationToken));

    public Task<IActionResult> RenewAsync(string reference, object body) =>
        RenewRawAsync(reference, JsonSerializer.Serialize(body, Json));

    public Task<IActionResult> RenewRawAsync(string reference, string body, string contentType = "application/json") =>
        InvokeAsync("POST", $"/api/policies/{reference}/renew", body, contentType, (scope, request) =>
            new RenewPolicyFunction(Service(scope), NullLogger<RenewPolicyFunction>.Instance)
                .Run(request, reference, TestContext.Current.CancellationToken));

    public Task<IActionResult> QuoteAsync(string reference, string? cancellationDate = null) =>
        InvokeAsync(
            "GET",
            $"/api/policies/{reference}/cancellation-quote",
            null,
            null,
            (scope, request) =>
            {
                if (cancellationDate is not null)
                {
                    request.QueryString = QueryString.Create("cancellationDate", cancellationDate);
                }

                return new GetCancellationQuoteFunction(Service(scope)).Run(request, reference, TestContext.Current.CancellationToken);
            });

    public Task<IActionResult> RecordClaimAsync(string reference) =>
        InvokeAsync("POST", $"/api/policies/{reference}/claims", null, null, (scope, request) =>
            new RecordClaimFunction(Service(scope)).Run(request, reference, TestContext.Current.CancellationToken));

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<IActionResult> InvokeAsync(
        string method,
        string path,
        string? body,
        string? contentType,
        Func<AsyncServiceScope, HttpRequest, Task<IActionResult>> function)
    {
        await using var scope = _services.CreateAsyncScope();
        return await function(scope, CreateRequest(method, path, body, contentType));
    }

    private static IPolicyService Service(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IPolicyService>();

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
