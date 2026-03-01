
namespace Common
{
    //Grain에서 사용하는 사용자타입의 정의 같은 경우 깊은 복사 및 바이트변환을 편리하게 해주기 위한 직렬화
    [GenerateSerializer]
    public record MatchInfo(Guid MatchId, string QueueKey, string player1, string player2, DateTimeOffset CreateAt);
}
