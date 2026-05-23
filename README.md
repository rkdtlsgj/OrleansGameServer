# Orleans Game Server

Microsoft Orleans 기반의 게임 서버 학습 프로젝트입니다.

유저 로그인, 세션 관리, 매칭 큐, 매칭 결과 저장, 지갑, 가챠 콘텐츠를 Grain 단위로 분리해 구현했습니다.<br>
게임 서버에서 자주 다루는 상태 관리, 비동기 처리, Redis 캐시, PostgreSQL 저장 흐름을 작은 규모로 실험하는 것을 목표로 합니다.

# Orleans 학습정리
https://blog.naver.com/rkdtlsgj/224198414818

# 서버 구조

<img width="1707" height="986" alt="image" src="https://github.com/user-attachments/assets/9c6abaaa-22a2-4a5e-abc0-b677c5157207" />


# 환경
* .NET 10.0
* Microsoft Orleans
* c# async/await
* PostgreSQL
* Redis

# 주요 기능
로그인 / 회원가입
* 유저 ID를 Grain key로 사용합니다.
* 회원가입 시 비밀번호 해시와 생성 시간을 DB에 저장합니다.
* 로그인시 DB의 유저 정보를 조회해 검증합니다.
* 로그인 성공 시 Redis에 24시간 유효한 세션을 저장합니다.

매칭 큐
* 채널 이름을 Grain key로 사용합니다.
* 현재 클라이언트는 `dice` 채널에 입장합니다.
* 같은 채널에 대기 중인 유저를 주기적으로 2명씩 매칭합니다.
* 매칭 성공 시 양쪽 클라이언트에 Observer로 결과를 알립니다.
* Redis에는 채널별 대기 유저와 유저별 현재 채널을 캐싱합니다.
* PostgreSQL에는 매칭 완료 기록을 저장합니다.

지갑
* 유저 ID를 Grain key로 사용합니다.
* 유료젬과 무료젬을 관리합니다.
* 재화 사용 시 무료젬을 먼저 차감하고, 부족분을 유료젬에서 차감합니다.

가챠
* 유저 ID를 Grain key로 사용합니다.
* 1회 뽑기와 10회 뽑기를 지원합니다.
* 뽑기 실행 전 `WalletGrain`을 통해 재화를 차감합니다.
* 뽑기 결과와 남은 재화를 클라이언트에 반환합니다.
* character_info 테이블을 이용해 캐릭터 정보를 조회합니다.

로그 
* ILogger를 이용하여 주요 서버 이벤트 기록합니다.
  

# 테스트

<details>
<summary>결과 보기</summary>
<img width="654" height="231" alt="image" src="https://github.com/user-attachments/assets/4e34b00c-5285-4b0e-8626-8b2943222524" /><br>
<img width="273" height="158" alt="image" src="https://github.com/user-attachments/assets/764da523-4f1b-4c05-ae15-fc0b75f1b49c" /><br>
타이머에 의해서 2명씩 매칭이되고 남은 한사람은 계속 기다리는 형태로 변경<br><br>

<img width="588" height="298" alt="Image" src="https://github.com/user-attachments/assets/3a887b01-31d8-45a4-b5a9-92e5491752bf" /><br>
<img width="715" height="151" alt="Image" src="https://github.com/user-attachments/assets/294db437-db34-410c-80c2-7be9eed879ab" /><br>
SQL에 매칭완료 이력 저장<br>
Redis에 채널별로 대기 유저 확인<br><br>


<img width="1045" height="295" alt="image" src="https://github.com/user-attachments/assets/ef5684a0-9322-4ff7-b72a-4b86ffd48f97" /><br>
유저 정보 DB에 저장 추가<br><br>

<img width="256" height="431" alt="image" src="https://github.com/user-attachments/assets/88e55fd8-cb69-4b68-8aba-83f9b7263ebc" />
<img width="430" height="271" alt="image" src="https://github.com/user-attachments/assets/ee3c6c48-2522-4e75-aedd-f06833c17926" /><br>

가챠시스템 구현
</details>




