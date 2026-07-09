# OrleansGameServer

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](#)
[![Orleans](https://img.shields.io/badge/Microsoft-Orleans-0078D4)](#)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)](#)
[![Redis](https://img.shields.io/badge/Redis-DC382D?logo=redis&logoColor=white)](#)
[![Blog](https://img.shields.io/badge/Tech%20Blog-Orleans%20%EC%8A%A4%ED%84%B0%EB%94%94-orange)](https://blog.naver.com/rkdtlsgj/224298880418)

.NET / Microsoft Orleans 기반으로 로그인 매치메이킹 가챠 지갑 시스템을 구현한 게임 서버입니다.

가상 액터(Grain)의 턴 기반 실행 모델이 게임 서버의 동시성 정합성 문제를 어떻게 해결하는지 검증하는 것이 목표입니다.

📝 Orleans 학습 정리: [블로그 포스팅](https://blog.naver.com/rkdtlsgj/224298880418)

---

## 목차

| # | 챕터 | 내용 |
|:---:|------|------|
| **01** | [아키텍처](#01-아키텍처) | 시스템 구성 기술 스택  주요 기여 |
| **02** | [Grain 동시성 설계](#02-grain-동시성-설계) | 턴 기반 실행  타이머 인터리빙 제어  단일 트랜잭션 |
| **03** | [데이터 계층 — 가챠·지갑 정합성](#03-데이터-계층--가챠지갑-정합성) | 유/무료 재화 분리  COPY  Batch Upsert  부하 테스트 |
| **04** | [설계 배경](#04-설계-배경) | 설계 동기  IOCP와 비교  트러블슈팅 사례 |

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
DB (COPY  batch upsert)    세션 · 매칭 대기열 캐시
```

### 기술 스택

| 분류 | 기술 |
|------|------|
| **Server** | .NET 10.0  Microsoft Orleans (Grains  GrainTimer  Observer) |
| **Concurrency** | 턴 기반 실행  타이머 `Interleave = false` 제어  단일 DB 트랜잭션 |
| **Data** | PostgreSQL (Npgsql  Dapper  COPY  batch upsert)  Redis (StackExchange.Redis) |
| **Client** | .NET 콘솔 앱 (Orleans Client + Observer 패턴) |

### 주요 기여

| 항목 | 내용 |
|------|------|
| **재화 유실 차단**  | 재화 차감과 뽑기 결과 저장을 하나의 DB 트랜잭션으로 통합 <br>부분 실패 시 재화만 차감되는 유실 시나리오를 구조적으로 제거 |
| **천장 시스템** | 90연차 SSR 확정 로직 + 임계값 상수 분리로 기획 데이터 변경에 유연한 구조 |
| **이력 기록 최적화**    | 가챠 이력 N건을 PostgreSQL COPY(binary import)로, 보유 캐릭터를 `unnest` 기반 batch upsert로 단일 라운드트립 기록 |
| **매칭 파이프라인**  | 채널 단위 Grain 대기열 → GrainTimer 주기 매칭 → Observer 비동기 통지 → 이력 기록 |

---

## 02 Grain 동시성 설계

Orleans Grain은 한 번에 하나의 요청만 실행하므로 락 없이 상태를 보호할 수 있습니다.<br>
다만 C#의 async 메서드는 await 지점마다 상태머신으로 분할되어 실행되므로<br>타이머 콜백이나 await 지점의 인터리빙까지 자동으로 안전해지는 것은 아닙니다.<br>
그래서 아래와 같은 지점들은 명시적인 설계 판단이 필요했습니다.

| 주제 | 설계 판단 |
|------|-----------|
| **턴 기반 실행** | Grain 메서드는 한 번에 하나의 턴만 실행 — `GachaGrain`의 천장 포인트 필드 캐싱(`_pityPoint`)이 락 없이 안전한 근거 |
| **타이머 인터리빙** | 매칭 타이머를 `Interleave = false`로 등록해 콜백도 일반 턴처럼 직렬화 <br> `Enqueue`/`Cancel`과 대기열 상태가 교차 실행되지 않음 |
| **`[Reentrant]` <br> 미사용** | 재진입을 허용하면 검사-후-행동(TOCTOU) 사이에 상태가 바뀔 수 있어, 기본값인 비-재진입을 유지 <br> 상태를 바꾸지 않는 조회 로그성 Grain에만 허용 가능하다고 판단|
| **`ConfigureAwait(false)` <br> 미사용** | 턴 기반 보장은 Grain 전용 TaskScheduler가 제공 — `ConfigureAwait(false)`를 쓰면 await 이후 스레드 풀로 벗어나 싱글스레드 보장이 깨지므로 Grain 내부에서는 사용하지 않음 |
| **Timer vs Reminder** | 대기열은 활성화 기간에만 의미 있는 메모리 상태이므로 Reminder 대신 silo-local GrainTimer 선택<br>`KeepAlive = true`로 유휴 비활성화 방지 |
| **Grain 생명주기** | `GetGrain`은 참조만 반환하고 실제 활성화(생성자  `OnActivateAsync`)는 첫 호출 시점에 일어남을 직접 테스트로 확인 <br> 유휴 시 비활성화됐다가 다음 호출에 재활성화되므로, 대기열 Grain에 `KeepAlive = true`를 준 근거 |

### 단일 트랜잭션 — 가챠 차감·저장 통합

[IOCP 서버](https://github.com/rkdtlsgj/IOCP_Server)를 공부할 때 실수했던 작업인데, 재화 차감과 결과 저장이 분리되어 있어 중간 실패 시 재화만 차감될 가능성이 있었습니다.<br>
그래서 차감 → 천장 갱신 → 이력 기록 → 캐릭터를 하나의 트랜잭션으로 묶어 이 시나리오를 제거했습니다.

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
| **캐시 전략** | 천장 포인트는 Grain 필드에 캐싱(턴 실행으로 안전), 세션 대기열은 Redis | 뽑기마다 반복되던 DB 조회 제거 |

### 부하 테스트 (유저 200명 · 10연차 100회씩)

| 구간 | 소요 시간 |
|---|---|
| 요청 전체 평균 | **avg 60ms** |
| 재화 차감 (SpendGem) | 15ms |
| 천장 조회 (GetPity) | 5ms |
| 뽑기 결과 저장 (SaveDraw) | 138ms → 병목 확인 |

병목이 DB 저장에 있음을 확인하고 천장 조회 캐싱  로그용 조회 제거 으로 개선했습니다.<br>
확률 검증 결과와 스크린샷은 [테스트 및 성능 문서](docs/performance.md)에 정리했습니다.

---

### 테스트

| 항목 | 내용 |
|------|------|
| **기능 테스트** | 로그인 → 매칭(2명 단위 Observer 통지) → 가챠(천장 이력) 시나리오 수동 검증 — [결과](docs/performance.md) |
| **부하 테스트** | 유저 200명 동시 가챠, 확률 분포가 설정값과 일치함을 검증 — [결과](docs/performance.md#-가챠-부하-테스트-및-최적화) |

### 코드 리딩 가이드

| 관심사 | 핵심 파일 |
|------|-----------|
| Grain 인터페이스 | [Common/Grain/](Common/Grain/) |
| 로그인 / 세션 | [LoginGrain.cs](OrleansMatchingServer/Grain/LoginGrain.cs) · [SessionRepository.cs](OrleansMatchingServer/SessionRepository.cs) |
| 매칭 대기열 / 타이머 | [MatchingQueueGrain.cs](OrleansMatchingServer/Grain/MatchingQueueGrain.cs) · [QueueCacheRepository.cs](OrleansMatchingServer/QueueCacheRepository.cs) |
| 가챠 / 단일 트랜잭션 | [GachaGrain.cs](OrleansMatchingServer/Grain/GachaGrain.cs) · [GachaHistoryRepository.cs](OrleansMatchingServer/GachaHistoryRepository.cs) |
| 지갑 | [WalletGrain.cs](OrleansMatchingServer/Grain/WalletGrain.cs) · [WalletRepository.cs](OrleansMatchingServer/WalletRepository.cs) |
| 클라이언트 / Observer | [Client/Program.cs](Client/Program.cs) · [ConsoleMatchObserver.cs](Client/ConsoleMatchObserver.cs) |

---

## 04 설계 배경

### IOCP와 비교하며 이해하기

Orleans를 배우면서 프로카데미때 공부했던 IOCP와 비교하면서 공부를 진행했습니다.<br>
당시 IOCP를 공부하며 직접 구현했던 서버가 [**IOCP_Server**](https://github.com/rkdtlsgj/IOCP_Server)입니다 — C++ IOCP 기반 채팅 서버로, 섹터 기반 AOI(3×3 섹터) 브로드캐스트, 메모리 풀, 섹터 단위 락으로 동시성을 직접 제어했습니다.<br>
그때 락으로 직접 지켰던 공유 상태를 Orleans는 모델 차원에서 어떻게 없애는지가 이 비교의 출발점이었습니다.

**비슷한 점**

* 둘 다 " 연결 요청마다 스레드를 만들지 않는다"는 점을 봤습니다. IOCP는 Completion Queue를 소수의 워커 스레드가 소비하고, Orleans는 Grain별 메시지 큐를 .NET 스레드 풀이 소비합니다 — **큐에 쌓고, 소수의 스레드가 꺼내 처리한다**는 구조가 같습니다.<br>
* 실제로 Windows에서 .NET의 비동기 소켓 I/O는 내부적으로 IOCP를 사용하므로, Orleans도 결국 IOCP 위에서 동작합니다.<br>

**다른 점**

| | IOCP | Orleans |
|---|------|---------|
| 해결하는 문제 | I/O 완료 통지와 스레드 스케줄링 (네트워크 계층) | 상태 실행 단위의 격리 (애플리케이션 계층) |
| 동시성 제어 | 어떤 워커 스레드든 어떤 세션이든 처리 → 공유 상태 보호는 개발자 몫 (락 / CAS) | Grain 단위 턴 기반 실행 기본 제공 → 락 불필요 |
| 분산 | 단일 머신 API — 스케일아웃은 별도 설계 필요 | 위치 투명성  클러스터링 내장 |
| 제어 수준 | 커널에 가까운 저수준 제어, 성능 튜닝 여지 큼 | 생산성과 안전을 위해 저수준을 추상화 |

요약하면 IOCP는 "**적은 스레드로 많은 I/O를 어떻게 처리할 것인가**"에 대한 답이고, Orleans는 그 위에서 "**상태를 어떻게 안전하게 다룰 것인가**"까지 답하는 모델입니다.<br>
IOCP 서버였다면 직접 만들어야 했을 세션별 직렬화(락 또는 로직 큐)를 Orleans는 Grain 모델로 기본 제공한다는 것이 가장 큰 차이였습니다.<br>

### 트러블슈팅 / 케이스 스터디

| 사례 | 요약 | 문서 |
|------|------|------|
| 가챠 재화 유실 가능성 | 차감과 저장이 분리되어 부분 실패 시 재화만 차감 → 단일 DB 트랜잭션으로 통합 | [주요 기능](docs/features.md#-가챠) |
| 가챠 저장 병목 | SaveDraw 138ms로 병목 확인 → 천장 캐싱  로그 조회 제거  COPY 적용 | [테스트 및 성능](docs/performance.md) |

---

## 📚 문서

* [주요 기능](docs/features.md) — 로그인/세션, 매칭, 지갑, 가챠 상세 설명
* [데이터베이스 구조](docs/database.md) — PostgreSQL 테이블 구조(ERD), Redis 키 구조
* [테스트 및 성능](docs/performance.md) — 기능 테스트 결과, 가챠 부하 테스트 및 최적화

---
