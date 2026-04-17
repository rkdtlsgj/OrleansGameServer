using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var host = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client =>
    {
        client.UseLocalhostClustering();
    })
    .Build();


await host.StartAsync();

var client = host.Services.GetRequiredService<IClusterClient>();

Console.Write("닉네임: ");
var nickname = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(nickname))
    return;

Console.Write("채널명: ");
var queueKey = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(queueKey))
    return;

var queue = client.GetGrain<IMatchmakingQueueGrain>(queueKey);

// Observer 등록 클라이언트 알람용도
var observer = new ConsoleMatchObserver();
var observerRef = client.CreateObjectReference<IMatchObserver>(observer);

Console.WriteLine("명령: /join, /cancel, /exit");

while (true)
{
    var line = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(line))
        continue;

    if (line.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (line.Equals("/join", StringComparison.OrdinalIgnoreCase))
        await queue.Enqueue(nickname, observerRef);

    else if (line.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
        await queue.Cancel(nickname);

    else
        Console.WriteLine("명령: /join, /cancel, /exit");
}

client.DeleteObjectReference<IMatchObserver>(observerRef);

await host.StopAsync();
