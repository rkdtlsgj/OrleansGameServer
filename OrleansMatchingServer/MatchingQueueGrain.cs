using Common;
using Orleans;

public class MatchingQueueGrain : Grain, IMatchmakingQueueGrain
{
    private readonly MatchHistoryRepository _historyRepository;
    private readonly QueueCacheRepository _queueCacheRepository;

    private Dictionary<string, IMatchObserver> _waiting = new Dictionary<string, IMatchObserver>();
    private Queue<string> _order = new Queue<string>();

    private IDisposable _timer;


    //테스트용 매칭 대기시간
    private static readonly TimeSpan MatchInterval = TimeSpan.FromMinutes(1);


    public MatchingQueueGrain(MatchHistoryRepository historyRepository, QueueCacheRepository queueCacheRepository)
    {
        _historyRepository = historyRepository;
        _queueCacheRepository = queueCacheRepository;
    }

    //Unity의 Awake같은 개념
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 주기 실행 타이머 등록
        _timer = this.RegisterGrainTimer(
            callback: (state, ct) => RunMatching(),  
            state: 0,
            options: new GrainTimerCreationOptions
            {
                DueTime = MatchInterval,
                Period = MatchInterval,
                Interleave = false,
                KeepAlive = true
            });

        return Task.CompletedTask;
    }

    //Unity의 Destroy같은 개념
    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer.Dispose();        
        return Task.CompletedTask;
    }

    public async Task Enqueue(string nickname, IMatchObserver observer)
    {
        var key = this.GetPrimaryKeyString();

        //중복방지
        if (_waiting.ContainsKey(nickname))
        {
            //갱신만 하도록 수정
            _waiting[nickname] = observer;
            Queued(observer);

            return;
        }

        _waiting[nickname] = observer;
        _order.Enqueue(nickname);

        await _queueCacheRepository.AddToQueueAsync(key, nickname);

        BroadcastSystem("대기열 참가!");
        BroadcastQueued();

        return;
    }

    public async Task Cancel(string nickname)
    {
        var key = this.GetPrimaryKeyString();

        if (_waiting.Remove(nickname))
        {
            await _queueCacheRepository.RemoveFromQueueAsync(key, nickname);

            BroadcastSystem("취소!");
            BroadcastQueued();
        }

        return;
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

    private bool TryDequeue(out string nickname)
    {
        while (_order.Count > 0)
        {
            var nick = _order.Dequeue();
            if (_waiting.ContainsKey(nick))
            {
                nickname = nick;
                return true;
            }
        }

        nickname = "";
        return false;
    }


    private async Task RunMatching()
    {
        var key = this.GetPrimaryKeyString();

        BroadcastSystem($"[매칭중] 대기인원 : {_waiting.Count}");

        while (TryDequeue(out var p1) && TryDequeue(out var p2))
        {
            var obs1 = _waiting[p1];
            var obs2 = _waiting[p2];


            //대기열에서 삭제
            _waiting.Remove(p1);
            _waiting.Remove(p2);

            await _queueCacheRepository.RemoveFromQueueAsync(key, p1);
            await _queueCacheRepository.RemoveFromQueueAsync(key, p2);

            var matchId = Guid.NewGuid(); //아이디 생성
            var match = GrainFactory.GetGrain<IMatchGrain>(matchId);
            var createdAt = DateTimeOffset.UtcNow;

            await match.Initialize(key, p1, p2);


            //매칭 기록
            await _historyRepository.SaveMatchAsync(matchId, key, p1, p2, createdAt);

            NotiMatchComplete(obs1, matchId, key, p2);
            NotiMatchComplete(obs2, matchId, key, p1);            
        }

        BroadcastQueued();

        return;
    }

    private void Queued(IMatchObserver obs)
    {
        obs.Queued(this.GetPrimaryKeyString(), _waiting.Count);
    }

    private static void NotiMatchComplete(IMatchObserver obs, Guid matchId, string key, string opponent)
    {
        obs.Matched(matchId, key, opponent);
    }


}