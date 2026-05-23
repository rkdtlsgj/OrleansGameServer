using Common;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace OrleansMatchingServer
{
    public class LoginGrain : Grain, ILoginGrain
    {
        private readonly UserRepository _userRepository;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<LoginGrain> _logger;

        public LoginGrain(
            UserRepository userRepository,
            IConnectionMultiplexer redis,
            ILogger<LoginGrain> logger)
        {
            _userRepository = userRepository;
            _redis = redis;
            _logger = logger;
        }

        public async Task<bool> RegisterAsync(string psw)
        {
            var userId = this.GetPrimaryKeyString();
            var createdTime = DateTimeOffset.UtcNow;
            var passwordHash = HashPassword(psw);

            var created = await _userRepository.CreateUserAsync(userId, passwordHash, createdTime);
            if (created == false)
            {
                _logger.LogWarning(
                    "등록 실패. UserId={UserId}",
                    userId);

                return false;
            }

            _logger.LogInformation(
                "등록 성공. UserId={UserId}, CreatedTime={CreatedTime}",
                userId,
                createdTime);

            return true;
        }

        public async Task<string?> LoginAsync(string psw)
        {
            var userId = this.GetPrimaryKeyString();
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
            {
                _logger.LogWarning(
                    "로그인 실패. UserId={UserId}",
                    userId);

                return null;
            }

            if (user.PasswordHash != HashPassword(psw))
            {
                _logger.LogWarning(
                    "비밀번호 오류. UserId={UserId}",
                    user.UserId);

                return null;
            }

            var sessionId = Guid.NewGuid().ToString();
            var db = _redis.GetDatabase();

            await db.StringSetAsync($"session:{sessionId}", user.UserId, TimeSpan.FromHours(24));

            _logger.LogInformation(
                "로그인 성공. UserId={UserId}, SessionExpiresInHours={SessionExpiresInHours}",
                user.UserId,
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
