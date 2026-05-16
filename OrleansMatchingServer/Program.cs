using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using StackExchange.Redis;


await Host.CreateDefaultBuilder(args)
    .UseOrleans(silo => silo.UseLocalhostClustering()
    .AddMemoryGrainStorage("matchStore")
    .AddMemoryGrainStorage("playerStore")
    .AddMemoryGrainStorage("gachaStore")
    .AddMemoryGrainStorage("walletStore"))

    .ConfigureServices((context, services) =>
    {
        var postgres = context.Configuration.GetConnectionString("Postgres");
        var redis = context.Configuration.GetConnectionString("Redis");

        services.AddSingleton(sp => new MatchHistoryRepository(
            postgres,
            sp.GetRequiredService<ILogger<MatchHistoryRepository>>()));
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redis));

        services.AddSingleton<QueueCacheRepository>();

    })
    .RunConsoleAsync();
