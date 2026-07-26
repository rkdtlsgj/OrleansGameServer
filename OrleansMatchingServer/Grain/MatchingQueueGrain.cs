using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using OrleansMatchingServer;

public class MatchingQueueGrain : Grain, IMatchmakingQueueGrain
{
    //테스트용 매칭 대기시간. 부하 테스트에서는 MatchIntervalSeconds로 줄여서 측정합니다.
    private static readonly TimeSpan DefaultMatchInterval = TimeSpan.FromMinutes(1);

    private readonly TimeSpan _matchInterval;

    private readonly MatchHistoryRepository _historyRepository;
    private readonly QueueCacheRepository _queueCacheRepository;
    private readonly SessionRepository _sessionRepository;
    private readonly ILogger<MatchingQueueGrain> _logger;

    private readonly Dictionary<string, IMatchObserver> _waiting = new();
    private readonly Queue<string> _order = new();

    private IDisposable? _timer;

    private readonly ILocalSiloDetails _localSiloDetails;

    public MatchingQueueGrain(
        MatchHistoryRepository historyRepository,
        QueueCacheRepository queueCacheRepository,
        SessionRepository sessionRepository,
        ILogger<MatchingQueueGrain> logger,
        ILocalSiloDetails localSiloDetails,
        IConfiguration configuration)
    {
        _historyRepository = historyRepository;
        _queueCacheRepository = queueCacheRepository;
        _sessionRepository = sessionRepository;
        _logger = logger;
        _localSiloDetails = localSiloDetails;

        var intervalSeconds = configuration.GetValue("MatchIntervalSeconds", 0);
        _matchInterval = intervalSeconds > 0
            ? TimeSpan.FromSeconds(intervalSeconds)
            : DefaultMatchInterval;
    }

    //Unity의 Awake같은 개념
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Silo 체크 Silo={Silo}",
            _localSiloDetails.SiloAddress);

        // 주기 실행 타이머 등록
        _timer = this.RegisterGrainTimer(
            callback: (state, ct) => RunMatching(),
            state: 0,
            options: new GrainTimerCreationOptions
            {
                DueTime = _matchInterval,
                Period = _matchInterval,
                Interleave = false,
                KeepAlive = true
            });

        return Task.CompletedTask;
    }

    //Unity의 Destroy같은 개념
    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();

        return Task.CompletedTask;
    }

    public async Task Enqueue(string sessionId, IMatchObserver observer)
    {
        var key = this.GetPrimaryKeyString();
        var nickname = await _sessionRepository.GetRequiredUserIdAsync(sessionId);

        //중복방지
        if (_waiting.ContainsKey(nickname))
        {
            //갱신만 하도록 수정
            _waiting[nickname] = observer;
            Queued(observer);

            _logger.LogInformation(
                "대기열 등록. Channel={Channel}, UserId={UserId}, WaitingCount={WaitingCount}",
                key,
                nickname,
                _waiting.Count);

            return;
        }

        _waiting[nickname] = observer;
        _order.Enqueue(nickname);

        await _queueCacheRepository.AddToQueueAsync(key, nickname);

        _logger.LogInformation(
            "대기열 참가. Channel={Channel}, UserId={UserId}, WaitingCount={WaitingCount}",
            key,
            nickname,
            _waiting.Count);

        BroadcastSystem("대기열 참가!");
        BroadcastQueued();
    }

    public async Task Cancel(string sessionId)
    {
        var key = this.GetPrimaryKeyString();
        var nickname = await _sessionRepository.GetRequiredUserIdAsync(sessionId);

        if (_waiting.Remove(nickname))
        {
            await _queueCacheRepository.RemoveFromQueueAsync(key, nickname);

            _logger.LogInformation(
                "매칭 취소. Channel={Channel}, UserId={UserId}, WaitingCount={WaitingCount}",
                key,
                nickname,
                _waiting.Count);

            BroadcastSystem("취소!");
            BroadcastQueued();
        }
        else
        {
            _logger.LogDebug(
                "대기열에 존재하지 않음. Channel={Channel}, UserId={UserId}",
                key,
                nickname);
        }
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

        _logger.LogInformation(
            "매칭 시작. Channel={Channel}, WaitingCount={WaitingCount}",
            key,
            _waiting.Count);

        while (TryDequeue(out var p1) && TryDequeue(out var p2))
        {
            var obs1 = _waiting[p1];
            var obs2 = _waiting[p2];

            //대기열에서 삭제
            _waiting.Remove(p1);
            _waiting.Remove(p2);

            await _queueCacheRepository.RemoveFromQueueAsync(key, p1);
            await _queueCacheRepository.RemoveFromQueueAsync(key, p2);

            var matchId = Guid.NewGuid();
            var match = GrainFactory.GetGrain<IMatchGrain>(matchId);
            var createdAt = DateTimeOffset.UtcNow;

            await match.Initialize(key, p1, p2);

            //매칭 기록
            await _historyRepository.SaveMatchAsync(matchId, key, p1, p2, createdAt);

            _logger.LogInformation(
                "매칭 완료. MatchId={MatchId}, Channel={Channel}, Player1={Player1}, Player2={Player2}",
                matchId,
                key,
                p1,
                p2);

            NotiMatchComplete(obs1, matchId, key, p2);
            NotiMatchComplete(obs2, matchId, key, p1);
        }

        _logger.LogInformation(
            "매칭 종료. Channel={Channel}, RemainingWaitingCount={WaitingCount}",
            key,
            _waiting.Count);

        BroadcastQueued();
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
