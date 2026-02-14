using Asteroids.Data;
using Asteroids.Engine;
using Asteroids.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Asteroids;

public static class AsteroidsServiceExtensions
{
    public static IServiceCollection AddAsteroids(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AsteroidsDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<AsteroidGameManager>();
        services.AddHostedService<AsteroidGameLoop>();

        return services;
    }

    public static IEndpointRouteBuilder MapAsteroids(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<AsteroidsHub>("/hubs/asteroids");
        return endpoints;
    }
}
