using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using OrleansMatchingServer;
using StackExchange.Redis;


await Host.CreateDefaultBuilder(args)
    .UseOrleans(silo => silo.UseLocalhostClustering()
    .AddMemoryGrainStorage("matchStore"))

    .ConfigureServices((context, services) =>
    {
        var postgres = GetRequiredConnectionString(context.Configuration, "Postgres");
        var redis = GetRequiredConnectionString(context.Configuration, "Redis");

        services.AddSingleton(sp => new MatchHistoryRepository(
            postgres,
            sp.GetRequiredService<ILogger<MatchHistoryRepository>>()));
        services.AddSingleton(sp => new GachaDataRepository(
            postgres,
            sp.GetRequiredService<ILogger<GachaDataRepository>>()));
        services.AddSingleton(_ => new WalletRepository(postgres));
        services.AddSingleton(_ => new GachaHistoryRepository(postgres));
        services.AddSingleton(_ => new UserRepository(postgres));
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redis));

        services.AddSingleton<QueueCacheRepository>();
        services.AddSingleton<SessionRepository>();

    })
    .RunConsoleAsync();

static string GetRequiredConnectionString(IConfiguration configuration, string name)
{
    return configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is missing.");
}
