using Scanner.Server.BLL;
using Scanner.Server.DAL;
using Scanner.Server.Model;
using Scanner.Web.Filters;
using Scanner.Web.Services;
using System.Text.Json;

namespace Scanner.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScannerWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));
        services.Configure<FeishuRobotOptions>(configuration.GetSection(FeishuRobotOptions.SectionName));
        services.AddHttpClient<IFeishuRobotService, FeishuRobotService>();
        services.AddScoped<ApiKeyAuthorizationFilter>();
        services.AddScoped<ApiExceptionFilter>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<IAccountService, MySqlAccountService>();
        services.AddScannerDataAccess(configuration);
        services.AddScannerBusinessLogic();
        return services;
    }

    private static IServiceCollection AddScannerDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseOptions options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new();
        services.AddSingleton(options);
        services.AddSingleton<IMySqlConnectionFactory, MySqlConnectionFactory>();
        services.AddScoped<IDatabaseHealthRepository, DatabaseHealthRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IShelvedPalletRepository, ShelvedPalletRepository>();
        return services;
    }

    private static IServiceCollection AddScannerBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IShelvedPalletService, ShelvedPalletService>();
        return services;
    }
}
