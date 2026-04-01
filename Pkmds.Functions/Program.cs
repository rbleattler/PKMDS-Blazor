using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pkmds.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddSingleton<IGitHubService, GitHubService>()
    .AddSingleton<IBlobService, BlobService>()
    .AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy
                .WithOrigins(
                    "https://codemonkey85.github.io",
                    "http://localhost:5283",
                    "https://localhost:7267")
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

builder.Build().Run();
