# Unity 구현 문서

이 문서는 Unity 쪽 구현을 처음 보는 개발자가 프로젝트 구조와 실행 흐름을 이해할 수 있도록 정리한 문서이다. Unity를 조금 다룰 줄 알고 C# 스크립트, GameObject, Prefab, Scene, MonoBehaviour 개념을 알고 있다는 전제로 설명한다.

## 1. 전체 개요

이 Unity 프로젝트는 1대1 멀티플레이어 라운드 전투 게임이다. 클라이언트는 Unity에서 화면, 입력, 캐릭터, 전투, UI, Photon Fusion 네트워킹을 담당하고, 백엔드는 계정, 토큰, 유저 정보, 매치 결과, 리더보드, 증강 데이터 조회를 담당한다.

주요 기술은 다음과 같다.

- Unity `6000.3.11f1`
- C#
- Photon Fusion
- Fusion SimpleKCC
- Unity UI / TextMesh Pro
- Unity Input System
- URP
- Spring Boot 백엔드 REST API

Unity 구현은 크게 세 영역으로 나뉜다.

- `Assets/Scripts/KBW`: 멀티플레이, 전투, 라운드 진행, 증강, HUD, 로비 UI
- `Assets/Scripts/JJH/Backend`: 백엔드 REST API 호출, 로그인 세션, 매치 결과 저장
- `Assets/Scripts/JJH/UI`: 인증 UI, 사운드 설정 UI

## 2. 주요 폴더 구조

```text
Assets/
  Scripts/
    KBW/
      FusionBootstrap.cs
      GameManager.cs
      MatchManager.cs
      PlayerNetwork.cs
      PlayerHealth.cs
      RifleProjectile.cs
      ThrowableItemProjectile.cs
      ThrowingAxeProjectile.cs
      SmokeZone.cs
      AugmentDefinition.cs
      AugmentDatabase.cs
      AugmentSelectionUI.cs
      MatchHUD.cs
      MatchResultUI.cs
      LobbyMenuUI.cs
      MainMenuFlowUI.cs
    JJH/
      Backend/
        BackendApiClient.cs
        AuthManager.cs
        BackendSession.cs
        BackendDataService.cs
        MatchResultService.cs
        BackendModels.cs
      UI/
        ProfileAuthUI.cs
        AudioSettingsUI.cs
  Data/
    KBW/
      여러 AugmentDefinition asset
  Prefabs/
    KBW/
      Player.prefab
      RifleBullet.prefab
      ThrowableItemProjectile.prefab
      ThrowingAxeProjectile.prefab
      SmokeZone.prefab
      ExplosionVFX.prefab
      RoomItemPrefab.prefab
    JJH/
      SettingsPanel.prefab
  Scenes/
    SampleScene.unity
    KBW/MultiPlayer.unity
```

`Assets/Scripts/KBW`는 게임 자체를 만든 코드이고, `Assets/Scripts/JJH/Backend`는 백엔드 서버와 연결하기 위한 코드이다. 실제 플레이 중에는 두 영역이 같이 동작한다. 예를 들어 로그인은 JJH 쪽 코드가 처리하고, 로그인한 유저 ID는 KBW 쪽 `PlayerNetwork`가 받아서 매치 결과 저장에 사용한다.

## 3. 런타임 시작 구조

몇몇 핵심 매니저는 씬에 직접 배치되어 있지 않아도 자동으로 생성된다.

`BackendApiClient`, `AuthManager`, `BackendDataService`, `MatchResultService`는 `RuntimeInitializeOnLoadMethod`를 사용해서 게임 시작 전에 Singleton GameObject를 만든다. 이렇게 한 이유는 어떤 씬에서 시작하더라도 백엔드 통신과 로그인 상태를 사용할 수 있게 하기 위해서이다.

핵심 Singleton 역할은 다음과 같다.

