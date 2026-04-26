using Common;
using Microsoft.CodeAnalysis.Operations;
using Orleans.Runtime;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace OrleansMatchingServer
{
    public class LoginGrain : Grain, ILoginGrain
    {
        private readonly IPersistentState<PlayerState> _state;
        private readonly IConnectionMultiplexer _redis;

        public LoginGrain([PersistentState("player", "playerStore")]IPersistentState<PlayerState> state, IConnectionMultiplexer redis)
        {
            _state = state;
            _redis = redis;
        }


        public async Task<bool> RegisterAsync(string psw)
        {
            if (_state.RecordExists) // 가입된 유저인가???
                return false;

            //추후 DB에 저장하는 것도 추가해야함

            _state.State = new PlayerState
            {
                UserId = this.GetPrimaryKeyString(),
                PasswordHash = HashPassword(psw),
                CreatedTime = DateTimeOffset.Now
            };

            await _state.WriteStateAsync();
            return true;
        }


        public async Task<string?> LoginAsync(string psw)
        {
            if (_state.RecordExists == false)
                return null;


            if (_state.State.PasswordHash != HashPassword(psw)) // 비밀번호 체크
                return null;

            var sessionId = Guid.NewGuid().ToString(); // 테스트용 guid
            var db = _redis.GetDatabase();

            await db.StringSetAsync($"session:{sessionId}", _state.State.UserId, TimeSpan.FromHours(24));

            return sessionId;
        }



        //비밀번호
        private static string HashPassword(string psw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(psw));
            return Convert.ToHexString(bytes);
        }
    }
}