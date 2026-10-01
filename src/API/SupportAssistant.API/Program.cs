using DotNetEnv;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Scalar.AspNetCore;
using Shared.Application.Abstractions;
using SupportAssistant.API.Extensions;

// Local development convenience: values from a git-ignored .env file (see .env.example) become
// environment variables. Real environment variables always win; other environments use only those.
if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
{
    Env.TraversePath().NoClobber().Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebApiServices();
builder.Services.AddModuleServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "v1";
});

app.MapOpenApi();
app.MapScalarApiReference();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

    await migrator.MigrateAsync();
    await seeder.SeedAsync();
}

app.Run();

public partial class Program;
