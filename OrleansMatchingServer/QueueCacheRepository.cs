using StackExchange.Redis;
public class QueueCacheRepository
{

    private readonly IDatabase _db;

    public QueueCacheRepository(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public Task AddToQueueAsync(string channel, string nickname)
    {
        var batch = _db.CreateBatch();
        var t1 = batch.SetAddAsync($"channel:{channel}:members", nickname);
        var t2 = batch.StringSetAsync($"user:{nickname}:channel", channel);
        batch.Execute();
        return Task.WhenAll(t1, t2);
    }

    public Task RemoveFromQueueAsync(string channel, string nickname)
    {
        var batch = _db.CreateBatch();
        var t1 = batch.SetRemoveAsync($"channel:{channel}:members", nickname);
        var t2 = batch.KeyDeleteAsync($"user:{nickname}:channel");
        batch.Execute();
        return Task.WhenAll(t1, t2);
    }

    public async Task<long> GetQueueCountAsync(string channel)
    {
        return await _db.SetLengthAsync($"channel:{channel}:members");
    }
}

