# 백엔드 구현 문서

이 문서는 `last-round-backend` Spring Boot 서버의 구조와 API, DB 저장 방식을 설명한다. Java, Spring Boot, REST API, JPA 개념을 조금 알고 있다는 전제로 작성했다.

## 1. 전체 개요

백엔드는 Unity 클라이언트가 필요한 계정과 게임 기록 기능을 제공한다.

담당하는 기능은 다음과 같다.

- 회원가입
- 로그인
- JWT access token 발급
- refresh token 발급 및 갱신
- 내 유저 정보 조회
- 닉네임 수정
- 매치 결과 저장
- 내 매치 히스토리 조회
- 리더보드 조회
- 백엔드 증강 목록 조회

프로젝트 위치는 다음이다.

```text
last-round-backend/
  build.gradle
  src/main/java/com/lastround/backend/
  src/main/resources/application.yml
  src/main/resources/application-mysql.yml
  src/main/resources/db/migration/
```

## 2. 기술 스택

`build.gradle` 기준 주요 기술은 다음과 같다.

- Java 17
- Spring Boot 3.3.4
- Spring Web
- Spring Security
- Spring Validation
- Spring Data JPA
- Flyway
- JJWT 0.12.6
- Lombok
- H2
- MySQL
- Gradle

개발 기본 DB는 H2 파일 DB이다. 운영 또는 외부 DB용 설정은 `application-mysql.yml`에 있다.

## 3. 실행 방법

Windows에서는 다음 명령으로 실행한다.

```bash
cd last-round-backend
gradlew.bat bootRun
```

기본 포트는 `8080`이다.

```text
http://localhost:8080
```

H2 콘솔은 개발 설정에서 열려 있다.

```text
http://localhost:8080/h2-console
```

## 4. 설정 파일

### 4.1 application.yml

기본 설정은 H2 파일 DB를 사용한다.

```yaml
spring:
  datasource:
    url: jdbc:h2:file:./data/last_round;MODE=MySQL;DATABASE_TO_LOWER=TRUE;AUTO_SERVER=TRUE
    username: sa
    password:
    driver-class-name: org.h2.Driver
```

중요한 설정은 다음과 같다.

- `ddl-auto: validate`: Entity로 DB를 자동 생성하지 않고, Flyway 마이그레이션과 Entity가 맞는지 검증한다.
- `flyway.enabled: true`: DB 스키마 변경은 `db/migration` SQL로 관리한다.
- `h2.console.enabled: true`: 개발 중 DB 확인용 H2 콘솔을 허용한다.
- `server.port: 8080`: 서버 포트이다.
- `jwt.secret`: JWT 서명 키이다.
- `access-token-expiration-minutes: 15`: access token 만료 시간이다.
- `refresh-token-expiration-days: 14`: refresh token 만료 일수이다.
- `refresh-cookie-name: refresh_token`: refresh token 쿠키 이름이다.

### 4.2 application-mysql.yml

MySQL 설정은 환경변수 기반으로 되어 있다.

```yaml
spring:
  datasource:
    url: jdbc:mysql://${DB_HOST:...}:${DB_PORT:3306}/${DB_NAME:last_round}
    username: ${DB_USERNAME:admin}
    password: ${DB_PASSWORD}
```

운영 배포에서는 `DB_PASSWORD`, `DB_HOST`, `DB_USERNAME` 같은 값이 외부 환경변수로 들어가야 한다.

## 5. 백엔드 패키지 구조

