using Common;
using Microsoft.Extensions.Logging;

namespace OrleansMatchingServer
{
    public class GachaGrain : Grain, IGachaGrain
    {
        private const int Cost = 160;
        private const int PityThreshold = 90;
        private const string PityRarity = "SSR";

        private readonly GachaDataRepository _gachaDataRepository;
        private readonly GachaHistoryRepository _gachaHistoryRepository;
        private readonly SessionRepository _sessionRepository;
        private readonly ILogger<GachaGrain> _logger;
        private int? _pityPoint;

        public GachaGrain(
            GachaDataRepository gachaDataRepository,
            GachaHistoryRepository gachaHistoryRepository,
            SessionRepository sessionRepository,
            ILogger<GachaGrain> logger)
        {
            _gachaDataRepository = gachaDataRepository;
            _gachaHistoryRepository = gachaHistoryRepository;
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

            var pityPoint = await GetPityPointAsync(userId);
            var historyWritten = false;

            try
            {
                var result = new List<Card>();
                for (var i = 0; i < count; i++)
                    result.Add(DrawOne(table, ref pityPoint));

                await _gachaHistoryRepository.SaveDrawAsync(userId, result, pityPoint);
                historyWritten = true;
                _pityPoint = pityPoint;

                _logger.LogDebug(
                    "가챠 성공. UserId={UserId}, Count={Count}, Cost={Cost}, PityPoint={PityPoint}, PaidGem={PaidGem}, FreeGem={FreeGem}",
                    userId,
                    count,
                    totalCost,
                    pityPoint,
                    spendResult.PaidGem,
                    spendResult.FreeGem);

                return new GachaResult
                {
                    Cards = result,
                    PityPoint = pityPoint,
                    PaidGem = spendResult.PaidGem,
                    FreeGem = spendResult.FreeGem
                };
            }
            catch
            {
                if (historyWritten == false)
                    await walletGrain.AddGemAsync(sessionId, spendResult.PaidGemUsed, spendResult.FreeGemUsed);

                throw;
            }
        }

        public async Task<GachaState> GetPityInfoAsync(string sessionId)
        {
            var userId = this.GetPrimaryKeyString();
            await _sessionRepository.EnsureUserSessionAsync(sessionId, userId);

            return new GachaState
            {
                PityPoint = await GetPityPointAsync(userId)
            };
        }

        public async Task<IReadOnlyList<GachaHistoryItem>> GetHistoryAsync(string sessionId, int limit)
        {
            var userId = this.GetPrimaryKeyString();
            await _sessionRepository.EnsureUserSessionAsync(sessionId, userId);

            return await _gachaHistoryRepository.GetHistoryAsync(userId, limit);
        }

        public async Task<IReadOnlyList<PlayerCharacter>> GetCharactersAsync(string sessionId)
        {
            var userId = this.GetPrimaryKeyString();
            await _sessionRepository.EnsureUserSessionAsync(sessionId, userId);

            return await _gachaHistoryRepository.GetCharactersAsync(userId);
        }

        private static Card DrawOne(GachaTable table, ref int pityPoint)
        {
            pityPoint++;

            var isPity = pityPoint >= PityThreshold;
            var rarity = isPity ? PityRarity : PickRarity(table.Probabilities);

            if (table.CardsByRarity.TryGetValue(rarity, out var pool) == false || pool.Count == 0)
                throw new InvalidOperationException($"가챠 테이블 오류. Rarity={rarity}");

            var card = Pick(pool);
            card.IsPity = isPity;

            if (rarity == PityRarity)
                pityPoint = 0;

            card.PityPointAfter = pityPoint;

            return card;
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

        private async Task<int> GetPityPointAsync(string userId)
        {
            if (_pityPoint.HasValue)
                return _pityPoint.Value;

            _pityPoint = await _gachaHistoryRepository.GetPityPointAsync(userId);
            return _pityPoint.Value;
        }
    }
}
