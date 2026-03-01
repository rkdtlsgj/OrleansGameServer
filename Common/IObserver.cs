using Orleans;

namespace Common
{
    public interface IMatchObserver : IGrainObserver
    {
        void System(string text);
        void Queued(string key, int count);
        void Matched(Guid matchId, string key, string opponent);
    }
}
