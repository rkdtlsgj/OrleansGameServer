using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class WalletGrain : Grain, IWalletGrain
    {
        private readonly IPersistentState<PlayerWallet> _state;
        private readonly ILogger<WalletGrain> _logger;

        public WalletGrain(
            [PersistentState("wallet", "walletStore")] IPersistentState<PlayerWallet> state,
            ILogger<WalletGrain> logger)
        {
            _state = state;
            _logger = logger;
        }

        public async Task AddGemAsync(int paidGem, int freeGem)
        {
            _state.State.PaidGem += paidGem;
            _state.State.FreeGem += freeGem;

            await _state.WriteStateAsync();

            _logger.LogInformation(
                "Gem added. UserId={UserId}, PaidGemAdded={PaidGemAdded}, FreeGemAdded={FreeGemAdded}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                this.GetPrimaryKeyString(),
                paidGem,
                freeGem,
                _state.State.PaidGem,
                _state.State.FreeGem);
        }

        public Task<PlayerWallet> GetWalletAsync() => Task.FromResult(_state.State);

        public async Task<bool> SpendGemAsync(int amount)
        {
            var total = _state.State.PaidGem + _state.State.FreeGem;
            if (total < amount)
            {
                _logger.LogWarning(
                    "Gem spend failed. UserId={UserId}, Amount={Amount}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                    this.GetPrimaryKeyString(),
                    amount,
                    _state.State.PaidGem,
                    _state.State.FreeGem);

                return false; // 자원부족
            }

            //무료잼 먼저 사용
            var freeUsed = Math.Min(_state.State.FreeGem, amount);
            _state.State.FreeGem -= freeUsed;

            var paidUsed = amount - freeUsed;
            _state.State.PaidGem -= paidUsed;

            await _state.WriteStateAsync();

            _logger.LogInformation(
                "Gem spent. UserId={UserId}, Amount={Amount}, FreeGemUsed={FreeGemUsed}, PaidGemUsed={PaidGemUsed}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                this.GetPrimaryKeyString(),
                amount,
                freeUsed,
                paidUsed,
                _state.State.PaidGem,
                _state.State.FreeGem);

            return true;
        }
    }
}
