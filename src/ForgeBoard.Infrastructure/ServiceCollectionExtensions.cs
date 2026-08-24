using ForgeBoard.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeBoard.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddForgeBoardInfrastructure(this IServiceCollection services, string connectionString, IConfiguration configuration, string samplesRoot, string runWorkspacesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(samplesRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runWorkspacesRoot);
        services.AddDbContext<ForgeBoardDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IMissionRepository, EfMissionRepository>();
        services.AddSingleton<ISampleWorkspace>(_ => new ConstrainedSampleWorkspace(samplesRoot));
        services.AddSingleton<IEngineeringMissionCatalog, BundledEngineeringMissionCatalog>();
        services.AddSingleton<IConstrainedSampleExecutor>(_ => new ConstrainedSampleExecutor(runWorkspacesRoot));
        var provider = configuration["ForgeBoard:AgentProvider"] ?? "Fake";
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]))
                throw new InvalidOperationException("OpenAI provider requires OpenAI:ApiKey configuration.");
            services.AddHttpClient<OpenAiAgentProvider>(client => client.BaseAddress = new Uri("https://api.openai.com/v1/"));
            services.AddScoped<IAgentProvider, OpenAiAgentProvider>();
        }
        else if (provider.Equals("Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IAgentProvider, DeterministicFakeAgentProvider>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported agent provider '{provider}'.");
        }
        return services;
    }
}
