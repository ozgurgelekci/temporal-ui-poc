using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Extensions.Hosting;
using Workflows;

const string TaskQueue = "document-pipeline";

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddSingleton<ProcessingActivities>();
builder.Services.AddSingleton<SigningActivities>();

builder.Services
    .AddHostedTemporalWorker(
        clientTargetHost: Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233",
        clientNamespace: "default",
        taskQueue: TaskQueue)
    .AddWorkflow<DocumentProcessingWorkflow>()
    .AddWorkflow<DocumentSigningWorkflow>()
    .AddScopedActivities<ProcessingActivities>()
    .AddScopedActivities<SigningActivities>();

var host = builder.Build();

host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Worker")
    .LogInformation("Worker starting on task queue '{TaskQueue}'", TaskQueue);

await host.RunAsync();
