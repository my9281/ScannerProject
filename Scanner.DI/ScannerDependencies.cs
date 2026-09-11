using System;
using Microsoft.Extensions.DependencyInjection;
using Scanner.Helpers.Services;

namespace Scanner.DI
{
    // One composition root per application; clients receive dependencies through constructors.
    public sealed class ScannerDependencies : IDisposable
    {
        private readonly ServiceProvider _provider;
        public ScannerDependencies()
        {
            var services = new ServiceCollection();
            services.AddScannerSharedServices();
            _provider = services.BuildServiceProvider();
        }
        public ChecklistDataCache BaseData => _provider.GetRequiredService<ChecklistDataCache>();
        public void Dispose() => _provider.Dispose();
    }

    public static class ScannerServiceRegistration
    {
        public static IServiceCollection AddScannerSharedServices(this IServiceCollection services)
        {
            services.AddSingleton<ChecklistDataCache>();
            return services;
        }
    }
}
