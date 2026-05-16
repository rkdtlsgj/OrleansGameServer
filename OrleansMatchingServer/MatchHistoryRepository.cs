using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

public class MatchHistoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<MatchHistoryRepository> _logger;

    public MatchHistoryRepository(string connectionString, ILogger<MatchHistoryRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
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

        _logger.LogInformation(
            "Match history saved. MatchId={MatchId}, Channel={Channel}, Player1={Player1}, Player2={Player2}, CreatedAt={CreatedAt}",
            matchId,
            channel,
            player1,
            player2,
            createdAt);
    }
}
