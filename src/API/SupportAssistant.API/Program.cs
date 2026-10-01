using DotNetEnv;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Knowledge.Service.Abstractions;
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

    // Build the search index from knowledge-base/; unchanged documents keep their stored embeddings.
    // A failure is logged rather than thrown: the API still starts and reports 503 until a reindex succeeds.
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var ingestion = await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().ReindexAsync();

        if (ingestion.Success)
        {
            logger.LogInformation(
                "Knowledge base indexed: {Documents} documents, {Chunks} sections, {RetrievalMode} retrieval.",
                ingestion.Data!.Documents,
                ingestion.Data.Chunks,
                ingestion.Data.RetrievalMode);

            if (ingestion.Data.Warning is not null)
            {
                logger.LogWarning("{Warning}", ingestion.Data.Warning);
            }
        }
        else
        {
            logger.LogError("Knowledge base could not be indexed: {Message}", ingestion.Message);
        }
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Knowledge base indexing failed at startup; questions return 503 until POST /v1/documents/reindex succeeds.");
    }
}

app.Run();

public partial class Program;
