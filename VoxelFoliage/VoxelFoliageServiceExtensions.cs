using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace VoxelFoliage;

public static class VoxelFoliageServiceExtensions
{
    public static IServiceCollection AddVoxelFoliage(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapVoxelFoliage(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
