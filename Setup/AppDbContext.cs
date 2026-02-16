using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EFCoreSecondLevelCacheInterceptor;
using Memoria.Models.Config;
using Memoria.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoria.Setup;

public static class AppDbContextSetup
{
    public static IServiceCollection AddConfiguredDbContext(
        this IServiceCollection services)
    {
        services.AddEFSecondLevelCache(options =>
        {
            options.UseMemoryCacheProvider();
            options.ConfigureLogging(true);
        });
        services.AddDbContext<AppDbContext>((serviceProvider, optionsBuilder) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<DatabaseConfig>>().Value;
            
            optionsBuilder
                .UseSqlite(config.ConnectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());
        });

        return services;
    }
}