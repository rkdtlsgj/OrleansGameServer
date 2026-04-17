
using Common;

namespace OrleansMatchingServer
{
    public class MatchingGrain : Grain, IMatchGrain
    {        
        private IPersistentState<MatchInfo> _info;


        public MatchingGrain([PersistentState("match","matchStore")]IPersistentState<MatchInfo> info)
        {
            _info = info;
        }

        public async Task Initialize(string key, string player1, string player2)
        {
            //이미 초기화했는가?
            if (_info.RecordExists)
                return;

            var id = this.GetPrimaryKey();
            _info.State = new MatchInfo(id, key, player1, player2, DateTimeOffset.Now);

            await _info.WriteStateAsync();
        }
        public Task<MatchInfo> GetInfo()
        {
            if (_info.RecordExists == false)
                throw new InvalidOperationException("초기화 안되어있다");

            return Task.FromResult(_info.State);
        }
    }
}
