# Orleans Game Server

Microsoft Orleans 기반의 게임 서버 학습 프로젝트입니다.

유저 로그인, 세션 관리, 매칭, 매칭 결과 저장, 지갑, 가챠 콘텐츠를 Grain 단위로 분리해 구현했습니다.<br>
게임 서버에서 자주 다루는 상태 관리, 비동기 처리, Redis 캐시, PostgreSQL 저장소를 작은 규모로 실험하는 것을 목표로 합니다.

# Orleans 학습정리
https://blog.naver.com/rkdtlsgj/224298880418

# 서버 구조

<img width="1707" height="986" alt="image" src="https://github.com/user-attachments/assets/9c6abaaa-22a2-4a5e-abc0-b677c5157207" />


# 환경
* .NET 10.0
* Microsoft Orleans
* C# async/await
* PostgreSQL
* Redis

# 실행 방법
<details>
<summary>실행 사전 준비</summary>
    
## 1. 사전 준비
* .NET 10.0 SDK
* 로컬 또는 원격 PostgreSQL 인스턴스
* 로컬 또는 원격 Redis 인스턴스

## 2. PostgreSQL 데이터베이스 및 테이블 생성

`OrleansMatchingServer/appsettings.json`의 기본값은 `matching`이라는 이름의 DB를 가리킵니다. 먼저 DB를 만들고, 아래 테이블을 생성합니다.

```sql
create database matching;

create table user_info (
    user_id text primary key,
    password_hash text not null,
    created_time timestamptz not null
);

create table match_history (
    match_id uuid primary key,
    channel text not null,
    player1 text not null,
    player2 text not null,
    created_at timestamptz not null
);

create table character_info (
    card_id uuid primary key,
    name text not null,
    rarity text not null
);

create table gacha_probability (
    rarity text primary key,
    probability numeric not null
);

create table player_wallet (
    user_id text primary key,
    paid_gem integer not null default 0 check (paid_gem >= 0),
    free_gem integer not null default 0 check (free_gem >= 0),
    updated_at timestamptz not null
);

create table gacha_user_state (
    user_id text primary key,
    pity_point integer not null default 0 check (pity_point >= 0),
    updated_at timestamptz not null
);

create table gacha_history (
    draw_id uuid primary key,
    user_id text not null,
    card_id text not null,
    name text not null,
    rarity text not null,
    obtained_at timestamptz not null,
    pity_point_after integer not null check (pity_point_after >= 0),
    is_pity boolean not null default false
);

create table player_character (
    user_id text not null,
    card_id text not null,
    name text not null,
    rarity text not null,
    count integer not null default 1,
    first_obtained_at timestamptz not null,
    last_obtained_at timestamptz not null,
    primary key (user_id, card_id)
);
```

`character_info`와 `gacha_probability`에는 가챠 동작을 위한 테스트 데이터가 최소 1개 이상 들어 있어야 합니다. 예시:

```sql
insert into gacha_probability (rarity, probability) values
    ('N', 0.7),
    ('SR', 0.25),
    ('SSR', 0.05);

insert into character_info (card_id, name, rarity) values
    (gen_random_uuid(), '테스트 캐릭터 N', 'N'),
    (gen_random_uuid(), '테스트 캐릭터 SR', 'SR'),
    (gen_random_uuid(), '테스트 캐릭터 SSR', 'SSR');
```

## 3. 연결 정보 설정