| 클래스 | 역할 |
| --- | --- |
| `BackendApiClient` | HTTP 요청 생성, JSON 직렬화, 토큰 헤더 추가 |
| `AuthManager` | 회원가입, 로그인, 토큰 갱신, 유저 정보 로드/수정 이벤트 관리 |
| `BackendSession` | access token, refresh token, user id, email, nickname을 `PlayerPrefs`에 저장 |
| `BackendDataService` | 리더보드와 백엔드 증강 목록 조회 |
| `MatchResultService` | 매치 결과 저장과 내 매치 히스토리 조회 |
| `GameManager` | 로컬 플레이어, 카메라, 커서 상태, 현재 매치 상태 연결 |
| `FusionBootstrap` | Photon Fusion 로비, 방 생성/입장, 플레이어 스폰, 입력 전달 |
| `MatchManager` | 라운드 상태 머신, 승패 판정, 증강 선택, 매치 결과 저장 트리거 |

## 4. 메뉴와 로그인 흐름

메인 메뉴는 `MainMenuFlowUI.cs`가 담당한다. 메뉴는 다음 순서로 흐른다.

1. 타이틀 화면
2. 로그인/회원가입 화면
3. 캐릭터 선택 및 닉네임 입력 화면
4. 로비 화면

`MainMenuFlowUI`는 여러 패널 GameObject를 켜고 끄면서 화면을 전환한다. `playButton`을 누르면 프로필 인증 화면으로 이동하고, 로그인 또는 회원가입이 성공하면 캐릭터 선택 화면으로 이동한다.

로그인 UI는 `ProfileAuthUI.cs`가 담당한다.

- 이메일과 비밀번호 입력값을 읽는다.
- 로그인 버튼은 `AuthManager.Login()`을 호출한다.
- 회원가입 버튼은 `AuthManager.Signup()`을 호출한다.
- 성공하면 팝업에 `User verified.`를 보여주고 `OnProceedRequested` 이벤트를 발생시킨다.
- 실패하면 서버 에러를 사람이 보기 쉬운 메시지로 바꿔서 팝업에 표시한다.

캐릭터 선택 화면에서는 `LocalPlayerProfile`에 닉네임, 캐릭터 ID, 캐릭터 이름을 저장한다. 이 값은 나중에 Photon Fusion 플레이어가 스폰될 때 `PlayerNetwork.RPC_RequestApplyProfile()`로 서버 권한 쪽에 전달된다.

## 5. 백엔드 로그인 세션

`BackendSession.cs`는 Unity 클라이언트 안에서 로그인 상태를 보관한다.

저장하는 값은 다음과 같다.

- `AccessToken`
- `RefreshToken`
- `UserId`
- `Email`
- `Nickname`

이 값들은 `PlayerPrefs`에 들어간다. 게임을 껐다 켜도 이전 토큰과 유저 정보가 남도록 만든 구조이다.

`BackendApiClient`는 인증이 필요한 API를 호출할 때 다음 헤더를 붙인다.

```text
Authorization: Bearer {accessToken}
```

기본 백엔드 주소는 `http://15.164.171.132:8080`이고, `BackendApiClient.BaseUrl`로 변경할 수 있다. 변경된 주소는 `PlayerPrefs`에 저장된다.

## 6. Photon Fusion 로비와 방 구조

멀티플레이 로비와 방 입장은 `FusionBootstrap.cs`가 담당한다.

주요 설정값은 다음과 같다.

- `customLobbyName`: `LastRound_Lobby`
- `maxPlayersPerRoom`: 2
- `defaultRoomPrefix`: `LastRound`
- `playerPrefab`: 네트워크로 스폰할 플레이어 프리팹
- `objectProvider`: 네트워크 오브젝트 풀

로비 진입은 `LobbyMenuUI.OnEnable()`에서 시작된다. `bootstrap.JoinLobby()`를 호출하면 Fusion 커스텀 로비에 접속하고, Fusion이 방 목록을 알려줄 때 `OnSessionListUpdated()`가 호출된다. 그 결과는 `LobbyMenuUI.RefreshRoomList()`로 전달되어 방 리스트 UI에 표시된다.

방 생성 흐름은 다음과 같다.

1. 유저가 방 이름을 입력하고 Create 버튼을 누른다.
2. `LobbyMenuUI.OnCreateRoomClicked()`가 `FusionBootstrap.CreateRoom()`을 호출한다.
3. 방 이름이 비어 있으면 `LastRound_랜덤숫자` 형식으로 만든다.
4. 중복 방 이름이 있으면 생성하지 않는다.
5. `StartSession(GameMode.Host, roomName)`으로 Host 세션을 시작한다.

