namespace Common
{
    public interface IMatchmakingQueueGrain : IGrainWithStringKey
    {
        Task Enqueue(string sessionId, IMatchObserver observer);
        Task Cancel(string sessionId);        
    }
}
