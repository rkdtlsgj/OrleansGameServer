using Common;
using Dapper;
using Npgsql;

namespace OrleansMatchingServer;

public class GachaHistoryRepository
{
    private readonly string _connectionString;

    public GachaHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> GetPityPointAsync(string userId)
    {
        const string sql = """
            select pity_point
            from gacha_user_state
            where user_id = @UserId;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<int?>(sql, new { UserId = userId }) ?? 0;
    }

    public async Task SaveDrawAsync(string userId, IReadOnlyList<Card> cards, int pityPoint)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        const string upsertStateSql = """
            insert into gacha_user_state(user_id, pity_point, updated_at)
            values (@UserId, @PityPoint, @UpdatedAt)
            on conflict (user_id) do update
            set pity_point = excluded.pity_point,
                updated_at = excluded.updated_at;
            """;

        await conn.ExecuteAsync(upsertStateSql, new
        {
            UserId = userId,
            PityPoint = pityPoint,
            UpdatedAt = DateTimeOffset.UtcNow
        }, tx);

        const string insertHistorySql = """
            insert into gacha_history(
                draw_id,
                user_id,
                card_id,
                name,
                rarity,
                obtained_at,
                pity_point_after,
                is_pity)
            values (
                @DrawId,
                @UserId,
                @CardId,
                @Name,
                @Rarity,
                @ObtainedAt,
                @PityPointAfter,
                @IsPity);
            """;

        await conn.ExecuteAsync(insertHistorySql, cards.Select(card => new
        {
            DrawId = Guid.NewGuid(),
            UserId = userId,
            card.CardId,
            card.Name,
            card.Rarity,
            ObtainedAt = card.ObtaiendAt,
            PityPointAfter = card.PityPointAfter,
            card.IsPity
        }), tx);

        const string upsertCharacterSql = """
            insert into player_character(
                user_id,
                card_id,
                name,
                rarity,
                count,
                first_obtained_at,
                last_obtained_at)
            values (
                @UserId,
                @CardId,
                @Name,
                @Rarity,
                1,
                @ObtainedAt,
                @ObtainedAt)
            on conflict (user_id, card_id) do update
            set count = player_character.count + 1,
                name = excluded.name,
                rarity = excluded.rarity,
                last_obtained_at = excluded.last_obtained_at;
            """;

        await conn.ExecuteAsync(upsertCharacterSql, cards.Select(card => new
        {
            UserId = userId,
            card.CardId,
            card.Name,
            card.Rarity,
            ObtainedAt = card.ObtaiendAt
        }), tx);

        await tx.CommitAsync();
    }

    public async Task<IReadOnlyList<GachaHistoryItem>> GetHistoryAsync(string userId, int limit)
    {
        const string sql = """
            select
                draw_id as "DrawId",
                card_id as "CardId",
                name as "Name",
                rarity as "Rarity",
                obtained_at as "ObtainedAt",
                pity_point_after as "PityPointAfter",
                is_pity as "IsPity"
            from gacha_history
            where user_id = @UserId
            order by obtained_at desc, draw_id desc
            limit @Limit;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        return (await conn.QueryAsync<GachaHistoryItem>(sql, new
        {
            UserId = userId,
            Limit = Math.Clamp(limit, 1, 100)
        })).ToArray();
    }

    public async Task<IReadOnlyList<PlayerCharacter>> GetCharactersAsync(string userId)
    {
        const string sql = """
            select
                card_id as "CardId",
                name as "Name",
                rarity as "Rarity",
                count as "Count",
                first_obtained_at as "FirstObtainedAt",
                last_obtained_at as "LastObtainedAt"
            from player_character
            where user_id = @UserId
            order by
                case rarity
                    when 'SSR' then 1
                    when 'SR' then 2
                    when 'R' then 3
                    else 4
                end,
                name;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        return (await conn.QueryAsync<PlayerCharacter>(sql, new { UserId = userId })).ToArray();
    }
}
