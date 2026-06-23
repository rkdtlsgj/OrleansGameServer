namespace Common
{
    public  interface IWalletGrain : IGrainWithStringKey
    {
        Task AddGemAsync(string sessionId, int paidGem, int freeGem);
        Task<PlayerWallet> GetWalletAsync(string sessionId);
        Task<SpendGemResult> SpendGemAsync(string sessionId, int amount);
    }
}
