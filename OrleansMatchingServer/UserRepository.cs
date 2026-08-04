using Dapper;
using Npgsql;

namespace OrleansMatchingServer;

public class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> CreateUserAsync(string userId, string passwordHash, DateTimeOffset createdTime)
    {
        const string sql = """
            insert into user_info(user_id, password_hash, created_time)
            values (@UserId, @PasswordHash, @CreatedTime);
            """;

        await using var conn = new NpgsqlConnection(_connectionString);

        try
        {
            await conn.ExecuteAsync(sql, new
            {
                UserId = userId,
                PasswordHash = passwordHash,
                CreatedTime = createdTime
            });

            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task<UserData?> GetUserAsync(string userId)
    {
        const string sql = """
            select
                user_id as "UserId",
                password_hash as "PasswordHash"
            from user_info
            where user_id = @UserId;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);

        return await conn.QuerySingleOrDefaultAsync<UserData>(sql, new { UserId = userId });
    }

    public async Task UpdatePasswordHashAsync(string userId, string passwordHash)
    {
        const string sql = """
            update user_info
            set password_hash = @PasswordHash
            where user_id = @UserId;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            UserId = userId,
            PasswordHash = passwordHash
        });
    }
}

public sealed record UserData(string UserId, string PasswordHash);
