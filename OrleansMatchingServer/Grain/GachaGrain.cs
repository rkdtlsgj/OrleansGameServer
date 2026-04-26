using Common;
using Orleans.Serialization.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansMatchingServer
{
    public class GachaGrain : Grain, IGachaGrain
    {
        private readonly IPersistentState<GachaState> _state;


        private static readonly List<string> SSR = ["에르핀(왕도)", "네르(빡침)", "코미(수영복)"]; // 테스트용 원래라면 DB든 json이든 따로 작업해야함
        private static readonly List<string> SR = ["에르핀", "네르", "코미"];
        private static readonly List<string> R = ["스피키", "이프리트", "쥬비"];

        private const double SSRate = 0.02;
        private const double RSRate = 0.18;
        private const int Cost = 160;

        public GachaGrain([PersistentState("gacha", "gachaStore")]IPersistentState<GachaState> state)
        {
            _state = state;
        }

        public async Task<GachaResult> DrawAsync(int count)
        {
            var walletGrain = GrainFactory.GetGrain<IWalletGrain>(this.GetPrimaryKeyString());

            var totalCost = Cost * count;
            var success = await walletGrain.SpendGemAsync(totalCost);

            if (success == false)
                throw new InvalidOperationException("재화 부족!");

            var result = new List<Card>();
            for (int i = 0; i < count; i++)
                result.Add(DrawOne());

            await _state.WriteStateAsync();

            var wallet = await walletGrain.GetWalletAsync();

            return new GachaResult
            {
                Cards = result,
                PityPoint = _state.State.PityPoint,
                PaidGem = wallet.PaidGem,
                FreeGem = wallet.FreeGem
            };
        }


        public Task<GachaState> GetPityInfoAsync() => Task.FromResult(_state.State);

        private Card DrawOne()
        {
            _state.State.PityPoint++; //포인트 증가

            var isSSR = Random.Shared.NextDouble() < SSRate;

            if (isSSR == true)
            {
                return Pick(SSR, "SSR");
            }

            var isSR = Random.Shared.NextDouble() < SSRate / (1 - SSRate);

            return isSR ? Pick(SR, "SR") : Pick(R, "R");
        }

        private static Card Pick(List<string> pool, string rarity) => new()
        {
            CardId = Guid.NewGuid().ToString(),
            Name = pool[Random.Shared.Next(pool.Count)],
            Rarity = rarity,
            ObtaiendAt = DateTimeOffset.Now
        };
    }
}
