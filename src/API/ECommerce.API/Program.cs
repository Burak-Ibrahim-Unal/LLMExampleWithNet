using ECommerce.API.Common.Middleware;
using ECommerce.API.Extensions;
using FastEndpoints;
using Shared.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebApiServices();
builder.Services.AddModuleServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<UserIdPreProcessor>();
app.UseMiddleware<SubscriptionPreProcessor>();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "v1";
});

app.UseOpenApi();
app.UseSwaggerUi();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

    await migrator.MigrateAsync();
    await seeder.SeedAsync();
}

app.Run();
