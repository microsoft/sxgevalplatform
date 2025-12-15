using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SxgEvalPlatformApi.Services
{
    // Helper to register an existing singleton as hosted service when needed
    public class ServiceFactoryHostedService<T> : IHostedService where T : class
    {
        private readonly IServiceProvider _provider;

        public ServiceFactoryHostedService(IServiceProvider provider)
        {
            _provider = provider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Resolve to ensure creation
            var _ = _provider.GetService<T>();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
