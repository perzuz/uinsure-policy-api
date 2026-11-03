using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Uinsure.PolicyApi.Core;
using Uinsure.PolicyApi.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// host.json only filters the Functions host's logs; the isolated worker process has its own
// logging pipeline, so framework noise (EF Core SQL, MVC result execution) is filtered here.
builder.Logging
    .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
    .AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services
    .AddPolicyCore()
    .AddPolicyInfrastructure(builder.Configuration.GetConnectionString("Policies") ?? "Data Source=policies.db");

var host = builder.Build();

await host.Services.EnsurePolicyDatabaseCreatedAsync();

await host.RunAsync();
