using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class GachaGrain : Grain, IGachaGrain
    {
        private readonly IPersistentState<GachaState> _state;
        private readonly GachaDataRepository _gachaDataRepository;
        private readonly ILogger<GachaGrain> _logger;

        private const int Cost = 160;

        public GachaGrain(
            [PersistentState("gacha", "gachaStore")] IPersistentState<GachaState> state,
            GachaDataRepository gachaDataRepository,
            ILogger<GachaGrain> logger)
        {
            _state = state;
            _gachaDataRepository = gachaDataRepository;
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
                    "가챠 실패 UserId={UserId}, Count={Count}, Cost={Cost}",
                    userId,
                    count,
                    totalCost);

                throw new InvalidOperationException("재화 부족!");
            }

            var result = new List<Card>();
            for (int i = 0; i < count; i++)
                result.Add(await DrawOneAsync());

            await _state.WriteStateAsync();

            var wallet = await walletGrain.GetWalletAsync();

            _logger.LogInformation(
                "가챠 성공. UserId={UserId}, Count={Count}, Cost={Cost}, PityPoint={PityPoint}, PaidGem={PaidGem}, FreeGem={FreeGem}",
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

        private async Task<Card> DrawOneAsync()
        {
            _state.State.PityPoint++; //포인트 증가

            var table = await _gachaDataRepository.GetTableAsync();
            var rarity = PickRarity(table.Probabilities);

            if (table.CardsByRarity.TryGetValue(rarity, out var pool) == false || pool.Count == 0)
                throw new InvalidOperationException($"오류체크. Rarity={rarity}");

            return Pick(pool);
        }

        private static string PickRarity(IReadOnlyList<GachaProbabilityData> probabilities)
        {
            var totalProbability = probabilities.Sum(item => item.Probability);
            var roll = Random.Shared.NextDouble() * totalProbability;
            var current = 0d;

            foreach (var item in probabilities)
            {
                current += item.Probability;
                if (roll < current)
                    return item.Rarity;
            }

            return probabilities[^1].Rarity;
        }

        private static Card Pick(IReadOnlyList<GachaCardData> pool)
        {
            var card = pool[Random.Shared.Next(pool.Count)];

            return new Card
            {
                CardId = card.CardId,
                Name = card.Name,
                Rarity = card.Rarity,
                ObtaiendAt = DateTimeOffset.UtcNow
            };
        }
    }
}