```text
com.lastround.backend
  config/
    SecurityConfig.java
    WebConfig.java
    DataInitializer.java
  controller/
    AuthController.java
    UserController.java
    MatchController.java
    LeaderboardController.java
    AugmentController.java
  dto/
    ApiResponse.java
    auth/
    user/
    match/
    leaderboard/
    augment/
  entity/
    User.java
    RefreshToken.java
    Match.java
    MatchPlayerStat.java
    MatchPlayerAugment.java
    Augment.java
  exception/
    AppException.java
    ErrorCode.java
    GlobalExceptionHandler.java
  repository/
    UserRepository.java
    RefreshTokenRepository.java
    MatchRepository.java
    MatchPlayerStatRepository.java
    MatchPlayerAugmentRepository.java
    AugmentRepository.java
  security/
    JwtTokenProvider.java
    JwtAuthenticationFilter.java
    CustomUserDetailsService.java
    UserPrincipal.java
    SecurityUtils.java
  service/
    AuthService.java
    RefreshTokenService.java
    UserService.java
    MatchService.java
    LeaderboardService.java
    AugmentService.java
```

구조는 일반적인 Spring 계층형 구조이다.

- Controller: HTTP 요청을 받는다.
- DTO: 요청/응답 데이터를 표현한다.
- Service: 실제 비즈니스 로직을 처리한다.
- Entity: DB 테이블과 매핑된다.
- Repository: JPA로 DB에 접근한다.
- Security: JWT 인증과 Spring Security 설정을 처리한다.
- Exception: 에러 응답을 통일한다.

## 6. 공통 응답 형식

모든 API는 `ApiResponse<T>` 형태로 응답한다.

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

실패하면 다음 형태가 된다.

```json
{
  "success": false,
  "data": null,
  "error": "User not found"
}
```

Unity의 `BackendApiResponse<T>`도 같은 필드 이름을 가지고 있다.

```csharp
public class BackendApiResponse<T>
{
    public bool success;
    public T data;
    public string error;
}
```

이렇게 맞춰 둔 이유는 Unity `JsonUtility.FromJson`으로 바로 파싱하기 위해서이다.

## 7. 인증과 보안 구조

보안 설정은 `SecurityConfig.java`에 있다.

### 7.1 공개 API

인증 없이 접근 가능한 경로는 다음과 같다.

- `/api/auth/**`
- `/`
- `/index.html`
- `/test-client.html`
- `/favicon.ico`
- `/h2-console/**`
- `GET /api/augments`
- `GET /api/leaderboard`

그 외 요청은 인증이 필요하다.

### 7.2 JWT 필터

`JwtAuthenticationFilter`는 모든 요청에서 `Authorization` 헤더를 읽는다.

```text
Authorization: Bearer {accessToken}
```

토큰이 유효하면 다음 작업을 한다.

1. `JwtTokenProvider.isValid(token)`으로 서명과 만료를 확인한다.
2. 토큰 subject에서 userId를 꺼낸다.
3. `CustomUserDetailsService.loadUserById(userId)`로 유저를 찾는다.
4. `UsernamePasswordAuthenticationToken`을 만든다.
5. `SecurityContextHolder`에 인증 정보를 넣는다.

컨트롤러나 서비스에서 현재 유저 ID가 필요할 때는 `SecurityUtils.getCurrentUserId()`를 호출한다.

### 7.3 Access Token과 Refresh Token

`JwtTokenProvider`는 두 종류의 토큰을 만든다.

- Access token: API 인증용, 기본 15분
- Refresh token: access token 재발급용, 기본 14일

Refresh token은 DB `refresh_tokens` 테이블에도 저장된다. 로그인하거나 회원가입하면 기존 유저의 refresh token을 삭제하고 새 token을 저장한다. refresh API를 호출하면 기존 refresh token을 검증한 뒤 새 refresh token으로 rotate한다.

`AuthController`는 refresh token을 응답 body에 넣어 주고, 동시에 `refresh_token` httpOnly 쿠키로도 내려준다. Unity 클라이언트는 주로 body의 refresh token을 `PlayerPrefs`에 저장해서 사용한다.

## 8. API 목록

