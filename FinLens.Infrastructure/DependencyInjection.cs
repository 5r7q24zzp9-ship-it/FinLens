using FinLens.Application.Common.Interfaces;
using FinLens.Infrastructure.Cache;
using FinLens.Infrastructure.Persistence;
using FinLens.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinLens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        var useRedis = configuration.GetValue<bool>("Cache:UseRedis");

        if (useRedis)
        {
            var redisConnection = configuration["Cache:RedisConnection"]
                ?? throw new InvalidOperationException("Cache:RedisConnection tanimli degil.");

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "finlens:";
            });

            services.AddScoped<ICacheService>(sp => new CacheService(
                sp.GetRequiredService<ILogger<CacheService>>(),
                useRedis: true,
                distributedCache: sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>()
            ));
        }
        else
        {
            services.AddMemoryCache();
            services.AddScoped<ICacheService>(sp => new CacheService(
                sp.GetRequiredService<ILogger<CacheService>>(),
                useRedis: false,
                memoryCache: sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
            ));
        }

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}