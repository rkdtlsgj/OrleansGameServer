# 테스트 및 성능

## 매칭 테스트

<img width="654" height="231" alt="image" src="https://github.com/user-attachments/assets/4e34b00c-5285-4b0e-8626-8b2943222524" /><br>
<img width="273" height="158" alt="image" src="https://github.com/user-attachments/assets/764da523-4f1b-4c05-ae15-fc0b75f1b49c" /><br>
타이머를 통해 2명씩 매칭되고, 남은 유저는 계속 기다리는 형태로 동작합니다.

<img width="588" height="298" alt="Image" src="https://github.com/user-attachments/assets/3a887b01-31d8-45a4-b5a9-92e5491752bf" /><br>
<img width="715" height="151" alt="Image" src="https://github.com/user-attachments/assets/294db437-db34-410c-80c2-7be9eed879ab" /><br>
SQL에 매칭 완료 이력 저장<br>
Redis에 채널별 대기 유저 확인

## 가챠 테스트

<img width="256" height="431" alt="image" src="https://github.com/user-attachments/assets/88e55fd8-cb69-4b68-8aba-83f9b7263ebc" />
<img width="430" height="271" alt="image" src="https://github.com/user-attachments/assets/ee3c6c48-2522-4e75-aedd-f06833c17926" /><br>
가챠 시스템 구현

## 가챠 부하 테스트 및 최적화

* User 200명 가챠 테스트
* User당 10회뽑기 100회 요청

등급 / 획득수 / 실제비율 / 기대값 / 차이<br>
<img width="320" height="57" alt="image" src="https://github.com/user-attachments/assets/99b03286-7905-4891-a287-8dd54a7b075b" /><br>
천장이 보정되어서 가차확률에 차이는 있지만 거의 일치 확인

* 평균 요청 avgMs=60
* SpendGemMs=15, GetPityMs=5, SaveDrawMs=138 → 대부분 DB 작업에서 느리다는 것을 확인 후 수정
* 수정 사항: 천장 조회 캐싱 / 로그용 조회 제거 / COPY 테스트
