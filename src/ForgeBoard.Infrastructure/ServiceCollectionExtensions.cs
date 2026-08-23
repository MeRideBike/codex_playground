using ForgeBoard.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeBoard.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddForgeBoardInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<ForgeBoardDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IMissionRepository, EfMissionRepository>();
        services.AddSingleton<IAgentProvider, DeterministicFakeAgentProvider>();
        return services;
    }
}
