namespace Common
{
    [GenerateSerializer]
    public class PlayerState
    {
        [Id(0)] public string UserId { get; set; }
        [Id(1)] public string PasswordHash { get; set; }
        [Id(2)] public DateTimeOffset CreatedTime { get; set; }

    }
}