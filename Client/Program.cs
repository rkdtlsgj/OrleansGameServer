using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client => client.UseLocalhostClustering())
    .Build();

await host.StartAsync();
var client = host.Services.GetRequiredService<IClusterClient>();

string? sessionId = null;
string? loggedInUser = null;


var observer = new ConsoleMatchObserver();
var observerRef = client.CreateObjectReference<IMatchObserver>(observer);

while (sessionId == null)
{
    Console.WriteLine("=== 로그인 ===");
    Console.Write("1. 로그인  2. 회원가입 선택: ");
    var choice = Console.ReadLine();

    Console.Write("아이디: ");
    var userId = Console.ReadLine()!;
    Console.Write("비밀번호: ");
    var password = Console.ReadLine()!;

    var loginGrain = client.GetGrain<ILoginGrain>(userId);

    if (choice == "2")
    {
        var ok = await loginGrain.RegisterAsync(password);
        Console.WriteLine(ok ? "회원가입 성공!" : "회원가입 실패");
        continue;
    }

    sessionId = await loginGrain.LoginAsync(password);
    if (sessionId == null)
    {
        Console.WriteLine("아이디 또는 비밀번호가 틀렸습니다.\n");
        continue;
    }

    loggedInUser = userId;

}

// 로비
while (true)
{
    Console.WriteLine("=== 로비 ===");
    Console.WriteLine("1. 매칭");
    Console.WriteLine("2. 가챠");
    Console.WriteLine("3. 젬 충전");
    Console.WriteLine("0. 종료");
    Console.Write("선택: ");

    var menu = Console.ReadLine();

    switch (menu)
    {
        // 매칭
        case "1":
            Console.WriteLine("\n매칭을 시작합니다...");
            var queueGrain = client.GetGrain<IMatchmakingQueueGrain>("dice");
            await queueGrain.Enqueue(loggedInUser!, observerRef);
            Console.WriteLine("매칭 대기 중... (아무 키나 누르면 취소)");
            Console.ReadKey();
            await queueGrain.Cancel(loggedInUser!);
            Console.WriteLine("매칭 취소됨\n");
            break;

        // 가챠
        case "2":
            await RunGachaMenu(client, loggedInUser!);
            break;

        // 충전
        case "3":
            var walletGrain = client.GetGrain<IWalletGrain>(loggedInUser!);
            await walletGrain.AddGemAsync(paidGem: 0, freeGem: 3200);
            var wallet = await walletGrain.GetWalletAsync();
            Console.WriteLine($"충전 완료! 유료젬: {wallet.PaidGem}  무료젬: {wallet.FreeGem}\n");
            break;

        // 종료
        case "0":
            Console.WriteLine("게임을 종료합니다.");
            await host.StopAsync();
            return;

        default:
            Console.WriteLine("잘못된 입력입니다.\n");
            break;
    }
}

// 가챠
async Task RunGachaMenu(IClusterClient clusterClient, string userId)
{
    var gachaGrain = clusterClient.GetGrain<IGachaGrain>(userId);
    var walletGrain = clusterClient.GetGrain<IWalletGrain>(userId);

    while (true)
    {
        var wallet = await walletGrain.GetWalletAsync();
        var pity = await gachaGrain.GetPityInfoAsync();

        Console.WriteLine("\n=== 가챠 ===");
        Console.WriteLine($"유료젬: {wallet.PaidGem}  무료젬: {wallet.FreeGem}");
        Console.WriteLine($"포인트: {pity.PityPoint}");
        Console.WriteLine("1. 1연챠 (160젬)");
        Console.WriteLine("2. 10연챠 (1600젬)");
        Console.WriteLine("0. 로비로 돌아가기");
        Console.Write("선택: ");

        var input = Console.ReadLine();
        if (input == "0") break;

        int count = input == "1" ? 1 : input == "2" ? 10 : 0;
        if (count == 0)
        {
            Console.WriteLine("잘못된 입력입니다.");
            continue;
        }

        try
        {
            var result = await gachaGrain.DrawAsync(count);

            Console.WriteLine("\n── 뽑기 결과 ──");
            foreach (var card in result.Cards)
            {
                var star = card.Rarity == "SSR" ? " ★★★" :
                           card.Rarity == "SR" ? " ★★" : "";
                Console.WriteLine($"  [{card.Rarity}] {card.Name}{star}");
            }

            Console.WriteLine($"포인트: {result.PityPoint}");
            Console.WriteLine($"남은 유료젬: {result.PaidGem}");
            Console.WriteLine($"남은 무료젬: {result.FreeGem}\n");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"\n{ex.Message}\n");
        }
    }
}