
namespace Common
{
    [GenerateSerializer]
    public class GachaState
    {
        [Id(0)] public int PityPoint { get; set; } // 포인트 체크
    }

    [GenerateSerializer]
    public class PlayerWallet
    {
        [Id(0)] public int PaidGem { get; set; } // 유료 재화
        [Id(1)] public int FreeGem { get; set; } // 무료 재화
    }

    [GenerateSerializer]
    public class Card // 캐릭터
    {
        [Id(0)] public string CardId { get; set; }
        [Id(1)] public string Name { get; set; }
        [Id(2)] public string Rarity { get; set; }
        [Id(3)] public DateTimeOffset ObtaiendAt { get; set; }
    }

    [GenerateSerializer]
    public class GachaResult
    {
        [Id(0)] public List<Card> Cards { get; set; }
        [Id(1)] public int PityPoint { get; set; }
        [Id(2)] public int PaidGem {get;set;}
        [Id(3)] public int FreeGem {get;set;}
    }
}
