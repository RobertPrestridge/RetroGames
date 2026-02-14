using PocketTanks.Data;
using PocketTanks.Engine;
using PocketTanks.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace PocketTanks;

public static class PocketTanksServiceExtensions
{
    public static IServiceCollection AddPocketTanks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TanksDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<TanksGameManager>();
        services.AddHostedService<TanksGameLoop>();

        return services;
    }

    public static IEndpointRouteBuilder MapPocketTanks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<TanksHub>("/hubs/tanks");
        return endpoints;
    }
}
