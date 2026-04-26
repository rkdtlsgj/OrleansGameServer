namespace Common
{
    public interface ILoginGrain : IGrainWithStringKey
    {
        Task<bool> RegisterAsync(string psw);
        Task<string?> LoginAsync(string psw);
    }
}
