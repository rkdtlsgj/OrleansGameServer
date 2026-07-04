using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class WalletGrain : Grain, IWalletGrain
    {
        private readonly WalletRepository _walletRepository;
        private readonly SessionRepository _sessionRepository;
        private readonly ILogger<WalletGrain> _logger;

        public WalletGrain(
            WalletRepository walletRepository,
            SessionRepository sessionRepository,
            ILogger<WalletGrain> logger)
        {
            _walletRepository = walletRepository;
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        public async Task AddGemAsync(string sessionId, int paidGem, int freeGem)
        {
            await EnsureSessionAsync(sessionId);

            if (paidGem < 0)
                throw new ArgumentOutOfRangeException(nameof(paidGem), "충전할 유료젬은 0 이상이어야 합니다.");

            if (freeGem < 0)
                throw new ArgumentOutOfRangeException(nameof(freeGem), "충전할 무료젬은 0 이상이어야 합니다.");

            var userId = this.GetPrimaryKeyString();
            await _walletRepository.AddGemAsync(userId, paidGem, freeGem);
            var wallet = await _walletRepository.GetWalletAsync(userId);

            _logger.LogInformation(
                "재화 추가. UserId={UserId}, PaidGemAdded={PaidGemAdded}, FreeGemAdded={FreeGemAdded}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                userId,
                paidGem,
                freeGem,
                wallet.PaidGem,
                wallet.FreeGem);
        }

        public async Task<PlayerWallet> GetWalletAsync(string sessionId)
        {
            await EnsureSessionAsync(sessionId);

            return await _walletRepository.GetWalletAsync(this.GetPrimaryKeyString());
        }

        public async Task<SpendGemResult> SpendGemAsync(string sessionId, int amount)
        {
            await EnsureSessionAsync(sessionId);

            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "사용할 재화는 0보다 커야 합니다.");

            var userId = this.GetPrimaryKeyString();
            var result = await _walletRepository.SpendGemAsync(userId, amount);
            var wallet = await _walletRepository.GetWalletAsync(userId);

            if (result.Success == false)
            {
                _logger.LogWarning(
                    "재화 부족. UserId={UserId}, Amount={Amount}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                    userId,
                    amount,
                    wallet.PaidGem,
                    wallet.FreeGem);

                return result;
            }

            _logger.LogInformation(
                "재화 사용. UserId={UserId}, Amount={Amount}, FreeGemUsed={FreeGemUsed}, PaidGemUsed={PaidGemUsed}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                userId,
                amount,
                result.FreeGemUsed,
                result.PaidGemUsed,
                wallet.PaidGem,
                wallet.FreeGem);

            return result;
        }

        private Task EnsureSessionAsync(string sessionId)
        {
            return _sessionRepository.EnsureUserSessionAsync(sessionId, this.GetPrimaryKeyString());
        }
    }
}
