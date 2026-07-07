using Common;
using Dapper;
using Npgsql;

namespace OrleansMatchingServer;

public class WalletRepository
{
    private readonly string _connectionString;

    public WalletRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddGemAsync(string userId, int paidGem, int freeGem)
    {
        const string sql = """
            insert into player_wallet(user_id, paid_gem, free_gem, updated_at)
            values (@UserId, @PaidGem, @FreeGem, @UpdatedAt)
            on conflict (user_id) do update
            set paid_gem = player_wallet.paid_gem + excluded.paid_gem,
                free_gem = player_wallet.free_gem + excluded.free_gem,
                updated_at = excluded.updated_at;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            UserId = userId,
            PaidGem = paidGem,
            FreeGem = freeGem,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<PlayerWallet> GetWalletAsync(string userId)
    {
        const string sql = """
            select
                paid_gem as "PaidGem",
                free_gem as "FreeGem"
            from player_wallet
            where user_id = @UserId;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<PlayerWallet>(sql, new { UserId = userId })
            ?? new PlayerWallet();
    }

    public async Task<SpendGemResult> SpendGemAsync(string userId, int amount)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        const string ensureSql = """
            insert into player_wallet(user_id, paid_gem, free_gem, updated_at)
            values (@UserId, 0, 0, @UpdatedAt)
            on conflict (user_id) do nothing;
            """;

        await conn.ExecuteAsync(ensureSql, new
        {
            UserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow
        }, tx);

        const string selectSql = """
            select
                paid_gem as "PaidGem",
                free_gem as "FreeGem"
            from player_wallet
            where user_id = @UserId
            for update;
            """;

        var wallet = await conn.QuerySingleAsync<PlayerWallet>(selectSql, new { UserId = userId }, tx);
        var total = wallet.PaidGem + wallet.FreeGem;
        if (total < amount)
        {
            await tx.RollbackAsync();
            return new SpendGemResult
            {
                Success = false,
                PaidGem = wallet.PaidGem,
                FreeGem = wallet.FreeGem
            };
        }

        var freeUsed = Math.Min(wallet.FreeGem, amount);
        var paidUsed = amount - freeUsed;
        var paidGemAfter = wallet.PaidGem - paidUsed;
        var freeGemAfter = wallet.FreeGem - freeUsed;

        const string updateSql = """
            update player_wallet
            set paid_gem = paid_gem - @PaidGemUsed,
                free_gem = free_gem - @FreeGemUsed,
                updated_at = @UpdatedAt
            where user_id = @UserId;
            """;

        await conn.ExecuteAsync(updateSql, new
        {
            UserId = userId,
            PaidGemUsed = paidUsed,
            FreeGemUsed = freeUsed,
            UpdatedAt = DateTimeOffset.UtcNow
        }, tx);

        await tx.CommitAsync();

        return new SpendGemResult
        {
            Success = true,
            PaidGemUsed = paidUsed,
            FreeGemUsed = freeUsed,
            PaidGem = paidGemAfter,
            FreeGem = freeGemAfter
        };
    }
}