| Method | Path | 인증 | 설명 |
| --- | --- | --- | --- |
| POST | `/api/auth/signup` | 필요 없음 | 회원가입 |
| POST | `/api/auth/login` | 필요 없음 | 로그인 |
| POST | `/api/auth/refresh` | 필요 없음 | refresh token으로 토큰 갱신 |
| GET | `/api/user/me` | 필요 | 내 유저 정보 조회 |
| PUT | `/api/user/update` | 필요 | 닉네임 수정 |
| POST | `/api/match/result` | 필요 | 매치 결과 저장 |
| GET | `/api/match/history` | 필요 | 내 매치 기록 조회 |
| GET | `/api/leaderboard` | 필요 없음 | 리더보드 조회 |
| GET | `/api/augments` | 필요 없음 | 백엔드 증강 목록 조회 |

## 9. Auth API

### 9.1 회원가입

```http
POST /api/auth/signup
Content-Type: application/json
```

요청:

```json
{
  "email": "player1@test.com",
  "password": "password123",
  "nickname": "player1"
}
```

검증 조건:

- email: 이메일 형식, 빈 값 불가
- password: 8자 이상 64자 이하
- nickname: 2자 이상 30자 이하
- email 중복 불가
- nickname 중복 불가

처리 흐름:

1. `AuthController.signup()`이 요청을 받는다.
2. `AuthService.signup()`이 이메일/닉네임 중복을 확인한다.
3. 비밀번호를 BCrypt로 암호화한다.
4. `users` 테이블에 저장한다.
5. access token과 refresh token을 발급한다.
6. refresh token을 DB에 저장한다.
7. refresh token 쿠키를 추가한다.
8. `AuthResponse`를 반환한다.

응답:

```json
{
  "success": true,
  "data": {
    "accessToken": "...",
    "refreshToken": "...",
    "userId": 1,
    "email": "player1@test.com",
    "nickname": "player1"
  },
  "error": null
}
```

### 9.2 로그인

```http
POST /api/auth/login
Content-Type: application/json
```

요청:

```json
{
  "email": "player1@test.com",
  "password": "password123"
}
```

처리 흐름:

1. 이메일로 유저를 찾는다.
2. 유저가 없으면 `USER_NOT_FOUND`.
3. BCrypt로 비밀번호를 비교한다.
4. 비밀번호가 틀리면 `WRONG_PASSWORD`.
5. 토큰을 발급한다.
6. 기존 refresh token을 삭제하고 새 refresh token을 저장한다.

### 9.3 토큰 갱신

```http
POST /api/auth/refresh
Content-Type: application/json
```

요청 body로 refresh token을 보낼 수 있다.

```json
{
  "refreshToken": "..."
}
```

또는 `refresh_token` 쿠키에 들어 있는 값을 사용할 수도 있다.

처리 흐름:

1. 요청 body에서 refresh token을 먼저 찾는다.
2. body에 없으면 쿠키에서 찾는다.
3. 둘 다 없으면 `INVALID_TOKEN`.
4. DB에서 refresh token을 찾는다.
5. 만료 시간이 지났으면 `TOKEN_EXPIRED`.
6. 새 access token과 새 refresh token을 만든다.
7. DB refresh token 값을 새 값으로 교체한다.

## 10. User API

### 10.1 내 정보 조회

```http
GET /api/user/me
Authorization: Bearer {accessToken}
```

응답:

```json
{
  "success": true,
  "data": {
    "id": 1,
    "email": "player1@test.com",
    "nickname": "player1",
    "createdAt": "2026-06-07T10:00:00"
  },
  "error": null
}
```

`SecurityUtils.getCurrentUserId()`로 현재 로그인 유저 ID를 꺼내고, `UserService.getMe()`가 DB에서 유저를 조회한다.

### 10.2 닉네임 수정

```http
PUT /api/user/update
Authorization: Bearer {accessToken}
Content-Type: application/json
```

요청:

```json
{
  "nickname": "newName"
}
```

검증 조건:

- 빈 값 불가
- 2자 이상 30자 이하
- 다른 유저가 이미 쓰는 닉네임이면 실패

