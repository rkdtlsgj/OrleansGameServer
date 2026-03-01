using Common;
using Orleans;

public class MatchingQueueGrain : Grain, IMatchmakingQueueGrain
{
    private readonly Dictionary<string, IMatchObserver> _waiting = new Dictionary<string, IMatchObserver>();

    public async Task Enqueue(string nickname, IMatchObserver observer)
    {
        _waiting[nickname] = observer;

        BroadcastQueued();

        //두명이상 이면 매칭 스타트!
        if (_waiting.Count >= 2)
        {
            var key = this.GetPrimaryKeyString();


            var p1 = _waiting.Keys.First();
            var p2 = _waiting.Keys.Skip(1).First();


            var obs1 = _waiting[p1];
            var obs2 = _waiting[p2];

            //대기열에서 삭제
            _waiting.Remove(p1);
            _waiting.Remove(p2);

            var matchId = Guid.NewGuid(); //아이디 생성
            var match = GrainFactory.GetGrain<IMatchGrain>(matchId);

            await match.Initialize(key, p1, p2);

            NotiMatchComplete(obs1, matchId, key, p2);
            NotiMatchComplete(obs2, matchId, key, p1);            

            await match.GetInfo();
        }
    }

    public Task Cancel(string nickname)
    {
        if (_waiting.Remove(nickname))
        {
            BroadcastSystem("취소!");
            BroadcastQueued();
        }

        return Task.CompletedTask;
    }


    private void BroadcastQueued()
    {
        var key = this.GetPrimaryKeyString();
        var count = _waiting.Count;

        foreach (var obs in _waiting.Values)
        {
            obs.Queued(key, count);
        }
    }

    private void BroadcastSystem(string text)
    {
        foreach (var obs in _waiting.Values)
        {
            obs.System(text);
        }
    }

    private static void NotiMatchComplete(IMatchObserver obs, Guid matchId, string key, string opponent)
    {
        obs.Matched(matchId, key, opponent);
    }


}