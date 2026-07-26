using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using OrleansMatchingServer;
using Orleans.Runtime;
using StackExchange.Redis;


await Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .UseOrleans((context, silo)=>
    {
        var siloPort = context.Configuration.GetValue("SiloPort", 11111);
        var gatewayPort = context.Configuration.GetValue("GatewayPort", 30000);
        var primaryPort = context.Configuration.GetValue("PrimaryPort", 11111);

        silo.UseLocalhostClustering(
            siloPort: siloPort,
            gatewayPort: gatewayPort,
            primarySiloEndpoint: new IPEndPoint(IPAddress.Loopback, primaryPort))
        .AddMemoryGrainStorage("matchStore")
        .Configure<Orleans.Configuration.ActivationCountBasedPlacementOptions>(options =>
            options.ChooseOutOf = 2)
        .Configure<Orleans.Configuration.ClusterOptions>(options =>
        {
            options.ClusterId = "dev";
            options.ServiceId = "dev";
        });

        silo.Services.AddSingleton<PlacementStrategy, ActivationCountBasedPlacement>();

    })        
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
        services.AddSingleton(sp => new GachaHistoryRepository(
            postgres,
            sp.GetRequiredService<WalletRepository>()));
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
