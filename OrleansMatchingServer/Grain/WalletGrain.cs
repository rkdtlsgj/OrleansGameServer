using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansMatchingServer
{
    public class WalletGrain : Grain, IWalletGrain
    {
        private readonly IPersistentState<PlayerWallet> _state;

        public WalletGrain([PersistentState("wallet", "walletStore")] IPersistentState<PlayerWallet> state)
        {
            _state = state;
        }

        public async Task AddGemAsync(int paidGem, int freeGem)
        {
            _state.State.PaidGem += paidGem;
            _state.State.FreeGem += freeGem;

            await _state.WriteStateAsync();
        }

        public Task<PlayerWallet> GetWalletAsync() => Task.FromResult(_state.State);

        public async Task<bool> SpendGemAsync(int amount)
        {
            var total = _state.State.PaidGem + _state.State.FreeGem;
            if (total < amount)            
                return false; // 자원부족            


            //무료잼 먼저 사용
            var freeUsed  = Math.Min(_state.State.FreeGem,amount);
            _state.State.FreeGem -= freeUsed;

            var paidUsed = amount - freeUsed;
            _state.State.PaidGem -= paidUsed;

            await _state.WriteStateAsync();
            return true;

        }
    }
}
