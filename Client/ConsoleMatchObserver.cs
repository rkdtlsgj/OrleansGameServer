using Common;

public class ConsoleMatchObserver : IMatchObserver
{
    public void System(string text)
    {
        Console.WriteLine("text");
    }

    public void Queued(string queueKey, int waitingCount)
    {
        Console.WriteLine($"[queue] {queueKey} 대기자: {waitingCount}");
    }

    public void Matched(Guid matchId, string queueKey, string opponent)
    {
        Console.WriteLine($" matchId={matchId} / 상대={opponent}");
    }
}