방 입장 흐름은 다음과 같다.

1. 방 목록 아이템은 `LobbyRoomItemUI`로 만들어진다.
2. Join 버튼을 누르면 `FusionBootstrap.JoinRoom(session)`이 호출된다.
3. 방이 열려 있고 인원이 꽉 차지 않았으면 `StartSession(GameMode.Client, roomName)`으로 접속한다.

세션이 시작되면 `FusionBootstrap.GameSessionStarted` 이벤트가 발생하고, `LobbyMenuUI.HideLobby()`가 로비 UI를 숨긴다.

## 7. 플레이어 스폰

Fusion에서 플레이어가 들어오면 `FusionBootstrap.OnPlayerJoined()`가 호출된다. 서버 권한 쪽에서만 플레이어를 스폰한다.

스폰 위치는 슬롯 기준으로 나뉜다.

- 슬롯 0: `(-3, 1, 0)`
- 슬롯 1: `(3, 1, 0)`

스폰할 때 `PlayerNetwork.ServerInitialize(slot)`이 호출된다. 이 함수는 플레이어의 초기 네트워크 상태를 세팅한다.

초기화되는 대표 값은 다음과 같다.

- 슬롯 번호
- 캐릭터 ID
- 플레이어 이름
- 백엔드 유저 ID
- 체력/사망 상태
- 이동/점프/대시 상태
- 총 발사 쿨다운
- 증강 관련 누적 값
- 액티브 아이템 기본값

로컬 플레이어는 `PlayerNetwork.Spawned()`에서 `GameManager.RegisterLocalPlayer()`로 등록된다. 등록되면 카메라가 해당 플레이어에 바인딩되고, 커서 상태도 현재 매치 상태에 맞게 바뀐다.

## 8. 입력 처리

입력은 `FusionBootstrap.OnInput()`에서 Fusion 네트워크 입력으로 변환된다.

사용하는 입력은 다음과 같다.

| 입력 | 의미 |
| --- | --- |
| WASD | 이동 |
| Mouse X/Y | 시점 회전 |
| Mouse Left | 발사 |
| Mouse Right | 보조 발사 자리 |
| Left Shift | 대시 |
| Space | 점프 |
| F | 액티브 아이템 사용 |
| R | 재장전 자리 |
| 1/2/3 | 증강 카드 선택 |

`GameplayInput` 구조체에는 다음 값이 들어간다.

- `Move`: 이동 방향
- `Look`: 마우스 회전 입력
- `AimOrigin`: 카메라 위치
- `AimDirection`: 카메라 정면 방향
- `Buttons`: Fusion `NetworkButtons`

`dashPressed`, `jumpPressed`, `abilityPressed` 같은 값은 `Update()`에서 `GetKeyDown`으로 버퍼링한다. 이유는 Fusion의 `OnInput()` 호출 타이밍과 Unity `Update()` 타이밍이 다르기 때문이다. 한 프레임짜리 입력을 놓치지 않으려면 먼저 bool로 저장해 두고 `OnInput()`에서 전송한 뒤 초기화하는 구조가 필요하다.

입력은 항상 받는 것이 아니라 현재 상태에 따라 차단된다.

- 현재 매치가 `Playing`이 아니면 입력을 비운다.
- 메뉴나 UI 커서 상태면 입력을 비운다.
- `GameManager.BlocksGameplayInput`가 true면 입력을 비운다.

## 9. 게임 상태 머신

매치 진행은 `MatchManager.cs`와 `MatchPhase.cs`가 담당한다.

상태는 다음과 같다.

```text
Lobby
  -> ChoosingAugment
  -> RoundIntro
  -> Playing
  -> RoundResult
  -> ChoosingAugment 또는 MatchResult
```

각 상태의 의미는 다음과 같다.

| 상태 | 의미 |
| --- | --- |
| `Lobby` | 플레이어가 모이기를 기다리는 상태 |
| `ChoosingAugment` | 이번 라운드 전 증강을 고르는 상태 |
| `RoundIntro` | 라운드 시작 전 짧은 안내/준비 상태 |
| `Playing` | 실제 전투 입력과 피해 판정이 가능한 상태 |
| `RoundResult` | 한 라운드 승패를 보여주는 상태 |
| `MatchResult` | 전체 매치 승패를 보여주고 백엔드 저장을 처리하는 상태 |

