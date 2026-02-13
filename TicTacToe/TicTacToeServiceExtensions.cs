using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TicTacToe.BackgroundServices;
using TicTacToe.Data;
using TicTacToe.Hubs;
using TicTacToe.Services;

namespace TicTacToe;

public static class TicTacToeServiceExtensions
{
    public static IServiceCollection AddTicTacToe(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IShortCodeService, ShortCodeService>();
        services.AddScoped<IGameService, GameService>();
        services.AddHostedService<GameCleanupService>();

        return services;
    }

    public static IEndpointRouteBuilder MapTicTacToe(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<GameHub>("/hubs/tictactoe");
        return endpoints;
    }
}
