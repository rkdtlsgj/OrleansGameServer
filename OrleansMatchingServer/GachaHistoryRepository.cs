using Common;
using Dapper;
using Npgsql;
using NpgsqlTypes;

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
        if (cards.Count == 0)
            return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var updatedAt = DateTimeOffset.UtcNow;

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
            UpdatedAt = updatedAt
        }, tx);

        await using (var writer = await conn.BeginBinaryImportAsync(
            """
            copy gacha_history(
                draw_id,
                user_id,
                card_id,
                name,
                rarity,
                obtained_at,
                pity_point_after,
                is_pity)
            from stdin (format binary)
            """))
        {
            foreach (var card in cards)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
                await writer.WriteAsync(userId, NpgsqlDbType.Text);
                await writer.WriteAsync(card.CardId, NpgsqlDbType.Text);
                await writer.WriteAsync(card.Name, NpgsqlDbType.Text);
                await writer.WriteAsync(card.Rarity, NpgsqlDbType.Text);
                await writer.WriteAsync(card.ObtaiendAt, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(card.PityPointAfter, NpgsqlDbType.Integer);
                await writer.WriteAsync(card.IsPity, NpgsqlDbType.Boolean);
            }

            await writer.CompleteAsync();
        }

        const string upsertCharacterSql = """
            insert into player_character(
                user_id,
                card_id,
                name,
                rarity,
                count,
                first_obtained_at,
                last_obtained_at)
            select
                @UserId,
                card_id,
                name,
                rarity,
                count,
                first_obtained_at,
                last_obtained_at
            from unnest(
                @CardIds::text[],
                @Names::text[],
                @Rarities::text[],
                @Counts::integer[],
                @FirstObtainedAts::timestamptz[],
                @LastObtainedAts::timestamptz[])
            as item(
                card_id,
                name,
                rarity,
                count,
                first_obtained_at,
                last_obtained_at)
            on conflict (user_id, card_id) do update
            set count = player_character.count + excluded.count,
                name = excluded.name,
                rarity = excluded.rarity,
                last_obtained_at = excluded.last_obtained_at;
            """;

        var characterRows = cards
            .GroupBy(card => card.CardId)
            .Select(group =>
            {
                var first = group.First();
                return new
                {
                    CardId = group.Key,
                    first.Name,
                    first.Rarity,
                    Count = group.Count(),
                    FirstObtainedAt = group.Min(card => card.ObtaiendAt),
                    LastObtainedAt = group.Max(card => card.ObtaiendAt)
                };
            })
            .ToArray();

        await conn.ExecuteAsync(upsertCharacterSql, new
        {
            UserId = userId,
            CardIds = characterRows.Select(row => row.CardId).ToArray(),
            Names = characterRows.Select(row => row.Name).ToArray(),
            Rarities = characterRows.Select(row => row.Rarity).ToArray(),
            Counts = characterRows.Select(row => row.Count).ToArray(),
            FirstObtainedAts = characterRows.Select(row => row.FirstObtainedAt).ToArray(),
            LastObtainedAts = characterRows.Select(row => row.LastObtainedAt).ToArray()
        }, tx);

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