`MatchManager.FixedUpdateNetwork()`는 서버 권한 쪽에서만 상태를 진행한다. `HasStateAuthority`가 아닌 클라이언트는 상태를 직접 바꾸지 않는다. 이것이 중요한 이유는 멀티플레이에서 승패, 점수, 라운드 진행은 한 곳에서만 결정해야 동기화 문제가 줄어들기 때문이다.

기본 룰은 다음과 같다.

- 필요한 플레이어 수: `playersRequiredToStart`
- 승리 필요 라운드 수: `roundsToWin`, 현재 값은 3
- 라운드 시작 안내 시간: `roundIntroSeconds`
- 라운드 결과 표시 시간: `roundResultSeconds`

라운드 승리 처리는 `RegisterRoundWin(winnerSlot)`에서 한다.

1. 승리 슬롯을 `RoundWinnerSlot`에 저장한다.
2. `Player0Wins` 또는 `Player1Wins`를 증가시킨다.
3. 누군가 `roundsToWin`에 도달했으면 `MatchWinnerSlot`을 세팅한다.
4. `RoundResult` 상태로 넘어간다.

## 10. 아레나 선택과 스폰

아레나는 `ArenaZone.cs`로 표현된다. 각 아레나에는 플레이어 0, 플레이어 1 스폰 Transform이 있고, 선택된 아레나만 boundary를 켠다.

`MatchManager`는 라운드마다 `ChooseArenaForRound()`를 호출한다.

- 사용 가능한 아레나 인덱스를 `unusedArenaIndices`에 넣는다.
- 랜덤으로 하나를 뽑는다.
- 뽑은 아레나는 목록에서 제거한다.
- `ActiveArenaIndex`를 네트워크 변수로 저장한다.
- 모든 클라이언트는 `Render()`에서 `ActiveArenaIndex` 변화를 감지하고 시각 상태를 갱신한다.

라운드 시작 전에 `ResetAllPlayersForRound()`가 각 플레이어의 `ResetForRound(spawnPosition, yaw)`를 호출한다. `PlayerNetwork`는 이때 위치, 회전, 체력, 발사 쿨다운, 대시 상태, 액티브 아이템 사용 횟수를 초기화한다.

## 11. 플레이어 네트워크 구조

`PlayerNetwork.cs`는 플레이어 한 명의 핵심 네트워크 상태와 행동을 담당한다. 이 클래스는 매우 많은 역할을 갖고 있으므로 기능별로 이해하는 것이 좋다.

주요 역할은 다음과 같다.

- 이동과 시점 회전
- 점프와 대시
- 소총 발사
- 액티브 아이템 사용
- 증강 효과 적용
- 캐릭터 외형과 이름 동기화
- 백엔드 유저 ID 동기화
- 라운드 리셋
- 히트 확인 카운터 관리

중요한 네트워크 변수는 다음과 같다.

| 변수 | 의미 |
| --- | --- |
| `SlotIndex` | 0번 플레이어인지 1번 플레이어인지 |
| `CharacterId` | 선택한 캐릭터 ID |
| `PlayerName` | 표시용 플레이어 이름 |
| `CharacterDisplayName` | 선택한 캐릭터 표시 이름 |
| `BackendUserId` | 백엔드 DB의 유저 ID |
| `LookYaw`, `LookPitch` | 네트워크 동기화되는 시점 각도 |
| `MoveX`, `MoveY`, `MoveAmount` | 애니메이션용 이동 값 |
| `IsDead` | 사망 여부 |
| `HitConfirmCount` | 명중 UI 표시용 카운터 |
| `OfferedAugmentId0/1/2` | 현재 선택 가능한 증강 후보 |
| `AugmentHistoryIds` | 매치 중 선택한 증강 ID 기록 |
| `AugmentHistoryRounds` | 각 증강을 몇 라운드에 골랐는지 기록 |

### 11.1 이동

이동은 `FixedUpdateNetwork()`에서 처리된다. Fusion 입력으로 받은 `GameplayInput.Move`를 SimpleKCC 기준 방향으로 변환하고, `kcc.Move()`를 호출한다.

