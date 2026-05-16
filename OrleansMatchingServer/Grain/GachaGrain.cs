using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class GachaGrain : Grain, IGachaGrain
    {
        private readonly IPersistentState<GachaState> _state;
        private readonly ILogger<GachaGrain> _logger;

        private static readonly List<string> SSR = ["에르핀(왕도)", "네르(빡침)", "코미(수영복)"]; // 테스트용 원래라면 DB든 json이든 따로 작업해야함
        private static readonly List<string> SR = ["에르핀", "네르", "코미"];
        private static readonly List<string> R = ["스피키", "이프리트", "쥬비"];

        private const double SSRate = 0.02;
        private const double RSRate = 0.18;
        private const int Cost = 160;

        public GachaGrain(
            [PersistentState("gacha", "gachaStore")] IPersistentState<GachaState> state,
            ILogger<GachaGrain> logger)
        {
            _state = state;
            _logger = logger;
        }

        public async Task<GachaResult> DrawAsync(int count)
        {
            var userId = this.GetPrimaryKeyString();
            var walletGrain = GrainFactory.GetGrain<IWalletGrain>(userId);

            var totalCost = Cost * count;
            var success = await walletGrain.SpendGemAsync(totalCost);

            if (success == false)
            {
                _logger.LogWarning(
                    "Gacha failed UserId={UserId}, Count={Count}, Cost={Cost}",
                    userId,
                    count,
                    totalCost);

                throw new InvalidOperationException("재화 부족!");
            }

            var result = new List<Card>();
            for (int i = 0; i < count; i++)
                result.Add(DrawOne());

            await _state.WriteStateAsync();

            var wallet = await walletGrain.GetWalletAsync();

            _logger.LogInformation(
                "Gacha succeeded. UserId={UserId}, Count={Count}, Cost={Cost}, PityPoint={PityPoint}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                userId,
                count,
                totalCost,
                _state.State.PityPoint,
                wallet.PaidGem,
                wallet.FreeGem);

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
