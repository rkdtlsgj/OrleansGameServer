using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public class QueueCacheRepository
{
    private readonly IDatabase _db;
    private readonly ILogger<QueueCacheRepository> _logger;

    public QueueCacheRepository(IConnectionMultiplexer redis, ILogger<QueueCacheRepository> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task AddToQueueAsync(string channel, string nickname)
    {
        var batch = _db.CreateBatch();
        var t1 = batch.SetAddAsync($"channel:{channel}:members", nickname);
        var t2 = batch.StringSetAsync($"user:{nickname}:channel", channel);
        batch.Execute();
        await Task.WhenAll(t1, t2);

        _logger.LogInformation(
            "AddToQueueAsync. Channel={Channel}, UserId={UserId}",
            channel,
            nickname);
    }

    public async Task RemoveFromQueueAsync(string channel, string nickname)
    {
        var batch = _db.CreateBatch();
        var t1 = batch.SetRemoveAsync($"channel:{channel}:members", nickname);
        var t2 = batch.KeyDeleteAsync($"user:{nickname}:channel");
        batch.Execute();
        await Task.WhenAll(t1, t2);

        _logger.LogInformation(
            "RemoveFromQueueAsync. Channel={Channel}, UserId={UserId}",
            channel,
            nickname);
    }
}