대시 입력이 들어오면 `StartDash()`가 대시 방향, 대시 지속 시간, 쿨다운을 네트워크 타이머로 저장한다. 대시 중에는 일반 이동 속도 대신 `dashSpeed`를 사용한다.

점프는 `Space` 입력이 들어왔고 KCC가 지면에 있을 때만 적용된다. 점프가 발생하면 `JumpAnimCount`를 증가시켜 애니메이터 트리거를 실행할 수 있게 한다.

### 11.2 시점과 카메라

시점 회전은 `LookYaw`, `LookPitch`에 저장된다. `LocalCamera.cs`는 로컬 플레이어의 이 값을 읽어서 카메라 위치와 회전을 맞춘다.

카메라는 1인칭과 3인칭을 지원한다.

- `V` 키로 1인칭/3인칭 전환
- 3인칭에서는 벽에 카메라가 파묻히지 않도록 SphereCast로 충돌 보정
- 발사 카운트가 증가하면 반동 효과 적용

`PlayerView.SetFirstPersonVisual()`은 1인칭일 때 자기 캐릭터 렌더러를 숨겨 카메라 앞을 가리지 않도록 한다.

### 11.3 발사

발사는 `HoldFire()`에서 처리된다.

흐름은 다음과 같다.

1. 서버 권한인지 확인한다.
2. 매치 상태가 `Playing`인지 확인한다.
3. 플레이어가 죽지 않았는지 확인한다.
4. 발사 쿨다운이 끝났는지 확인한다.
5. 카메라 기준 Raycast로 조준 지점을 찾는다.
6. 총구 위치에서 조준 지점 방향으로 투사체 방향을 만든다.
7. 증강 효과를 반영해서 투사체 개수, 퍼짐, 크기, 속도, 피해량을 계산한다.
8. `Runner.Spawn()`으로 `RifleProjectile` 네트워크 프리팹을 생성한다.
9. 생성된 투사체에 `Init()`으로 계산된 전투 값을 전달한다.

기본 피해량은 `rifleDamage`이고, 증강에 따라 `ProjectileDamageMultiplier`, `ProjectileExtraProjectiles`, `ProjectileSpreadAngle` 등이 적용된다.

### 11.4 액티브 아이템

액티브 아이템은 `CurrentActiveItem`으로 관리된다.

지원하는 타입은 다음과 같다.

- `MedKit`
- `Grenade`
- `SmokeBomb`
- `ThrowingAxe`

`F` 키를 누르면 `UseAbility()`가 호출되고, 현재 아이템 타입에 따라 다음 동작을 한다.

- MedKit: `PlayerHealth.Heal()`로 회복
- Grenade: `ThrowableItemProjectile`을 수류탄으로 스폰
- SmokeBomb: `ThrowableItemProjectile`을 연막탄으로 스폰
- ThrowingAxe: `ThrowingAxeProjectile`을 스폰

던지는 도끼는 사용 후 `ActiveItemUsesRemaining`이 0이 되고, 주운 뒤 `RestoreThrowingAxe()`로 다시 1이 된다.

## 12. 체력과 피해

체력은 `PlayerHealth.cs`가 담당한다.

주요 값은 다음과 같다.

- `CurrentHealth`
- `MaxHealth`
- `IsAlive`
- `DamageFeedbackCount`
- `IsPoisonedNet`

피해는 `TakeDamage(damage, attacker)`로 들어온다. 이 함수는 서버 권한에서만 실제 체력을 줄인다. 체력이 0이 되면 `Die()`가 호출되고, `MatchManager.ReportPlayerDefeated(owner)`로 라운드 승패 판정이 넘어간다.

독 상태는 `ApplyPoison()`으로 적용된다. 독은 별도 TickTimer를 사용해서 일정 간격마다 피해를 준다. 독이 끝나면 독 관련 네트워크 상태를 초기화한다.

`DamageFeedbackCount`는 로컬 UI에서 피격 화면 효과를 표시하기 위한 카운터이다. 값이 증가하면 `CombatFeedbackUI`가 빨간 플래시와 사운드를 재생한다.

## 13. 투사체와 전투 효과

### 13.1 RifleProjectile

`RifleProjectile.cs`는 소총 탄환 네트워크 오브젝트이다.

처리하는 기능은 다음과 같다.

