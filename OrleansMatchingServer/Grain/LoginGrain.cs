using Common;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace OrleansMatchingServer
{
    public class LoginGrain : Grain, ILoginGrain
    {
        private readonly IPersistentState<PlayerState> _state;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<LoginGrain> _logger;

        public LoginGrain(
            [PersistentState("player", "playerStore")] IPersistentState<PlayerState> state,
            IConnectionMultiplexer redis,
            ILogger<LoginGrain> logger)
        {
            _state = state;
            _redis = redis;
            _logger = logger;
        }

        public async Task<bool> RegisterAsync(string psw)
        {
            var userId = this.GetPrimaryKeyString();

            if (_state.RecordExists)
            {
                _logger.LogWarning(
                    "Register failed. UserId={UserId}",
                    userId);

                return false;
            }

            _state.State = new PlayerState
            {
                UserId = userId,
                PasswordHash = HashPassword(psw),
                CreatedTime = DateTimeOffset.Now
            };

            await _state.WriteStateAsync();

            _logger.LogInformation(
                "User registered. UserId={UserId}, CreatedTime={CreatedTime}",
                _state.State.UserId,
                _state.State.CreatedTime);

            return true;
        }

        public async Task<string?> LoginAsync(string psw)
        {
            var userId = this.GetPrimaryKeyString();

            if (_state.RecordExists == false)
            {
                _logger.LogWarning(
                    "Login failed. UserId={UserId}",
                    userId);

                return null;
            }

            if (_state.State.PasswordHash != HashPassword(psw))
            {
                _logger.LogWarning(
                    "Login password failed. UserId={UserId}",
                    _state.State.UserId);

                return null;
            }

            var sessionId = Guid.NewGuid().ToString();
            var db = _redis.GetDatabase();

            await db.StringSetAsync($"session:{sessionId}", _state.State.UserId, TimeSpan.FromHours(24));

            _logger.LogInformation(
                "Login succeeded. UserId={UserId}, SessionExpiresInHours={SessionExpiresInHours}",
                _state.State.UserId,
                24);

            return sessionId;
        }

        private static string HashPassword(string psw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(psw));
            return Convert.ToHexString(bytes);
        }
    }
}