Unity에서는 캐릭터 선택 후 Continue 버튼을 누를 때 이 API를 호출해서 입력한 닉네임을 서버에 반영한다.

## 11. Match API

### 11.1 매치 결과 저장

```http
POST /api/match/result
Authorization: Bearer {accessToken}
Content-Type: application/json
```

요청 예시:

```json
{
  "player1Id": 1,
  "player2Id": 2,
  "winnerId": 1,
  "player1Score": 3,
  "player2Score": 1,
  "players": [
    {
      "userId": 1,
      "result": "WIN",
      "score": 3,
      "damageDealt": 0,
      "characterName": "Character A",
      "augments": [
        {
          "augmentId": 0,
          "augmentName": "Multi Shot",
          "selectedOrder": 1,
          "selectedRound": 1
        }
      ]
    },
    {
      "userId": 2,
      "result": "LOSE",
      "score": 1,
      "damageDealt": 0,
      "characterName": "Character B",
      "augments": []
    }
  ]
}
```

검증 조건은 `MatchService.validatePlayers()`와 `validatePlayerPayload()`에 있다.

- player1Id와 player2Id는 달라야 한다.
- winnerId는 player1Id 또는 player2Id 중 하나여야 한다.
- 두 유저가 모두 DB에 존재해야 한다.
- players 배열은 정확히 2명이어야 한다.
- players의 userId 집합은 player1Id/player2Id와 같아야 한다.
- 플레이어별 score는 요약 점수와 일치해야 한다.
- 승자는 result가 `WIN`이어야 한다.
- 패자는 result가 `LOSE`여야 한다.
- 같은 라운드/순서의 증강 기록이 중복되면 안 된다.
- augmentId가 0보다 크면 실제 `augments` 테이블에 존재해야 한다.

저장되는 테이블은 세 개이다.

1. `matches`: 전체 요약
2. `match_player_stats`: 플레이어별 결과
3. `match_player_augments`: 플레이어별 선택 증강

현재 Unity는 자체 증강 ScriptableObject를 사용하므로 백엔드 `augments` 테이블의 ID와 바로 연결되지 않는다. 그래서 Unity는 보통 `augmentId`를 0으로 보내고 `augmentName`에 이름을 담는다. 백엔드는 `augment_id`가 null이어도 `augment_name`을 저장할 수 있다.

### 11.2 내 매치 히스토리

```http
GET /api/match/history?page=0&size=20
Authorization: Bearer {accessToken}
```

처리 흐름:

1. 현재 로그인 유저 ID를 가져온다.
2. `matches`에서 player1Id 또는 player2Id가 현재 유저인 기록을 조회한다.
3. 최신순으로 paging한다.
4. 조회된 match ID 목록으로 플레이어별 stat을 가져온다.
5. 조회된 match ID 목록으로 플레이어별 augment 기록을 가져온다.
6. match별로 묶어서 `MatchHistoryResponse`로 반환한다.

응답 구조:

```json
{
  "success": true,
  "data": {
    "content": [
      {
        "id": 1,
        "player1Id": 1,
        "player2Id": 2,
        "winnerId": 1,
        "player1Score": 3,
        "player2Score": 1,
        "createdAt": "2026-06-07T10:00:00",
        "players": [
          {
            "userId": 1,
            "nickname": "player1",
            "result": "WIN",
            "score": 3,
            "damageDealt": 0,
            "characterName": "Character A",
            "augments": []
          }
        ]
      }
    ],
    "page": 0,
    "size": 20,
    "totalElements": 1,
    "totalPages": 1
  },
  "error": null
}
```

## 12. Leaderboard API

```http
GET /api/leaderboard
```

인증 없이 호출 가능하다.

`LeaderboardService`는 모든 유저를 조회한 뒤 각 유저별로 다음 값을 계산한다.

- 전체 매치 수: player1Id 또는 player2Id로 들어간 횟수
- 전체 승리 수: winnerId가 해당 유저인 횟수
- 승률: `wins / matches * 100`, 소수점 2자리 반올림

