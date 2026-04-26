namespace Common
{
    public interface IGachaGrain : IGrainWithStringKey
    {
        Task<GachaResult> DrawAsync(int count);
        Task<GachaState> GetPityInfoAsync();
    }
}