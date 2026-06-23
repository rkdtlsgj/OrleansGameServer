using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class WalletGrain : Grain, IWalletGrain
    {
        private readonly IPersistentState<PlayerWallet> _state;
        private readonly SessionRepository _sessionRepository;
        private readonly ILogger<WalletGrain> _logger;

        public WalletGrain(
            [PersistentState("wallet", "walletStore")] IPersistentState<PlayerWallet> state,
            SessionRepository sessionRepository,
            ILogger<WalletGrain> logger)
        {
            _state = state;
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

            _state.State.PaidGem += paidGem;
            _state.State.FreeGem += freeGem;

            await _state.WriteStateAsync();

            _logger.LogInformation(
                "재화 추가. UserId={UserId}, PaidGemAdded={PaidGemAdded}, FreeGemAdded={FreeGemAdded}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                this.GetPrimaryKeyString(),
                paidGem,
                freeGem,
                _state.State.PaidGem,
                _state.State.FreeGem);
        }

        public async Task<PlayerWallet> GetWalletAsync(string sessionId)
        {
            await EnsureSessionAsync(sessionId);

            return _state.State;
        }

        public async Task<SpendGemResult> SpendGemAsync(string sessionId, int amount)
        {
            await EnsureSessionAsync(sessionId);

            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "사용할 재화는 0보다 커야 합니다.");

            var total = _state.State.PaidGem + _state.State.FreeGem;
            if (total < amount)
            {
                _logger.LogWarning(
                    "재화 부족. UserId={UserId}, Amount={Amount}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                    this.GetPrimaryKeyString(),
                    amount,
                    _state.State.PaidGem,
                    _state.State.FreeGem);

                return new SpendGemResult { Success = false };
            }

            var freeUsed = Math.Min(_state.State.FreeGem, amount);
            _state.State.FreeGem -= freeUsed;

            var paidUsed = amount - freeUsed;
            _state.State.PaidGem -= paidUsed;

            await _state.WriteStateAsync();

            _logger.LogInformation(
                "재화 사용. UserId={UserId}, Amount={Amount}, FreeGemUsed={FreeGemUsed}, PaidGemUsed={PaidGemUsed}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                this.GetPrimaryKeyString(),
                amount,
                freeUsed,
                paidUsed,
                _state.State.PaidGem,
                _state.State.FreeGem);

            return new SpendGemResult
            {
                Success = true,
                PaidGemUsed = paidUsed,
                FreeGemUsed = freeUsed
            };
        }

        private Task EnsureSessionAsync(string sessionId)
        {
            return _sessionRepository.EnsureUserSessionAsync(sessionId, this.GetPrimaryKeyString());
        }
    }
}