정렬 기준은 다음과 같다.

1. 승률 높은 순
2. 승리 수 높은 순
3. 전체 매치 수 높은 순

응답:

```json
{
  "success": true,
  "data": [
    {
      "userId": 1,
      "nickname": "player1",
      "totalMatches": 10,
      "totalWins": 7,
      "winRate": 70.0
    }
  ],
  "error": null
}
```

## 13. Augment API

```http
GET /api/augments
```

인증 없이 호출 가능하다.

백엔드 `augments` 테이블에 저장된 증강 목록을 반환한다.

응답:

```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "Fast Reload",
      "description": "Reload speed increases by 30%.",
      "effectType": "RELOAD_SPEED"
    }
  ],
  "error": null
}
```

현재 Unity 실제 게임 증강은 `Assets/Data/KBW`의 ScriptableObject를 주로 사용한다. 백엔드 증강 API는 테스트와 데이터 조회용 성격이 더 강하다.

## 14. DB 테이블 구조

Flyway 마이그레이션은 `src/main/resources/db/migration`에 있다.

### 14.1 V1__init_schema.sql

처음 생성하는 테이블:

- `users`
- `matches`
- `augments`
- `refresh_tokens`

`users`는 이메일과 닉네임에 unique 제약이 있다.

`matches`는 player1Id, player2Id, winnerId, score, createdAt을 저장한다.

`refresh_tokens`는 refresh token 문자열과 만료 시간을 저장한다.

### 14.2 V2__add_match_detail_tables.sql

매치 상세 기록용 테이블을 추가한다.

- `match_player_stats`
- `match_player_augments`

`match_player_stats`는 한 매치 안에서 유저별 결과를 저장한다.

저장 필드:

- match_id
- user_id
- result
- score
- damage_dealt
- character_name
- created_at

`match_player_augments`는 유저가 어떤 라운드에 어떤 증강을 몇 번째로 골랐는지 저장한다.

저장 필드:

- match_id
- user_id
- augment_id
- selected_order
- selected_round
- created_at

### 14.3 V3__add_augment_name.sql

Unity 증강과 DB 증강 ID가 아직 직접 연결되지 않는 문제를 해결하기 위해 `augment_name` 컬럼을 추가하고 `augment_id`를 nullable로 바꾼다.

이 변경 덕분에 Unity가 `augmentId: 0`과 `augmentName: "Multi Shot"`을 보내도 백엔드는 증강 이름을 저장할 수 있다.

## 15. Entity 설명

### User

`users` 테이블과 매핑된다.

- id
- email
- password
- nickname
- createdAt

비밀번호는 평문이 아니라 BCrypt hash로 저장된다.

### RefreshToken

`refresh_tokens` 테이블과 매핑된다.

- id
- user
- token
- expiresAt
- createdAt

로그인할 때 유저별 기존 refresh token을 지우고 새 token을 저장한다.

### Match

`matches` 테이블과 매핑된다.

- id
- player1Id
- player2Id
- winnerId
- player1Score
- player2Score
- createdAt

이 테이블은 매치 요약을 빠르게 조회하기 위한 중심 테이블이다.

### MatchPlayerStat

`match_player_stats` 테이블과 매핑된다.

한 match와 한 user의 상세 결과를 저장한다. 같은 match/user 조합은 unique이다.

### MatchPlayerAugment

`match_player_augments` 테이블과 매핑된다.

한 유저가 매치 중 선택한 증강 기록을 저장한다. `augment` FK는 nullable이고, 대신 `augmentName`으로 Unity 증강 이름을 저장할 수 있다.

### Augment

`augments` 테이블과 매핑된다.

- id
- name
- description
- effectType

`DataInitializer`는 DB가 비어 있으면 테스트용 증강 5개를 seed한다.

## 16. 예외 처리

에러 코드는 `ErrorCode.java`에 정의되어 있다.

