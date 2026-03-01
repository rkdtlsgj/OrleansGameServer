namespace Common
{
    public interface IMatchGrain : IGrainWithGuidKey
    {
        Task Initialize(string key, string player1, string player2);
        Task<MatchInfo> GetInfo();
    }
}
