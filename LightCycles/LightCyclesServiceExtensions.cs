using LightCycles.Data;
using LightCycles.Engine;
using LightCycles.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LightCycles;

public static class LightCyclesServiceExtensions
{
    public static IServiceCollection AddLightCycles(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TronDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<TronGameManager>();
        services.AddHostedService<TronGameLoop>();

        return services;
    }

    public static IEndpointRouteBuilder MapLightCycles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<TronHub>("/hubs/tron");
        return endpoints;
    }
}
