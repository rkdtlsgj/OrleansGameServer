namespace Common
{
    public interface IMatchmakingQueueGrain : IGrainWithStringKey
    {
        Task Enqueue(string nickname, IMatchObserver observer);
        Task Cancel(string nickname);        
    }
}