- 직선 이동
- SphereCast 충돌 검사
- 플레이어/더미 피해
- 방패 차단 검사
- 관통
- 벽 튕김
- 적 방향 보정 튕김
- 거리 기반 피해 증가
- 폭발 피해
- 독 적용
- 독 구름 생성
- 탄환 크기와 독 색상 시각화

탄환은 `lifeSeconds`가 지나면 자동으로 despawn된다. 충돌 시에는 피해 적용 후 조건에 따라 계속 진행하거나 사라진다.

### 13.2 ThrowableItemProjectile

`ThrowableItemProjectile.cs`는 수류탄과 연막탄을 하나의 클래스로 처리한다.

`ThrowableItemKind` 값에 따라 동작이 달라진다.

- `Grenade`: 폭발 반경 안의 대상에게 피해를 주고 폭발 VFX를 스폰한다.
- `SmokeBomb`: `SmokeZone`을 스폰한다.

던진 직후 자기 자신의 콜라이더에 맞는 것을 방지하기 위해 owner ID를 저장하고, 충돌 검사에서 자기 자신이면 무시한다.

### 13.3 ThrowingAxeProjectile

`ThrowingAxeProjectile.cs`는 도끼 투척 아이템이다.

동작은 다음과 같다.

1. 앞으로 날아가며 중력 영향을 받는다.
2. 플레이어나 더미에 맞으면 피해를 준다.
3. 플레이어에 맞으면 근처 바닥에 떨어진다.
4. 벽이나 지형에 맞으면 그 위치에 박힌다.
5. 일정 시간이 지나거나 주인이 가까이 오면 회수된다.

도끼는 회수되어야 다시 사용할 수 있으므로, 일반 수류탄과 달리 `RestoreThrowingAxe()` 흐름이 있다.

### 13.4 SmokeZone

`SmokeZone.cs`는 연막 또는 독 구름 영역이다.

일반 연막은 시각 효과 중심이고, 독 구름은 범위 안 대상에게 `ApplyPoison()`을 반복 적용한다. 독 구름의 피해 판정 반경은 즉시 최종 크기가 되지 않고 `toxicDamageGrowthSeconds` 동안 서서히 커진다. 이 처리는 갑자기 넓은 독 판정이 생겨 불공정하게 느껴지는 것을 줄이기 위한 구조이다.

## 14. 증강 시스템

증강 데이터는 `AugmentDefinition.cs`로 만든 ScriptableObject이다. 실제 데이터 asset은 `Assets/Data/KBW`에 있다.

현재 들어 있는 증강 asset 예시는 다음과 같다.

- Big Round
- Buckshot
- Drill Round
- Explosive Payload
- Fastball
- Grenade Kit
- Grow Shot
- Heavy Slug
- Multi Shot
- Poison Coating
- Rapid Barrel
- Ricochet Rounds
- Shield Orbit
- Smoke Kit
- Spinning Knife
- Target Bounce
- Throwing Axe
- Toxic Cloud

`AugmentDefinition`에는 다음 계열의 값이 있다.

- 신원 정보: id, displayName, description, icon, rarity, category
- 탄환 형태: 추가 탄환 수, 퍼짐 각도, 크기, 속도, 피해 배율, 발사 간격 배율
- 탄환 행동: 튕김, 관통, 적 방향 보정, 거리 기반 피해 증가
- 상태 이상: 독 피해량, 독 지속시간
- 폭발/장판: 폭발 반경, 폭발 피해 배율, 독 구름 반경/지속시간
- 보조 장신구: 궤도 방패, 궤도 근접 무기, 피해량, 회전 속도
- 액티브 아이템 교체: 수류탄, 연막탄, 도끼, 회복량

증강 후보는 `MatchManager.AssignAugmentsByRoundRule()`에서 뽑는다.

- 첫 라운드는 양쪽 플레이어 모두 선택한다.
- 이후 라운드는 이전 라운드 패배자만 선택한다.
- 각 선택자는 중복되지 않는 3개 후보를 받는다.
- 후보 ID는 `PlayerNetwork.OfferedAugmentId0/1/2`에 저장된다.

선택 UI는 `AugmentSelectionUI.cs`가 담당한다.

