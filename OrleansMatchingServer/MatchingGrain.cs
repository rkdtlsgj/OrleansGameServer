
using Common;

namespace OrleansMatchingServer
{
    public class MatchingGrain : Grain, IMatchGrain
    {
        private MatchInfo _info;

        public Task Initialize(string key, string player1, string player2)
        {
            var id = this.GetPrimaryKey();
            _info = new MatchInfo(id, key, player1, player2, DateTimeOffset.Now);

            return Task.CompletedTask;
        }
        public Task<MatchInfo> GetInfo()
        {
            return Task.FromResult(_info!);
        }
    }
}
