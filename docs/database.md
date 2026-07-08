# 데이터베이스 구조

PostgreSQL은 유저, 재화, 가챠 기록<br>
Redis는 세션, 매칭 대기열을 담당합니다.

## PostgreSQL 테이블

| 테이블 | 용도 | 사용하는 곳 |
|---|---|---|
| `user_info` | 계정 정보 (비밀번호 해시, 생성 시간) | LoginGrain / UserRepository |
| `match_history` | 매칭 완료 기록 | MatchHistoryRepository |
| `character_info` | 가챠로 뽑을 수 있는 캐릭터 마스터 데이터 | GachaDataRepository |
| `gacha_probability` | 등급별 가챠 확률 | GachaDataRepository |
| `player_wallet` | 유저별 유료젬/무료젬 잔액 | WalletGrain / WalletRepository |
| `gacha_user_state` | 유저별 천장 포인트 | GachaGrain / GachaHistoryRepository |
| `gacha_history` | 뽑기 1회당 1행의 가챠 이력 | GachaHistoryRepository |
| `player_character` | 유저별 보유 캐릭터 (중복 획득 시 count 증가) | GachaHistoryRepository |

    user_info {
        text user_id PK
        text password_hash
        timestamptz created_time
    }

    match_history {
        uuid match_id PK
        text channel
        text player1
        text player2
        timestamptz created_at
    }

    character_info {
        uuid card_id PK
        text name
        text rarity
    }

    gacha_probability {
        text rarity PK
        numeric probability
    }

    player_wallet {
        text user_id PK
        integer paid_gem "0 이상"
        integer free_gem "0 이상"
        timestamptz updated_at
    }

    gacha_user_state {
        text user_id PK
        integer pity_point "0 이상"
        timestamptz updated_at
    }

    gacha_history {
        uuid draw_id PK
        text user_id
        text card_id
        text name
        text rarity
        timestamptz obtained_at
        integer pity_point_after "0 이상"
        boolean is_pity
    }

    player_character {
        text user_id PK "복합 PK"
        text card_id PK "복합 PK"
        text name
        text rarity
        integer count "기본값 1"
        timestamptz first_obtained_at
        timestamptz last_obtained_at
    }


## 데이터 흐름

* 가챠 뽑기 1번에 `player_wallet` 차감 → `gacha_user_state` 갱신 → `gacha_history` 기록(COPY) → `player_character` upsert가 **하나의 트랜잭션**으로 처리됩니다.
* `gacha_history`는 뽑기 1회당 1행씩 계속 쌓이는 이력 테이블이고, `player_character`는 유저-캐릭터당 1행을 유지하며 `count`만 증가하는 집계 테이블입니다.
* `character_info`와 `gacha_probability`는 서버가 읽기만 하는 데이터입니다.

## Redis 키

| 키 | 타입 | 용도 |
|---|---|---|
| `session:{sessionId}` | string | 세션 → 유저 ID 매핑, 24시간 만료 |
| `channel:{channel}:members` | set | 채널별 매칭 대기 유저 목록 |
| `user:{nickname}:channel` | string | 유저가 현재 대기 중인 채널 |