`OrleansMatchingServer/appsettings.json`에서 PostgreSQL과 Redis 연결 문자열을 환경에 맞게 수정합니다.

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=matching;Username=postgres;Password=1234",
    "Redis": "localhost:6379"
  }
}
```

## 4. 서버 실행

```bash
cd OrleansMatchingServer
dotnet run
```

## 5. 클라이언트 실행

서버가 기동된 상태에서 별도 터미널로 실행합니다.

```bash
cd Client
dotnet run
```

클라이언트에서는 회원가입과 로그인 후 매칭, 가챠, 젬 충전 메뉴를 사용할 수 있습니다. 매칭은 같은 `dice` 채널에 2명 이상 접속하면 1분 주기 타이머를 통해 진행됩니다.
</details>

# 주요 기능
로그인 / 회원가입
* 유저 ID를 Grain key로 사용합니다.
* 회원가입 시 비밀번호 해시와 생성 시간을 DB에 저장합니다.
* 로그인 시 DB에서 유저 정보를 조회하고 비밀번호를 검증합니다.
* 로그인 성공 시 Redis에 24시간 유효한 세션을 저장합니다.

매칭
* 채널 이름을 Grain key로 사용합니다.
* 현재 클라이언트는 `dice` 채널에 입장합니다.
* 같은 채널에서 대기 중인 유저를 주기적으로 2명씩 매칭합니다.
* 매칭 성공 시 양쪽 클라이언트에 Observer로 결과를 알립니다.
* Redis에는 채널별 대기 유저와 유저별 현재 채널을 캐싱합니다.
* PostgreSQL에는 매칭 완료 기록을 저장합니다.

지갑
* 유저 ID를 Grain key로 사용합니다.
* 유료젬과 무료젬을 관리합니다.
* 재화는 PostgreSQL의 `player_wallet` 테이블에 저장합니다.
* 재화 사용 시 무료젬을 먼저 차감하고, 부족한 분량을 유료젬에서 차감합니다.

가챠
* 유저 ID를 Grain key로 사용합니다.
* 1회 뽑기와 10회 뽑기를 지원합니다.
* 뽑기 실행 시 `WalletGrain`을 통해 재화를 차감합니다.
* 90회째 SSR 천장을 지원하고, SSR 획득 시 천장 포인트를 초기화합니다.
* 가챠 이력은 `gacha_history`에 저장합니다.
* 보유 캐릭터는 `player_character`에 저장하고, 중복 획득 시 count를 증가시킵니다.
* 뽑기 결과와 남은 재화를 클라이언트에 반환합니다.
* `character_info` 테이블을 이용해 캐릭터 정보를 조회합니다.

로그
* ILogger를 이용하여 주요 서버 이벤트를 기록합니다.


# 테스트

<details>
<summary>결과 보기</summary>
<img width="654" height="231" alt="image" src="https://github.com/user-attachments/assets/4e34b00c-5285-4b0e-8626-8b2943222524" /><br>
<img width="273" height="158" alt="image" src="https://github.com/user-attachments/assets/764da523-4f1b-4c05-ae15-fc0b75f1b49c" /><br>
타이머를 통해 2명씩 매칭되고, 남은 유저는 계속 기다리는 형태로 동작합니다.<br><br>

<img width="588" height="298" alt="Image" src="https://github.com/user-attachments/assets/3a887b01-31d8-45a4-b5a9-92e5491752bf" /><br>
<img width="715" height="151" alt="Image" src="https://github.com/user-attachments/assets/294db437-db34-410c-80c2-7be9eed879ab" /><br>
SQL에 매칭 완료 이력 저장<br>
Redis에 채널별 대기 유저 확인<br><br>

<img width="1045" height="295" alt="image" src="https://github.com/user-attachments/assets/ef5684a0-9322-4ff7-b72a-4b86ffd48f97" /><br>
유저 정보 DB 저장 추가<br><br>

<img width="256" height="431" alt="image" src="https://github.com/user-attachments/assets/88e55fd8-cb69-4b68-8aba-83f9b7263ebc" />
<img width="430" height="271" alt="image" src="https://github.com/user-attachments/assets/ee3c6c48-2522-4e75-aedd-f06833c17926" /><br>
가챠 시스템 구현<br><br>

User 200명 가챠 테스트<br>
User당 10회뽑기 100회 요청<br>
등급 / 획득수 / 실제비율 / 기대값 / 차이<br>
<img width="320" height="57" alt="image" src="https://github.com/user-attachments/assets/99b03286-7905-4891-a287-8dd54a7b075b" /><br>
천장이 보정되어서 가차확률에 차이는 있지만 거의 일치 확인<br>
평균 요청 avgMs=87 maxMs=1116<br>
maxMs 요청이 너무 오래걸려서 확인해본 결과 지갑 차감쪽에서 요청이 느리다는 걸 로그로 확인했고 수정 예정<br>


</details>
