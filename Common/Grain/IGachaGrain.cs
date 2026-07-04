namespace Common
{
    public interface IGachaGrain : IGrainWithStringKey
    {
        Task<GachaResult> DrawAsync(string sessionId, int count);
        Task<GachaState> GetPityInfoAsync(string sessionId);
        Task<IReadOnlyList<GachaHistoryItem>> GetHistoryAsync(string sessionId, int limit);
        Task<IReadOnlyList<PlayerCharacter>> GetCharactersAsync(string sessionId);
    }
}
