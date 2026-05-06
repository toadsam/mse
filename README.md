# mse

Unity 클라이언트와 Spring Boot 백엔드를 함께 포함한 메타버스/게임 서비스 실험 저장소입니다. Unity 프로젝트와 `last-round-backend` 서버를 한 저장소에서 관리합니다.

## 프로젝트 개요

`mse`는 Unity 기반 클라이언트와 Java 백엔드를 연결해 게임 결과, 유저 정보, 랭킹, 매치 기록 같은 데이터를 다루는 구조를 실험합니다. Unity 쪽은 3D 씬과 에셋을 포함하고, 백엔드는 REST API와 인증/저장소 계층을 제공합니다.

## 주요 기능 영역

### Unity 클라이언트

- Unity 씬과 3D 에셋 구성
- Synty/Polygon 계열 에셋 활용
- 클라이언트 플레이 공간과 시각 요소 실험

### Spring Boot 백엔드

- 사용자 인증 API
- JWT 기반 보안 구조
- 매치 기록 API
- 랭킹 API
- 증강/강화 데이터 API
- JPA 기반 데이터 저장
- H2/MySQL 환경 대응

## 기술 스택

- Unity `6000.3.11f1`
- C#
- Java 17
- Spring Boot `3.3.4`
- Spring Security
- Spring Data JPA
- JWT
- H2 / MySQL
- Gradle

## 폴더 구조

```text
.
├── Assets/
├── Packages/
├── ProjectSettings/
└── last-round-backend/
    ├── src/main/java/com/lastround/backend/
    │   ├── controller/
    │   ├── service/
    │   ├── entity/
    │   ├── repository/
    │   └── security/
    └── build.gradle
```

## Unity 실행 방법

1. Unity Hub에서 `6000.3.11f1` 버전을 설치합니다.
2. 저장소 루트 폴더를 Unity 프로젝트로 엽니다.
3. `Assets/Scenes` 아래의 씬을 열어 실행합니다.

## 백엔드 실행 방법

```bash
cd last-round-backend
./gradlew bootRun
```

Windows에서는 다음 명령을 사용할 수 있습니다.

```bash
gradlew.bat bootRun
```

기본 서버 포트는 `8080`입니다.

## 백엔드 주요 도메인

- `AuthController`: 회원가입, 로그인, 토큰 갱신
- `UserController`: 내 정보 조회/수정
- `MatchController`: 매치 결과와 기록
- `LeaderboardController`: 랭킹 조회
- `AugmentController`: 증강 데이터 조회

## 개발 메모

현재 백엔드 설정에는 로컬 개발용 DB와 JWT 설정이 포함되어 있습니다. 운영 배포 전에는 민감한 설정을 환경 변수로 분리하고, Unity 클라이언트의 API 연결 주소도 환경별로 관리하는 것이 좋습니다.
