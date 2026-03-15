using Dapper;
using Npgsql;
using System.Threading.Channels;

public class MatchHistoryRepository
{
    private readonly string _connectionString;

    public MatchHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SaveMatchAsync(Guid matchId, string channel, string player1, string player2, DateTimeOffset createdAt)
    {
        const string sql = """
            insert into match_history(match_id, channel, player1, player2, created_at)
            values (@MatchId, @Channel, @Player1, @Player2, @CreatedAt);
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            MatchId = matchId,
            Channel = channel,
            Player1 = player1,
            Player2 = player2,
            CreatedAt = createdAt
        });
    }
}

