# OrleansGameServer

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](#)
[![Orleans](https://img.shields.io/badge/Microsoft-Orleans-0078D4)](#)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)](#)
[![Redis](https://img.shields.io/badge/Redis-DC382D?logo=redis&logoColor=white)](#)
[![Blog](https://img.shields.io/badge/Tech%20Blog-Orleans%20%EC%8A%A4%ED%84%B0%EB%94%94-orange)](https://blog.naver.com/rkdtlsgj/224298880418)

.NET / Microsoft Orleans 기반으로 로그인 매치메이킹 가챠 지갑 시스템을 구현한 게임 서버입니다.

가상 액터(Grain)의 턴 기반 실행 모델이 게임 서버의 동시성 정합성 문제를 어떻게 해결하는지 검증하는 것이 목표입니다.

📝 Orleans 학습 정리: https://blog.naver.com/rkdtlsgj/224298880418


---

## 목차

| # | 챕터 | 내용 |
|:---:|------|------|
| **01** | [아키텍처](#01-아키텍처) | 시스템 구성  기술 스택  주요 기여  코드 리딩 가이드 |
| **02** | [Grain 동시성 설계](#02-grain-동시성-설계) | 턴 기반 실행  타이머 인터리빙 제어  단일 트랜잭션 |
| **03** | [데이터 계층 — 가챠·지갑 정합성](#03-데이터-계층--가챠지갑-정합성) | 유/무료 재화 분리  COPY  Batch Upsert  부하 테스트 |
| **04** | [분산 — 무엇이 보장되고 무엇이 아닌가](#04-분산--무엇이-보장되고-무엇이-아닌가) | 배치 분포  장애 조치  스케일아웃 실측 |
| **05** | [설계 변천사](#05-설계-변천사) | 대체 기각된 설계와 그 근거  인지하고 있는 한계 |
| **06** | [설계 배경](#06-설계-배경) | 설계 동기  IOCP와 비교  트러블슈팅 사례 |

---

## 01 아키텍처
```
Client (콘솔 · Orleans Client)
  │ Grain 호출                          ▲ Observer 콜백 (매칭 결과 통지)
  ▼                                     │
Orleans Silo ───────────────────────────┘
  ├─ LoginGrain           인증  Redis 세션 발급 (24h TTL)
  ├─ MatchingQueueGrain   채널별 대기열  GrainTimer 주기 매칭
  │      └─> MatchGrain   매치 인스턴스 생성  초기화
  ├─ WalletGrain          유료/무료 젬 분리 정산
  └─ GachaGrain           확률 추첨  90연차 천장  차감+저장 단일 트랜잭션
         │
    ┌────┴─────┐
    ▼          ▼
PostgreSQL    Redis
DB (COPY  batch upsert)    세션  매칭 대기열 캐시
```

### 기술 스택

| 분류 | 기술 |
|------|------|
| **Server** | .NET 10.0  Microsoft Orleans (Grains  GrainTimer  Observer) |
| **Concurrency** | 턴 기반 실행  타이머 `Interleave = false` 제어  단일 DB 트랜잭션  `FOR UPDATE` 행 잠금 |
| **Data** | PostgreSQL (Npgsql  Dapper  COPY  batch upsert)  Redis (StackExchange.Redis) |
| **Client** | .NET 콘솔 앱 (Orleans Client + Observer 패턴) |

### 주요 기여

| 항목 | 내용 |
|------|------|
| **재화 유실 차단** | 재화 차감과 뽑기 결과 저장을 하나의 DB 트랜잭션으로 통합 <br>부분 실패 시 재화만 차감되는 유실 시나리오를 구조적으로 제거 |
| **천장 시스템** | 90연차 SSR 확정 로직 + 임계값 상수 분리로 기획 데이터 변경에 유연한 구조 |
| **이력 기록 최적화** | 가챠 이력 N건을 PostgreSQL COPY(binary import)로, 보유 캐릭터를 `unnest` 기반 batch upsert로 단일 라운드트립 기록 <br>SaveDraw 138ms → 1.5ms |
| **매칭 파이프라인** | 채널 단위 Grain 대기열 → GrainTimer 주기 매칭 → Observer 비동기 통지 → 이력 기록 |
| **분산 효과 검증** | 사일로 1개 vs 2개를 가챠·매칭 두 워크로드로 실측해 **스케일아웃이 성립하는 조건**을 규명 <br>병목이 공유 DB에 있고 키가 흩어지지 않으면 사일로 추가가 오히려 손해임을 수치로 확인 |

### 코드 리딩 가이드

| 관심사 | 핵심 파일 |
|------|-----------|
| Grain 인터페이스 | [Common/Grain/](Common/Grain/) |
| 로그인 / 세션 | [LoginGrain.cs](OrleansMatchingServer/Grain/LoginGrain.cs) · [SessionRepository.cs](OrleansMatchingServer/SessionRepository.cs) |
| 매칭 대기열 / 타이머 | [MatchingQueueGrain.cs](OrleansMatchingServer/Grain/MatchingQueueGrain.cs) · [QueueCacheRepository.cs](OrleansMatchingServer/QueueCacheRepository.cs) |
| 가챠 / 단일 트랜잭션 | [GachaGrain.cs](OrleansMatchingServer/Grain/GachaGrain.cs) · [GachaHistoryRepository.cs](OrleansMatchingServer/GachaHistoryRepository.cs) |
| 지갑 | [WalletGrain.cs](OrleansMatchingServer/Grain/WalletGrain.cs) · [WalletRepository.cs](OrleansMatchingServer/WalletRepository.cs) |
| 클라이언트 / Observer | [Client/Program.cs](Client/Program.cs) · [ConsoleMatchObserver.cs](Client/ConsoleMatchObserver.cs) |
| 부하 테스트 하네스 | [GachaLoadTest/](GachaLoadTest/) · [MatchLoadTest/](MatchLoadTest/) · [LoadTestCommon/](LoadTestCommon/) |

---

## 02 Grain 동시성 설계

Orleans Grain은 한 번에 하나의 요청만 실행하므로 락 없이 상태를 보호할 수 있습니다.<br>
다만 C#의 async 메서드는 await 지점마다 상태머신으로 분할되어 실행되므로<br>타이머 콜백이나 await 지점의 인터리빙까지 자동으로 안전해지는 것은 아닙니다.<br>
그래서 아래와 같은 지점들은 명시적인 설계 판단이 필요했습니다.

| 주제 | 설계 판단 |
|------|-----------|
| **턴 기반 실행** | Grain 메서드는 한 번에 하나의 턴만 실행 — `GachaGrain`의 천장 포인트 필드 캐싱(`_pityPoint`)이 락 없이 안전한 근거 |
| **지갑 정합성 — <br>보장 계층의 분리** | Grain의 턴은 **메모리 상태**를 지키고, 지갑 잔액 같은 **DB 상태**는 `SELECT ... FOR UPDATE` 행 잠금 + 트랜잭션 내 잔액 검사로 지킴 <br>차감 경로가 `WalletGrain`과 `GachaGrain` 두 곳이어도 DB 계층에서 동시 차감이 직렬화되는 근거 |
| **타이머 인터리빙** | 매칭 타이머를 `Interleave = false`로 등록해 콜백도 일반 턴처럼 직렬화 <br> `Enqueue`/`Cancel`과 대기열 상태가 교차 실행되지 않음 |
| **`[Reentrant]` <br> 미사용** | 재진입을 허용하면 검사-후-행동(TOCTOU) 사이에 상태가 바뀔 수 있어, 기본값인 비-재진입을 유지 <br> 상태를 바꾸지 않는 조회 로그성 Grain에만 허용 가능하다고 판단 |
| **`ConfigureAwait(false)` <br> 미사용** | 턴 기반 보장은 Grain 전용 TaskScheduler가 제공 — `ConfigureAwait(false)`를 쓰면 await 이후 스레드 풀로 벗어나 싱글스레드 보장이 깨지므로 Grain 내부에서는 사용하지 않음 |
| **Timer vs Reminder** | 대기열은 활성화 기간에만 의미 있는 메모리 상태이므로 Reminder 대신 silo-local GrainTimer 선택<br>`KeepAlive = true`로 유휴 비활성화 방지 |
| **Grain 생명주기** | `GetGrain`은 참조만 반환하고 실제 활성화(생성자 · `OnActivateAsync`)는 첫 호출 시점에 일어남을 직접 테스트로 확인 <br> 유휴 시 비활성화됐다가 다음 호출에 재활성화되므로, 대기열 Grain에 `KeepAlive = true`를 준 근거 |

### 단일 트랜잭션 — 가챠 차감·저장 통합

[IOCP 서버](https://github.com/rkdtlsgj/IOCP_Server)를 공부할 때 실수했던 작업인데, 재화 차감과 결과 저장이 분리되어 있어 중간 실패 시 재화만 차감될 가능성이 있었습니다.<br>
그래서 차감 → 천장 갱신 → 이력 기록 → 캐릭터를 하나의 트랜잭션으로 묶어 이 시나리오를 제거했습니다.<br>
(1차 설계였던 보상 트랜잭션을 대체한 과정은 [05 설계 변천사](#05-설계-변천사)에 정리했습니다.)

```csharp
await using var conn = new NpgsqlConnection(_connectionString);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();

var spendResult = await _walletRepository.SpendGemAsync(conn, tx, userId, amount);
if (spendResult.Success == false)
{
    await tx.RollbackAsync();
    return spendResult;
}

await SaveDrawAsync(conn, tx, userId, cards, pityPoint);
await tx.CommitAsync();
```
---

## 03 데이터 계층 — 가챠·지갑 정합성

| 설계 | 내용 | 게임 적용 |
|------|------|-----------|
| **유료/무료 재화 분리** | 무료젬 우선 차감, 부족분만 유료젬에서 차감 | 결제 재화 정산 정확성  환불 정책 대응 |
| **COPY (binary import)** | 가챠 이력 N건을 단일 스트림으로 기록 | 10연차 등 대량 기록 시 DB 왕복 절감 |
| **Batch Upsert** | 보유 캐릭터 N건을 `unnest` 배열 바인딩 + `on conflict`로 단일 쿼리 처리 | 중복 획득 시 count 집계 유지 |
| **캐시 전략** | 천장 포인트는 Grain 필드에 캐싱(턴 실행으로 안전), 세션·대기열은 Redis | 뽑기마다 반복되던 DB 조회 제거 |

### 부하 테스트 — 병목을 단계별로 제거

유저 200명이 10연차를 100회씩 수행하는 시나리오로 측정했습니다. 클라이언트에서 `TestTimer`로 `DrawAsync(sessionId, 10)` 호출 시간을 기록하고, 서버에서는 DB 작업 구간을 나눠 측정했습니다.

**STEP 0 · 최초 측정 — 병목 식별**

| 구간 | 소요 시간 |
|---|---:|
| 요청 전체 평균 | **60ms** |측정
| 재화 차감 (SpendGem) | 15ms |
| 천장 조회 (GetPity) | 5ms |
| 뽑기 결과 저장 (SaveDraw) | **138ms — 병목** |

**STEP 1 · 천장 조회 캐싱** — 뽑기마다 반복되던 GetPity DB 조회를 Grain 필드 캐싱으로 제거

**STEP 2 · 로그용 조회 쿼리 제거** — 결과에 불필요한 조회를 제거

**STEP 3 · COPY / unnest 배치 적용** — 행 단위 INSERT를 단일 스트림·단일 쿼리로 통합

**최종 결과 (200명 기준)**

| 구간 | 개선 전 | 개선 후 |
|---|---:|---:|
| 요청 전체 평균 | 60ms | **6.2ms** |
| 뽑기 결과 저장 (SaveDraw) | 138ms | **1.5ms** |

### 규모별 측정

개선 후에는 실제 사용 흐름을 고려해 가챠 요청 사이에 1~2초 간격을 두고 부하를 측정했습니다.

| 동시 사용자 | 요청 평균 | SpendGem | GetPity | SaveDraw | CPU | 총 소요시간 |
|---:|---:|---:|---:|---:|---:|---:|
| **200명** | **6.2ms** | 0.7ms | 2.4ms | 1.5ms | 7% | 02:38 |
| **1,000명** | **5.9ms** | 0.8ms | 0.3ms | 2.1ms | 17% | 02:42 |
| **5,000명** | **96.2ms** | 3.5ms | 1.8ms | 12.3ms | 23% | 02:49 |

5,000명 구간에서 요청 평균이 96.2ms로 튄 원인은 이후 [04장](#04-분산--무엇이-보장되고-무엇이-아닌가)에서 추적했습니다. PostgreSQL 커넥션 한도(`max_connections=100`)에 Npgsql 기본 풀(프로세스당 100)이 그대로 닿는 구성이었고, 풀 크기를 명시하자 50만 요청에서 실패 0건이 됐습니다.

### 기능·확률 검증

| 항&#8288;목 | 검증 내용 | 결과 |
|------|----------|------|
| **매칭** | GrainTimer로 2명씩 매칭하고 홀수 인원은 대기열에 유지 | Observer 결과 통지, PostgreSQL `match_history` 저장, Redis 채널별 대기열 확인 |
| **가챠** | 1회·10회 뽑기, 90연차 천장, 뽑기 이력 저장 | 각 시나리오 정상 동작과 DB 이력 저장 확인 |
| **확률** | 200명이 각각 10연차를 100회 요청 | 천장 보정에 따른 소폭 차이를 제외하고 실제 획득 비율이 설정 확률과 거의 일치 |

확률 검증 결과와 스크린샷, 구간별 로그는 [테스트 및 성능 문서](docs/performance.md)에 정리했습니다.

---

## 04 분산 — 무엇이 보장되고 무엇이 아닌가

Orleans 문서를 보면서 공부하던 도중 부하 분산에 대해 설명이 있었고 흥미가 생겨서 확인을 위해 작업했습니다.

사일로 2개를 띄워 분산이 실제로 무엇을 주는지 측정했습니다. 위치 투명성과 클러스터링은 Orleans의 대표적인 장점으로 소개되지만, 그것이 곧 성능을 뜻하는지는 별개 문제입니다.

상세 수치와 실행 방법은 [분산 · 스케일아웃 측정 문서](docs/distributed.md)에 있습니다.

### 4-1. 배치 분포 — 활성화 수 기준으로 균형을 맞춘다

`ActivationCountBasedPlacement`(`ChooseOutOf = 2`) 기준, 자신이 활성화된 사일로를 반환하는 프로브 Grain으로 측정했습니다.

| 실행 | 11111 | 11112 |
|---|---:|---:|
| run1 (1,000개) | 570 (57.0%) | 430 (43.0%) |
| run2 (1,000개) | 431 (43.1%) | 569 (56.9%) |
| **누적 (2,000개)** | **1,001 (50.05%)** | **999 (49.95%)** |

run2가 run1의 정반대로 나온 것이 핵심입니다. run1 종료 시점에 11111에 570개가 살아 있었기 때문에 run2에서는 활성화 수가 적은 11112가 계속 선택됐습니다 — 요청 단위가 아니라 **클러스터 전체 활성화 수** 기준으로 균형을 맞춘다는 뜻입니다.

### 4-2. 장애 조치

동일한 Grain ID 집합으로 장애 전후를 비교했습니다.

| 종료 대상 | 결과 |
|---|---|
| 보조 사일로 (11112) | 장애 전 100:100 → **살아남은 사일로에서 200개(100%) 재활성화**, 호출 200/200 성공, 애플리케이션에 예외 미전달 |
| **Primary 사일로 (11111)** | **클러스터 사용 불가** — 생존 사일로가 멤버십을 갱신하지 못해 인계 실패, 클라이언트 미처리 예외 종료 |

### 4-3. 성능 — 사일로를 늘리면 빨라지는가

사일로 수만 바꿔 동일 시나리오를 측정했습니다.

| 지표 | 1 사일로 | 2 사일로 |
|---|---:|---:|
| 요청 평균 — 200명 (간격 1~2초) | **2.91ms** | 5.50ms |
| 처리량 — 200명 (간격 없음) | **1,807 req/s** | 1,536 req/s |
| p50 — 5,000명 · 50만 요청 | **51.8~63.9ms** | 103.7ms |
| 처리량 — 5,000명 | **2,628~2,700 req/s** | 2,587 req/s |
| 사일로 CPU | 최대 11.5% | 최대 11.2% |

개선되지 않았고, 지연은 오히려 늘었습니다. 처음에는 늘어 날거라 생각했지만 병목은 SQL에서 발생했기 때문에 사일로를 늘려도 눈에 띄는 성능은 없었습니다

### 결론

사일로 수를 늘려서 성능적인 이득을 보려면 Grain의 Key가 잘 흩어져있어야 하며 다른 사일로가 죽을시 다른 사일로가 계속 요청을 받아서 작업할 수 있는 환경이다. 

---

## 05 설계 변천사

처음 설계가 그대로 남은 것이 아니라, 한계를 확인하고 대체하거나 기각한 것들입니다.<br>
개선뿐 아니라 **왜 버렸는지**도 판단의 일부라고 생각해 함께 기록합니다.

| 설계 | 1차 접근 | 확인한 한계 | 최종 판단 |
|------|---------|------------|----------|
| **가챠 정&#8288;합&#8288;성** | 보상 트랜잭션 — 차감 후 저장 실패 시 환불 호출로 원복 | 환불 호출 자체가 실패하면 유실이 남고, 실패 경로가 성공 경로만큼 복잡해짐 <br>차감과 저장이 **같은 PostgreSQL**이므로 애초에 분산 트랜잭션 문제가 아니었음 | **대체** — 단일 DB 트랜잭션으로 통합, 보상 경로 자체를 제거 |
| **이력 &#8288;저장** | 행 단위 INSERT 반복 | 10연차 시 N회 왕복으로 SaveDraw가 요청의 병목(138ms) | **대체** — COPY(binary import) + `unnest` batch upsert |
| **대기열<br>스케줄링** | Reminder 검토 | 대기열은 활성화 기간에만 유효한 메모리 상태 — 영속 스케줄러가 과함 | **기각** — silo-local GrainTimer + `KeepAlive = true` |
| **스케일아&#8288;웃<br>방&#8288;식** | 사일로 추가로 처리량 확보 | 병목이 공유 DB라 사일로를 늘려도 처리량은 그대로이고 지연만 증가 ([04장](#04-분산--무엇이-보장되고-무엇이-아닌가)) | **보류** — 저장소 분할(파티셔닝 → 샤딩)이 선행 과제 |

---

## 06 설계 배경

### IOCP와 비교하며 이해하기

Orleans를 배울 때 프로카데미에서 공부한 IOCP와 비교하며 이해했습니다.<br>
당시 IOCP를 공부하며 직접 구현했던 서버가 [**IOCP_Server**](https://github.com/rkdtlsgj/IOCP_Server)입니다 — C++ IOCP 기반 채팅 서버로, 섹터 기반 AOI(3×3 섹터) 브로드캐스트, 메모리 풀, 섹터 단위 락으로 동시성을 직접 제어했습니다.<br>
그때 락으로 직접 지켰던 공유 상태를 Orleans는 모델 차원에서 어떻게 없애는지가 이 비교의 출발점이었습니다.

**비슷한 점**

* 둘 다 "연결 요청마다 스레드를 만들지 않는다"는 공통점이 있습니다. IOCP는 Completion Queue를 소수의 워커 스레드가 소비하고, Orleans는 Grain별 메시지 큐를 .NET 스레드 풀이 소비합니다 — **큐에 쌓고, 소수의 스레드가 꺼내 처리한다**는 구조가 같습니다.<br>
* 실제로 Windows에서 .NET의 비동기 소켓 I/O는 내부적으로 IOCP를 사용하므로, Orleans도 결국 IOCP 위에서 동작합니다.<br>

**다른 점**

| | IOCP | Orleans |
|---|------|---------|
| 해결하는 문&#8288;제 | I/O 완료 통지와 스레드 스케줄링 (네트워크 계층) | 상태 실행 단위의 격리 (애플리케이션 계층) |
| 동시성 제어 | 어떤 워커 스레드든 어떤 세션이든 처리 → 공유 상태 보호는 개발자 몫 (락 / CAS) | Grain 단위 턴 기반 실행 기본 제공 → 락 불필요 |
| 분산 | 단일 머신 API — 스케일아웃은 별도 설계 필요 | 위치 투명성 · 클러스터링 내장 <br>단 **처리량 확보는 별개** — [04장](#04-분산--무엇이-보장되고-무엇이-아닌가) 참고 |
| 제어 수준 | 커널에 가까운 저수준 제어, 성능 튜닝 여지 큼 | 생산성과 안전을 위해 저수준을 추상화 |

요약하면 IOCP는 "**적은 스레드로 많은 I/O를 어떻게 처리할 것인가**"에 대한 답이고, Orleans는 그 위에서 "**상태를 어떻게 안전하게 다룰 것인가**"까지 답하는 모델입니다.<br>
IOCP 서버였다면 직접 만들어야 했을 세션별 직렬화(락 또는 로직 큐)를 Orleans는 Grain 모델로 기본 제공한다는 것이 가장 큰 차이였습니다.<br>

### 트러블슈팅 / 케이스 스터디

| 사례 | 요약 | 문서 |
|------|------|------|
| 가챠 재화 유실 가&#8288;능&#8288;성 | 차감과 저장이 분리되어 부분 실패 시 재화만 차감 → 단일 DB 트랜잭션으로 통합 | [주요 기능](docs/features.md#-가챠) |
| 가챠 저장 병목 | SaveDraw 138ms로 병목 확인 → 천장 캐싱  로그 조회 제거  COPY 적용 | [테스트 및 성능](docs/performance.md) |
| 멀티 사일로 접속 &#8288;실패 | 사일로는 `UseLocalhostClustering` 기본값으로 ClusterId·ServiceId가 `default`인데 클라이언트는 `dev`로 설정해 접속 불가 → 사일로에 `Configure<ClusterOptions>`로 명시. `UseLocalhostClustering(serviceId:, clusterId:)` 인자로는 반영되지 않았음 | [분산 / 스케일아웃](docs/distributed.md#테스트-환경) |
| 커넥션 한&#8288;도 초과 | 포화 부하에서 실패 원인이 전부 `SqlState 53300` → `max_connections=100`에 Npgsql 기본 풀(프로세스당 100)이 그대로 닿은 것. 풀 크기 명시로 50만 요청 실패 0건 | [분산 / 스케일아웃](docs/distributed.md#6-커넥션-한도를-풀고-5000명-재측정) |

---

## 📚 문서

* [주요 기능](docs/features.md) — 로그인/세션, 매칭, 지갑, 가챠 상세 설명
* [데이터베이스 구조](docs/database.md) — PostgreSQL 테이블 구조(ERD), Redis 키 구조
* [테스트 및 성능](docs/performance.md) — 기능 테스트 결과, 가챠 부하 테스트 및 최적화
* [분산 · 스케일아웃 측정](docs/distributed.md) — 배치 분포, 장애 조치, 사일로 1개 vs 2개 실측

---
