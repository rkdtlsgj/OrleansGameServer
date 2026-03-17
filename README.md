# Orleans MatchServer

Microsoft Orleans기반의 매칭 대기열 프로그램이다.<br>
유저는 닉네임을 입력하고 채널을 입력해서 매칭 대기열에 참가하게된다.

개발 1단계 같은 큐에 2명이 모이면 서버가 실시간으로 매칭을 진행하게 된다.<br>
개발 2단계 스케줄러를 이용해서 몇분마다 매칭을 돌리는 시스템을 추가한다.<br>
개발 3단계 매칭기록을 Redis와 SQL에 기록한다

# 환경
* .NET 8.0
* Microsoft Orleans (Silo/Client)
* Localhost clustering(개발용)
* PostgreSQL
* Redis

# 개발 목적
매치메이킹 처럼 동시 참가로 경쟁 조건이 생기기 쉬운문제를 Grain모델로 단순화하여 방지<br>
채널명을 키로 체크하여 순차적으로 Grain 요청을 처리하여 순차적으로 처리하여 락없이 수행하기위함<br>
Redis와 SQL을 연동하여 실시간 상태관리와 데이터를 저장하는 형태로 설계<br>

<details>
  <summary>코드 보기</summary>
  타이머를 이용해 주기적으로 RunMatch를 실행하도록 추가<br>
  <img width="489" height="450" alt="image" src="https://github.com/user-attachments/assets/8b08150b-8969-470d-92f3-c485b9c9fcd1" /><br>
  <img width="451" height="323" alt="image" src="https://github.com/user-attachments/assets/a9a42733-da86-486d-bda4-ea95378e280a" />
</details>





# 테스트
1차<br>
<img width="634" height="137" alt="image" src="https://github.com/user-attachments/assets/a97a1120-9f2d-43a1-9dea-61b7d7b63d80" />
<img width="641" height="121" alt="image" src="https://github.com/user-attachments/assets/b4886409-e5ca-4f2b-bdd9-e302ff97b245" />



2차<br>
<img width="654" height="231" alt="image" src="https://github.com/user-attachments/assets/4e34b00c-5285-4b0e-8626-8b2943222524" /><br>
<img width="273" height="158" alt="image" src="https://github.com/user-attachments/assets/764da523-4f1b-4c05-ae15-fc0b75f1b49c" /><br>
타이머에 의해서 2명씩 매칭이되고 남은 한사람은 계속 기다리는 형태로 변경<br>


3차<br>
<img width="588" height="298" alt="Image" src="https://github.com/user-attachments/assets/3a887b01-31d8-45a4-b5a9-92e5491752bf" /><br>
<img width="715" height="151" alt="Image" src="https://github.com/user-attachments/assets/294db437-db34-410c-80c2-7be9eed879ab" /><br>
SQL에 매칭완료 이력 저장<br>
Redis에 채널별로 대기 유저 확인<br>