| 코드 | HTTP 상태 | 의미 |
| --- | --- | --- |
| `INVALID_REQUEST` | 400 | 잘못된 요청 |
| `USER_NOT_FOUND` | 404 | 유저 없음 |
| `EMAIL_ALREADY_EXISTS` | 409 | 이메일 중복 |
| `NICKNAME_ALREADY_EXISTS` | 409 | 닉네임 중복 |
| `WRONG_PASSWORD` | 401 | 비밀번호 틀림 |
| `INVALID_TOKEN` | 401 | 토큰 잘못됨 |
| `TOKEN_EXPIRED` | 401 | 토큰 만료 |
| `FORBIDDEN` | 403 | 권한 없음 |

`GlobalExceptionHandler`는 예외를 잡아서 `ApiResponse.fail(message)` 형태로 바꾼다. Unity는 HTTP 실패 시 이 `error` 값을 읽어 표시한다.

## 17. CORS와 정적 테스트 클라이언트

`WebConfig`는 모든 origin pattern을 허용한다.

```java
registry.addMapping("/**")
    .allowedOriginPatterns("*")
    .allowedMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
    .allowedHeaders("*")
    .allowCredentials(true);
```

개발 단계에서 Unity, 브라우저 테스트 클라이언트, 로컬 서버 접근을 쉽게 하기 위한 설정이다.

`src/main/resources/static/test-client.html`은 브라우저에서 백엔드 API를 직접 테스트하기 위한 정적 페이지이다. 로그인, 토큰, 매치 저장, 조회 등을 수동으로 확인할 때 사용할 수 있다.

## 18. 새 기능을 추가할 때 보는 위치

### 새 API 추가

1. 요청 DTO를 `dto` 아래에 만든다.
2. 응답 DTO를 만든다.
3. `controller`에 endpoint를 추가한다.
4. 실제 로직은 `service`에 넣는다.
5. DB가 필요하면 `entity`, `repository`, Flyway migration을 추가한다.
6. 인증이 필요한지 `SecurityConfig`를 확인한다.
7. Unity에서 호출해야 하면 `BackendModels.cs`, `BackendApiClient.cs`에 모델과 메서드를 추가한다.

### 매치 통계 추가

1. `MatchPlayerResultRequest`에 새 필드를 추가한다.
2. `match_player_stats` 또는 새 테이블에 컬럼을 추가하는 migration을 만든다.
3. `MatchPlayerStat` entity에 필드를 추가한다.
4. `MatchService.persistPlayerDetails()`에서 저장한다.
5. 히스토리 응답 DTO에도 필요한 필드를 추가한다.
6. Unity의 `MatchManager.BuildPlayerResult()`에서 값을 넣는다.

### 보안 강화

현재 개발 편의를 위해 다음 설정은 느슨하다.

- CORS 전체 허용
- H2 콘솔 허용
- refresh cookie `secure(false)`
- JWT secret이 설정 파일에 직접 있음

실제 배포에서는 CORS origin 제한, H2 콘솔 비활성화, HTTPS cookie secure 적용, JWT secret 환경변수 분리가 필요하다.

## 19. 현재 구현상 주의할 점

- 일부 Java 주석은 인코딩이 깨져 보인다. 기능에는 영향이 없지만 유지보수성을 위해 정리하는 것이 좋다.
- `MatchService` 일부 들여쓰기가 불규칙하다. 동작에는 영향이 없지만 읽기 어렵다.
- access token 만료 시 Unity가 자동으로 refresh 후 재시도하는 구조는 아직 강하지 않다. 현재는 명시적으로 `Refresh()`를 호출할 수 있는 구조이다.
- Unity 증강 ID와 DB 증강 ID가 다르기 때문에, 현재 매치 저장은 `augmentName` 중심으로 처리한다.
- `GET /api/leaderboard`는 모든 유저를 조회하고 유저별 count 쿼리를 실행한다. 유저 수가 많아지면 집계 쿼리 최적화가 필요하다.
