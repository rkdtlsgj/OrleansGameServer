# 테스트 및 성능

기능별 동작 확인 결과와 가챠 부하 테스트, 최적화 과정을 정리했습니다.

[← README로 돌아가기](../README.md)

## ⚔️ 매칭 테스트

<img width="654" height="231" alt="매칭 로그" src="https://github.com/user-attachments/assets/4e34b00c-5285-4b0e-8626-8b2943222524" /><br>
<img width="273" height="158" alt="매칭 결과" src="https://github.com/user-attachments/assets/764da523-4f1b-4c05-ae15-fc0b75f1b49c" /><br>

타이머를 통해 2명씩 매칭되고, 남은 유저는 계속 대기하는 형태로 동작합니다.

<img width="588" height="298" alt="매칭 이력 저장" src="https://github.com/user-attachments/assets/3a887b01-31d8-45a4-b5a9-92e5491752bf" /><br>
<img width="715" height="151" alt="Redis 대기 유저" src="https://github.com/user-attachments/assets/294db437-db34-410c-80c2-7be9eed879ab" /><br>

* PostgreSQL `match_history`에 매칭 완료 이력 저장 확인
* Redis에서 채널별 대기 유저 목록 확인

## 🎰 가챠 테스트

<img width="256" height="431" alt="가챠 뽑기 결과" src="https://github.com/user-attachments/assets/88e55fd8-cb69-4b68-8aba-83f9b7263ebc" />
<img width="430" height="271" alt="가챠 이력" src="https://github.com/user-attachments/assets/ee3c6c48-2522-4e75-aedd-f06833c17926" /><br>

1회/10회 뽑기, 천장, 이력 저장이 정상 동작하는 것을 확인했습니다.

## 📊 가챠 부하 테스트 및 최적화

### 테스트 조건

* User **200명** 동시 가챠 테스트
* User당 10회 뽑기 **100회** 요청


var timer = TestTimer.StartNew();<br>
await DrawAsync(sessionId, 10);<br>
timer.Stop();<br>
  
### 확률 검증

등급 / 획득수 / 실제비율 / 기대값 / 차이<br>
<img width="320" height="57" alt="확률 검증 결과" src="https://github.com/user-attachments/assets/99b03286-7905-4891-a287-8dd54a7b075b" /><br>

> 천장 보정으로 인한 소폭 차이는 있으나, 실제 획득 비율이 설정 확률과 거의 일치함을 확인했습니다.

### 병목 분석

| 구간 | 소요 시간 |
|---|---| 
200명
| 요청 전체 평균 | avgMs = 60 |
| 재화 차감 (SpendGem) | 15ms |
| 천장 조회 (GetPity) | 5ms |
| 뽑기 결과 저장 (SaveDraw) | 138ms |

구간별 측정 결과 **대부분의 시간이 DB 저장 작업(SaveDraw)에서 소요**되는 것을 확인했습니다.

개선 이후 실제 환경과 비슷하게 가챠 이후 1000ms~2000ms 텀을 준 후 실행하게 했습니다.

200명
| 요청 전체 평균 | avgMs = 6.2ms |
| 재화 차감 (SpendGem) | 0.7ms |
| 천장 조회 (GetPity) | 2.4ms |
| 뽑기 결과 저장 (SaveDraw) | 1.5ms |
| CPU 사용률 | 7% | 소요시간 02:38 |

1000명
| 요청 전체 평균 | avgMs = 5.9ms |
| 재화 차감 (SpendGem) | 0.8ms |
| 천장 조회 (GetPity) | 0.3ms |
| 뽑기 결과 저장 (SaveDraw) | 2.1ms |
| CPU 사용률 | 17% | 소요시간 02:42 |

5000명
| 요청 전체 평균 | avgMs = 96.2ms |
| 재화 차감 (SpendGem) | 3.5ms |
| 천장 조회 (GetPity) | 1.8ms |
| 뽑기 결과 저장 (SaveDraw) | 12.3ms |
| CPU 사용률 | 23% | 소요시간 02:49 |

수많은 인원이 동시에 진행하기 때문에 CPU 사용률이 사람이 많을 수록 올라간다.

### 개선 사항

* 천장 포인트 조회 캐싱 으로 반복 DB 조회 제거
* 로그용 조회 쿼리 제거
* 대량 이력 저장에 PostgreSQL COPY 적용
