using StackExchange.Redis;

namespace OrleansMatchingServer;

public class SessionRepository
{
    private readonly IDatabase _db;

    public SessionRepository(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<string> CreateSessionAsync(string userId, TimeSpan expiresIn)
    {
        var sessionId = Guid.NewGuid().ToString();
        await _db.StringSetAsync(GetSessionKey(sessionId), userId, expiresIn);

        return sessionId;
    }

    public async Task<string> GetRequiredUserIdAsync(string sessionId)
    {
        var userId = await _db.StringGetAsync(GetSessionKey(sessionId));
        if (userId.HasValue == false)
            throw new UnauthorizedAccessException("유효하지 않은 세션입니다.");

        return userId.ToString();
    }

    public async Task EnsureUserSessionAsync(string sessionId, string expectedUserId)
    {
        var userId = await GetRequiredUserIdAsync(sessionId);
        if (userId != expectedUserId)
            throw new UnauthorizedAccessException("세션 사용자와 요청 사용자가 일치하지 않습니다.");
    }

    private static string GetSessionKey(string sessionId) => $"session:{sessionId}";
}
