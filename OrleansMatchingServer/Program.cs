using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;


await Host.CreateDefaultBuilder(args)
    .UseOrleans(silo => silo.UseLocalhostClustering())
    .RunConsoleAsync();