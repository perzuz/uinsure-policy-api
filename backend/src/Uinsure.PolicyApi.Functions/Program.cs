using System.Globalization;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Uinsure.PolicyApi.Core;
using Uinsure.PolicyApi.Functions;
using Uinsure.PolicyApi.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// host.json only filters the Functions host's logs; the isolated worker process has its own
// logging pipeline, so framework noise (EF Core SQL, MVC result execution) is filtered here.
builder.Logging
    .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
    .AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// Local demo aid: pretend today is another date (e.g. to try renewing). Registered before
// AddPolicyCore, which only adds the system clock if no TimeProvider is registered.
var timeOverride = builder.Configuration["TimeOverride:Today"];
var hasTimeOverride = DateOnly.TryParseExact(timeOverride, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var overriddenToday);
if (hasTimeOverride)
{
    builder.Services.AddSingleton<TimeProvider>(new FixedDateTimeProvider(overriddenToday));
}

builder.Services
    .AddPolicyCore()
    .AddPolicyInfrastructure(builder.Configuration.GetConnectionString("Policies") ?? "Data Source=policies.db");

var host = builder.Build();

if (hasTimeOverride)
{
    // Written to stderr because ILogger output is only forwarded to the Functions host once the
    // worker has connected (after this point), and host.json hides the worker's stdout.
    Console.Error.WriteLine($"WARNING: TimeOverride is set; the API is treating {overriddenToday:yyyy-MM-dd} as today.");
}

await host.Services.EnsurePolicyDatabaseCreatedAsync();

await host.RunAsync();