1. 현재 상태가 `ChoosingAugment`인지 확인한다.
2. 로컬 플레이어가 선택 가능한지 확인한다.
3. 후보 ID로 `AugmentDatabase.GetById()`를 호출해 ScriptableObject를 찾는다.
4. `AugmentCardUI.Bind()`로 카드 UI를 채운다.
5. 카드를 클릭하면 `PlayerNetwork.RPC_RequestSelectAugment(slotIndex)`를 호출한다.

선택 요청은 RPC로 서버 권한에 전달된다. 서버 권한은 실제 증강을 적용하고, 선택 기록을 `AugmentHistoryIds`, `AugmentHistoryRounds`에 저장한다.

## 15. 매치 결과 UI와 저장

전체 매치가 끝나면 `MatchManager.EnterMatchResultPhase()`가 호출된다. 이때 `ReportMatchResultToBackend()`를 실행해서 백엔드 저장을 시도한다.

저장할 데이터는 `BuildMatchResultRequest()`에서 만든다.

포함되는 값은 다음과 같다.

- player1Id
- player2Id
- winnerId
- player1Score
- player2Score
- 플레이어별 결과
- 플레이어별 캐릭터 이름
- 플레이어별 선택 증강 이름

중요한 점은 Unity 증강 ID와 백엔드 `augments` 테이블 ID가 현재 직접 매핑되어 있지 않다는 것이다. 그래서 Unity는 매치 저장 시 `augmentId`를 0으로 보내고, `augmentName`에 Unity의 `displayName`을 넣는다. 백엔드는 `augment_id`가 null이어도 `augment_name`을 저장할 수 있게 만들어져 있다.

결과 화면은 `MatchResultUI.cs`가 담당한다.

- 승자 닉네임
- 양쪽 점수
- 선택 캐릭터
- 선택 증강 목록
- 매치 시간
- 백엔드 저장 성공/실패 상태
- 로비로 돌아가기 버튼
- 자동 로비 복귀 카운트다운

저장이 너무 오래 걸리면 `showResultTimeoutSeconds` 후 결과 화면을 강제로 보여준다. 이렇게 하면 백엔드 저장 실패가 있어도 유저가 결과 화면에서 멈추지 않는다.

## 16. HUD와 피드백 UI

전투 중 UI는 여러 클래스로 나뉜다.

| 클래스 | 역할 |
| --- | --- |
| `MatchHUD` | 체력, 라운드, 점수, 중앙 메시지, 액티브 아이템 표시 |
| `CrosshairUI` | Playing 상태에서만 조준점 표시 |
| `CombatFeedbackUI` | 명중 마커, 피격 플래시, 효과음 |
| `MatchResultUI` | 최종 결과 화면 |
| `AudioSettingsUI` | BGM/SFX 볼륨 저장 및 적용 |

`CombatFeedbackUI`는 직접 피해 이벤트를 받지 않고 카운터 변화를 본다. 내가 적을 맞히면 `PlayerNetwork.HitConfirmCount`가 증가하고, 내가 피해를 받으면 `PlayerHealth.DamageFeedbackCount`가 증가한다. UI는 이전 값과 현재 값을 비교해서 새 이벤트를 감지한다.

## 17. 커서와 입력 차단

`CursorController.cs`는 커서 상태를 세 가지로 관리한다.

- `Menu`: 커서 보임, 잠금 없음
- `Gameplay`: 커서 숨김, 화면 중앙 잠금
- `UI`: 커서 보임, 잠금 없음

`GameManager.SyncCursorWithPhase()`는 매치 상태에 따라 커서를 바꾼다.

- Playing: Gameplay 커서
- ChoosingAugment: UI 커서
- 그 외: Menu 커서

`CursorController.BlocksGameplayInput`는 현재 커서가 Gameplay이 아니면 true를 반환한다. `FusionBootstrap.OnInput()`은 이 값을 보고 전투 입력을 비운다.

## 18. 오브젝트 풀

`PooledNetworkObjectProvider.cs`는 Fusion 네트워크 프리팹용 오브젝트 풀이다. 투사체처럼 자주 생성되고 사라지는 오브젝트를 매번 Instantiate/Destroy하면 비용이 커질 수 있기 때문에 풀링 구조를 사용한다.

동작은 다음과 같다.

