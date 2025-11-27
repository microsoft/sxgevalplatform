using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Grpc;

var host = new HostBuilder()
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Durable Functions worker
        services.AddDurableTaskWorker(builder =>
        {
            builder.UseGrpc(); 
        });

        // Optional: Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
