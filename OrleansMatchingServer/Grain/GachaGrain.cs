using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class GachaGrain : Grain, IGachaGrain
    {
        private const int Cost = 160;

        private readonly IPersistentState<GachaState> _state;
        private readonly GachaDataRepository _gachaDataRepository;
        private readonly SessionRepository _sessionRepository;
        private readonly ILogger<GachaGrain> _logger;

        public GachaGrain(
            [PersistentState("gacha", "gachaStore")] IPersistentState<GachaState> state,
            GachaDataRepository gachaDataRepository,
            SessionRepository sessionRepository,
            ILogger<GachaGrain> logger)
        {
            _state = state;
            _gachaDataRepository = gachaDataRepository;
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        public async Task<GachaResult> DrawAsync(string sessionId, int count)
        {
            if (count is not (1 or 10))
                throw new ArgumentOutOfRangeException(nameof(count), "가챠는 1회 또는 10회만 실행할 수 있습니다.");

            var userId = this.GetPrimaryKeyString();
            await _sessionRepository.EnsureUserSessionAsync(sessionId, userId);

            var table = await _gachaDataRepository.GetTableAsync();
            var walletGrain = GrainFactory.GetGrain<IWalletGrain>(userId);
            var totalCost = Cost * count;
            var spendResult = await walletGrain.SpendGemAsync(sessionId, totalCost);

            if (spendResult.Success == false)
            {
                _logger.LogWarning(
                    "가챠 실패. UserId={UserId}, Count={Count}, Cost={Cost}",
                    userId,
                    count,
                    totalCost);

                throw new InvalidOperationException("재화가 부족합니다.");
            }

            var originalPityPoint = _state.State.PityPoint;
            var stateWritten = false;

            try
            {
                var result = new List<Card>();
                for (var i = 0; i < count; i++)
                    result.Add(DrawOne(table));

                await _state.WriteStateAsync();
                stateWritten = true;

                var wallet = await walletGrain.GetWalletAsync(sessionId);

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
            catch
            {
                if (stateWritten == false)
                {
                    _state.State.PityPoint = originalPityPoint;
                    await walletGrain.AddGemAsync(sessionId, spendResult.PaidGemUsed, spendResult.FreeGemUsed);
                }

                throw;
            }
        }

        public async Task<GachaState> GetPityInfoAsync(string sessionId)
        {
            await _sessionRepository.EnsureUserSessionAsync(sessionId, this.GetPrimaryKeyString());

            return _state.State;
        }

        private Card DrawOne(GachaTable table)
        {
            _state.State.PityPoint++;
            var rarity = PickRarity(table.Probabilities);

            if (table.CardsByRarity.TryGetValue(rarity, out var pool) == false || pool.Count == 0)
                throw new InvalidOperationException($"가챠 테이블 오류. Rarity={rarity}");

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