1. Fusion이 네트워크 프리팹 인스턴스를 요청한다.
2. 풀에 쉬고 있는 인스턴스가 있으면 꺼내서 활성화한다.
3. 없으면 새로 Instantiate한다.
4. despawn 시 파괴하지 않고 비활성화해서 큐에 넣는다.
5. 프리팹별 최대 풀 개수를 넘으면 파괴한다.

`FusionBootstrap.CreateRunnerIfNeeded()`에서 이 provider를 runner의 `StartGameArgs.ObjectProvider`로 넘긴다.

## 19. 로비 복귀와 연결 종료 처리

매치가 끝나거나 연결이 끊기면 `FusionBootstrap.ReturnToLobby()` 또는 `ShutdownAndReturnToLobby()`가 실행된다.

이 부분은 단순히 화면만 바꾸는 것이 아니라 네트워크 runner를 정리해야 한다.

중요한 처리:

- runner가 살아 있으면 `runner.Shutdown()` 호출
- callback 제거
- `NetworkRunner`, `NetworkSceneManagerDefault` 컴포넌트 제거
- 스폰된 플레이어 목록 초기화
- 방 목록 캐시 초기화
- 로비 UI 다시 표시
- 필요하면 다음 프레임에 로비 재접속

주석에도 적혀 있듯이, runner가 정리되기 전에 새 runner를 만들면 같은 GameObject에 runner가 중복되어 Fusion이 오동작할 수 있다. 그래서 cleanup 이후 약간 기다렸다가 `JoinLobby()`를 다시 호출하는 구조가 들어가 있다.

## 20. 새 기능을 추가할 때 보는 위치

### 새 증강 추가

1. `Assets/Data/KBW`에 새 `AugmentDefinition` asset을 만든다.
2. `AugmentDatabase`가 들고 있는 리스트에 추가한다.
3. 단순 수치 변경이면 `PlayerNetwork.ApplyAugment()`에 이미 있는 필드를 사용한다.
4. 완전히 새로운 효과라면 `AugmentDefinition`에 필드를 추가하고, `PlayerNetwork.ApplyAugment()`와 투사체/체력 로직 중 필요한 곳에 반영한다.

### 새 액티브 아이템 추가

1. `ActiveItemType` enum에 타입을 추가한다.
2. `AugmentDefinition`에서 선택 가능하게 만든다.
3. `PlayerNetwork.UseAbility()`에 새 case를 추가한다.
4. 필요하면 새 NetworkBehaviour 투사체/장판 클래스를 만든다.
5. HUD 아이콘과 이름은 `MatchHUD`에 추가한다.

### 매치 결과에 새 통계 추가

1. Unity `BackendModels.cs`의 `MatchPlayerResultRequest`에 필드를 추가한다.
2. `MatchManager.BuildPlayerResult()`에서 값을 채운다.
3. 백엔드 DTO, Entity, Migration, Service 저장 로직을 같이 수정한다.

### 새 UI 화면 추가

1. 씬 Canvas에 패널을 추가한다.
2. 패널을 관리할 UI 스크립트를 만든다.
3. `MainMenuFlowUI` 또는 관련 UI 컨트롤러에 패널 전환 코드를 연결한다.
4. 전투 입력을 막아야 하는 화면이면 `GameManager.SetUICursor()`를 사용한다.

## 21. 현재 구현상 주의할 점

- 일부 C# 주석과 문자열은 인코딩이 깨져 보인다. 기능에는 직접 영향이 없지만 문서화나 유지보수 시 정리하면 좋다.
- `PlayerNetwork`가 많은 책임을 갖고 있다. 기능이 더 커지면 이동, 전투, 증강, 프로필 동기화를 별도 컴포넌트로 나눌 수 있다.
- Unity 증강 ID와 백엔드 증강 ID가 아직 직접 매핑되지 않는다. 현재는 매치 저장 시 이름 중심으로 저장한다.
- `BackendSession`은 토큰을 `PlayerPrefs`에 저장한다. 실제 서비스 수준 보안이 필요하면 저장 방식을 다시 검토해야 한다.
- `MatchResultUI`는 저장 타임아웃이 있어 UX는 막히지 않지만, 실패 원인을 유저에게 자세히 보여주지는 않는다.
