using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace OrleansMatchingServer;

public class GachaDataRepository
{
    private readonly string _connectionString;
    private readonly ILogger<GachaDataRepository> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private GachaTable? _cache;

    public GachaDataRepository(string connectionString, ILogger<GachaDataRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<GachaTable> GetTableAsync()
    {
        if (_cache != null)
            return _cache;

        await _cacheLock.WaitAsync();

        try
        {
            if (_cache != null)
                return _cache;

            await using var conn = new NpgsqlConnection(_connectionString);

            var cards = (await conn.QueryAsync<GachaCardData>(
                """
                select
                    card_id::text as CardId,
                    name as Name,
                    rarity as Rarity
                from character_info
                order by rarity, name;
                """)).ToList();

            var probabilities = (await conn.QueryAsync<GachaProbabilityData>(
                """
                select
                    rarity as Rarity,
                    probability::double precision as Probability
                from gacha_probability;
                """)).ToList();

            if (cards.Count == 0)
                throw new InvalidOperationException("character_info 테이블에 가챠 캐릭터 정보가 없습니다.");

            if (probabilities.Count == 0)
                throw new InvalidOperationException("gacha_probability 테이블에 가챠 확률 정보가 없습니다.");

            IReadOnlyDictionary<string, IReadOnlyList<GachaCardData>> cardsByRarity = cards
                .GroupBy(card => card.Rarity)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<GachaCardData>)group.ToArray());

            foreach (var probability in probabilities)
            {
                if (probability.Probability <= 0)
                    throw new InvalidOperationException($"가챠 확률은 0보다 커야 합니다. Rarity={probability.Rarity}");

                if (cardsByRarity.ContainsKey(probability.Rarity) == false)
                    throw new InvalidOperationException($"확률은 있지만 캐릭터가 없는 rarity입니다. Rarity={probability.Rarity}");
            }

            _cache = new GachaTable(probabilities.ToArray(), cardsByRarity);

            _logger.LogInformation(
                "가챠 정보 캐시. CardCount={CardCount}, ProbabilityCount={ProbabilityCount}",
                cards.Count,
                probabilities.Count);

            return _cache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}

public sealed record GachaTable(
    IReadOnlyList<GachaProbabilityData> Probabilities,
    IReadOnlyDictionary<string, IReadOnlyList<GachaCardData>> CardsByRarity);

public sealed record GachaCardData(string CardId, string Name, string Rarity);

public sealed record GachaProbabilityData(string Rarity, double Probability);
