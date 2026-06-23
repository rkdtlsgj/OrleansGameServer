namespace Common
{
    public interface IGachaGrain : IGrainWithStringKey
    {
        Task<GachaResult> DrawAsync(string sessionId, int count);
        Task<GachaState> GetPityInfoAsync(string sessionId);
    }
}
