using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class LoginGrain : Grain, ILoginGrain
    {
        private readonly UserRepository _userRepository;
        private readonly SessionRepository _sessionRepository;
        private readonly ILogger<LoginGrain> _logger;

        public LoginGrain(
            UserRepository userRepository,
            SessionRepository sessionRepository,
            ILogger<LoginGrain> logger)
        {
            _userRepository = userRepository;
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        public async Task<bool> RegisterAsync(string psw)
        {
            var userId = this.GetPrimaryKeyString();
            var createdTime = DateTimeOffset.UtcNow;
            var passwordHash = PasswordHasher.Hash(psw);

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

            var verification = PasswordHasher.Verify(psw, user.PasswordHash);
            if (verification.Verified == false)
            {
                _logger.LogWarning(
                    "비밀번호 오류. UserId={UserId}",
                    user.UserId);

                return null;
            }

            if (verification.NeedsRehash)
            {
                await _userRepository.UpdatePasswordHashAsync(user.UserId, PasswordHasher.Hash(psw));

                _logger.LogInformation(
                    "비밀번호 해시 업그레이드. UserId={UserId}",
                    user.UserId);
            }

            var sessionId = await _sessionRepository.CreateSessionAsync(user.UserId, TimeSpan.FromHours(24));

            _logger.LogInformation(
                "로그인 성공. UserId={UserId}, SessionExpiresInHours={SessionExpiresInHours}",
                user.UserId,
                24);

            return sessionId;
        }
    }
}
