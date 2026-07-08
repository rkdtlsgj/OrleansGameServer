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

# 문서
* [주요 기능](docs/features.md) — 로그인/세션, 매칭, 지갑, 가챠 상세 설명
* [데이터베이스 구조](docs/database.md) — PostgreSQL 테이블 구조(ERD), Redis 키 구조
* [테스트 시나리오](docs/test-scenarios.md) — 기능별 수동 테스트 체크리스트
