namespace Common
{
    public  interface IWalletGrain : IGrainWithStringKey
    {
        Task AddGemAsync(int paidGem, int freeGem);
        Task<PlayerWallet> GetWalletAsync();
        Task<bool> SpendGemAsync(int amount);
    }
}
