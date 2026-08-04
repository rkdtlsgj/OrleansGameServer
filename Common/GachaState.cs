
namespace Common
{
    [GenerateSerializer]
    public class PlayerWallet
    {
        [Id(0)] public int PaidGem { get; set; } // 유료 재화
        [Id(1)] public int FreeGem { get; set; } // 무료 재화
    }

    [GenerateSerializer]
    public class SpendGemResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public int PaidGemUsed { get; set; }
        [Id(2)] public int FreeGemUsed { get; set; }
        [Id(3)] public int PaidGem { get; set; }
        [Id(4)] public int FreeGem { get; set; }
    }

    [GenerateSerializer]
    public class Card // 캐릭터
    {
        [Id(0)] public string CardId { get; set; } = "";
        [Id(1)] public string Name { get; set; } = "";
        [Id(2)] public string Rarity { get; set; } = "";
        [Id(3)] public DateTimeOffset ObtaiendAt { get; set; }
        [Id(4)] public bool IsPity { get; set; }
        [Id(5)] public int PityPointAfter { get; set; }
    }

    [GenerateSerializer]
    public class GachaResult
    {
        [Id(0)] public List<Card> Cards { get; set; } = new();
        [Id(1)] public int PityPoint { get; set; }
        [Id(2)] public int PaidGem {get;set;}
        [Id(3)] public int FreeGem {get;set;}
    }

    [GenerateSerializer]
    public class GachaHistoryItem
    {
        [Id(0)] public Guid DrawId { get; set; }
        [Id(1)] public string CardId { get; set; } = "";
        [Id(2)] public string Name { get; set; } = "";
        [Id(3)] public string Rarity { get; set; } = "";
        [Id(4)] public DateTimeOffset ObtainedAt { get; set; }
        [Id(5)] public int PityPointAfter { get; set; }
        [Id(6)] public bool IsPity { get; set; }
    }

    [GenerateSerializer]
    public class PlayerCharacter
    {
        [Id(0)] public string CardId { get; set; } = "";
        [Id(1)] public string Name { get; set; } = "";
        [Id(2)] public string Rarity { get; set; } = "";
        [Id(3)] public int Count { get; set; }
        [Id(4)] public DateTimeOffset FirstObtainedAt { get; set; }
        [Id(5)] public DateTimeOffset LastObtainedAt { get; set; }
    }
}
