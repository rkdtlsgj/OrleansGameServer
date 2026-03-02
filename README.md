# Orleans MatchServer

Microsoft Orleans기반의 매칭 대기열 프로그램이다
유저는 닉네임을 입력하고 채널을 입력해서 매칭 대기열에 참가하게된다.

개발 1단계 같은 큐에 2명이 모이면 서버가 실시간으로 매칭을 진행하게 된다.
개발 2단계 스케줄러를 이용해서 몇분마다 매칭을 돌리는 시스템을 추가한다.

# 환경
* .NET 8.0
* Microsoft Orleans (Silo/Client)
* Localhost clustering(개발용)


# 개발 목적
매치메이킹 처럼 동시 참가로 경쟁 조건이 생기기 쉬운문제를 Grain모델로 단순화하여 방지<br>
채널명을 키로 체크하여 순차적으로 Grain 요청을 처리하여 순차적으로 처리하여 락없이 수행하기위함


# 테스트
1차<br>
<img width="634" height="137" alt="image" src="https://github.com/user-attachments/assets/a97a1120-9f2d-43a1-9dea-61b7d7b63d80" />
<img width="641" height="121" alt="image" src="https://github.com/user-attachments/assets/b4886409-e5ca-4f2b-bdd9-e302ff97b245" />




