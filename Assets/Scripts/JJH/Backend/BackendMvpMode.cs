using System.Text;
using UnityEngine;

public class BackendMvpMode : MonoBehaviour
{
    private const int WindowId = 8081;

    [SerializeField] private bool visible = true;

    private Rect windowRect = new Rect(20f, 20f, 520f, 700f);
    private Vector2 scroll;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle smallStyle;
    private GUIStyle okStyle;
    private GUIStyle badStyle;
    private int lastToggleFrame = -1;

    private string baseUrl = "http://localhost:8080";
    private string email = "player1@test.com";
    private string password = "password123";
    private string nickname = "player1";
    private string newNickname = "player1_new";
    private string player1Id = "1";
    private string player2Id = "2";
    private string winnerId = "1";
    private string player1Score = "3";
    private string player2Score = "1";
    private string log = "애들아 안녕! 나는 정재훈이야..지금 시간나서 만들고 있어!!! 이거 꽤 어렵다\n\n백엔드 MVP 패널 준비됨\n1. 백엔드 서버를 켠다\n2. 서버 주소 적용\n3. 회원가입 또는 로그인\n4. 조회/저장 버튼 테스트\n\nF8 또는 오른쪽 위 닫기 버튼: 패널 숨기기/보이기";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<BackendMvpMode>() != null)
            return;

        GameObject obj = new GameObject("BackendMvpMode");
        DontDestroyOnLoad(obj);
        obj.AddComponent<BackendMvpMode>();
    }

    private void Start()
    {
        if (BackendApiClient.Instance != null)
            baseUrl = BackendApiClient.Instance.BaseUrl;

        if (!string.IsNullOrWhiteSpace(BackendSession.Email))
            email = BackendSession.Email;

        if (!string.IsNullOrWhiteSpace(BackendSession.Nickname))
        {
            nickname = BackendSession.Nickname;
            newNickname = BackendSession.Nickname;
        }

        if (BackendSession.UserId > 0)
        {
            player1Id = BackendSession.UserId.ToString();
            winnerId = player1Id;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
            ToggleVisible();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F8)
        {
            ToggleVisible();
            Event.current.Use();
        }

        if (!visible)
            return;

        BuildStyles();
        windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "백엔드 연결 테스트");
    }

    private void DrawWindow(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(500f), GUILayout.Height(650f));

        GUILayout.Label("백엔드 MVP 모드", titleStyle);
        GUILayout.Label("애들아 안녕! 나는 정재훈이야..지금 mse시간에 만들고 있어!!! 이거 꽤 어렵다", smallStyle);
        GUILayout.Label("그래서 이 창은 백엔드랑 Unity가 잘 연결되는지 버튼으로 하나씩 확인하려고 만든 테스트 화면이야. 순서대로 눌러보면 돼.", smallStyle);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("창 닫기", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            visible = false;
            return;
        }
        GUILayout.EndHorizontal();

        DrawSection("0단계. 서버 주소");
        DrawLabel("서버 주소");
        baseUrl = GUILayout.TextField(baseUrl);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("서버 주소 적용", GUILayout.Height(30f)))
        {
            BackendApiClient.Instance.BaseUrl = baseUrl;
            AppendLog("서버 주소 적용", baseUrl);
        }
        if (GUILayout.Button("서버 연결 확인", GUILayout.Height(30f)))
            PingServer();
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        DrawLoginStatus();

        DrawSection("1단계. 회원가입 / 로그인");
        DrawLabel("이메일");
        email = GUILayout.TextField(email);
        DrawLabel("비밀번호");
        password = GUILayout.PasswordField(password, '*');
        DrawLabel("닉네임");
        nickname = GUILayout.TextField(nickname);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("회원가입", GUILayout.Height(32f)))
            Signup();
        if (GUILayout.Button("로그인", GUILayout.Height(32f)))
            Login();
        if (GUILayout.Button("토큰 갱신", GUILayout.Height(32f)))
            Refresh();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("내 정보 가져오기", GUILayout.Height(30f)))
            GetMe();
        if (GUILayout.Button("로컬 로그아웃", GUILayout.Height(30f)))
        {
            BackendSession.Clear();
            AppendLog("로컬 로그아웃", "Unity에 저장된 토큰을 지웠어. 서버 계정은 삭제되지 않아.");
        }
        GUILayout.EndHorizontal();

        DrawSection("2단계. 내 정보 수정");
        DrawLabel("새 닉네임");
        newNickname = GUILayout.TextField(newNickname);
        if (GUILayout.Button("닉네임 변경", GUILayout.Height(30f)))
            UpdateNickname();

        DrawSection("3단계. 서버 데이터 조회");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("리더보드 보기", GUILayout.Height(32f)))
            GetLeaderboard();
        if (GUILayout.Button("증강 목록 보기", GUILayout.Height(32f)))
            GetAugments();
        GUILayout.EndHorizontal();

        DrawSection("4단계. 매치 결과 저장");
        GUILayout.Label("player1Id/player2Id는 백엔드 유저 번호야. 로그인하면 내 번호를 자동으로 1P에 넣어줘.", smallStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("내 ID를 1P에 넣기"))
            FillMyIdAsPlayer1();
        if (GUILayout.Button("내 ID를 승자로 넣기"))
            FillMyIdAsWinner();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        DrawInlineLabel("1P ID");
        player1Id = GUILayout.TextField(player1Id);
        DrawInlineLabel("2P ID");
        player2Id = GUILayout.TextField(player2Id);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        DrawInlineLabel("승자 ID");
        winnerId = GUILayout.TextField(winnerId);
        DrawInlineLabel("1P 점수");
        player1Score = GUILayout.TextField(player1Score);
        DrawInlineLabel("2P 점수");
        player2Score = GUILayout.TextField(player2Score);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1P 승리 예시"))
            FillWinner(true);
        if (GUILayout.Button("2P 승리 예시"))
            FillWinner(false);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("매치 결과 서버에 저장", GUILayout.Height(34f)))
            PostMatchResult();

        if (GUILayout.Button("내 매치 기록 보기", GUILayout.Height(30f)))
            GetHistory();

        DrawSection("결과 로그");
        GUILayout.TextArea(log, GUILayout.MinHeight(210f));

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private void BuildStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        if (sectionStyle == null)
        {
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.65f, 0.9f, 1f) }
            };
        }

        if (smallStyle == null)
        {
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.84f, 0.9f, 1f) }
            };
        }

        if (okStyle == null)
        {
            okStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 1f, 0.55f) }
            };
        }

        if (badStyle == null)
        {
            badStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.45f, 0.45f) }
            };
        }
    }

    private void DrawSection(string text)
    {
        GUILayout.Space(12f);
        GUILayout.Label(text, sectionStyle);
    }

    private void DrawLabel(string text)
    {
        GUILayout.Label(text, smallStyle);
    }

    private void DrawInlineLabel(string text)
    {
        GUILayout.Label(text, GUILayout.Width(58f));
    }

    private void DrawLoginStatus()
    {
        if (BackendSession.IsLoggedIn)
            GUILayout.Label($"로그인 상태: {BackendSession.UserId}번 / {BackendSession.Nickname} / {BackendSession.Email}", okStyle);
        else
            GUILayout.Label("로그인 상태: 아직 로그인 안 됨", badStyle);
    }

    private void ToggleVisible()
    {
        if (lastToggleFrame == Time.frameCount)
            return;

        visible = !visible;
        lastToggleFrame = Time.frameCount;
    }

    private void Signup()
    {
        BackendApiClient.Instance.Signup(new SignupRequest
        {
            email = email.Trim(),
            password = password,
            nickname = nickname.Trim()
        }, auth =>
        {
            SyncUserFields(auth);
            AppendLog("회원가입 성공", FormatAuth(auth));
        }, AppendError);
    }

    private void Login()
    {
        BackendApiClient.Instance.Login(new LoginRequest
        {
            email = email.Trim(),
            password = password
        }, auth =>
        {
            SyncUserFields(auth);
            AppendLog("로그인 성공", FormatAuth(auth));
        }, AppendError);
    }

    private void Refresh()
    {
        BackendApiClient.Instance.Refresh(auth =>
        {
            SyncUserFields(auth);
            AppendLog("토큰 갱신 성공", FormatAuth(auth));
        }, AppendError);
    }

    private void PingServer()
    {
        BackendApiClient.Instance.GetAugments(augments =>
        {
            int count = augments != null ? augments.Count : 0;
            AppendLog("서버 연결 성공", $"서버에서 증강 데이터 {count}개를 받아왔어.");
        }, AppendError);
    }

    private void GetMe()
    {
        BackendApiClient.Instance.GetMe(user =>
        {
            player1Id = user.id.ToString();
            winnerId = player1Id;
            newNickname = user.nickname;
            AppendLog("내 정보 조회 성공", $"유저 번호: {user.id}\n이메일: {user.email}\n닉네임: {user.nickname}");
        }, AppendError);
    }

    private void UpdateNickname()
    {
        BackendApiClient.Instance.UpdateUser(new UserUpdateRequest { nickname = newNickname.Trim() }, user =>
        {
            nickname = user.nickname;
            AppendLog("닉네임 변경 성공", $"유저 번호: {user.id}\n새 닉네임: {user.nickname}");
        }, AppendError);
    }

    private void GetLeaderboard()
    {
        BackendApiClient.Instance.GetLeaderboard(entries =>
        {
            StringBuilder builder = new StringBuilder();
            if (entries != null)
            {
                foreach (LeaderboardEntry entry in entries)
                    builder.AppendLine($"{entry.userId}번 {entry.nickname} | 승리 {entry.totalWins} / 경기 {entry.totalMatches} | 승률 {entry.winRate}%");
            }
            AppendLog("리더보드 조회 성공", builder.Length == 0 ? "아직 데이터가 없어." : builder.ToString());
        }, AppendError);
    }

    private void GetAugments()
    {
        BackendApiClient.Instance.GetAugments(augments =>
        {
            StringBuilder builder = new StringBuilder();
            if (augments != null)
            {
                foreach (AugmentResponse augment in augments)
                    builder.AppendLine($"{augment.id}번 {augment.name} | 타입: {augment.effectType}\n설명: {augment.description}");
            }
            AppendLog("증강 목록 조회 성공", builder.Length == 0 ? "아직 데이터가 없어." : builder.ToString());
        }, AppendError);
    }

    private void PostMatchResult()
    {
        if (!TryReadMatch(out MatchResultRequest request))
            return;

        BackendApiClient.Instance.SaveMatchResult(request,
            match => AppendLog("매치 결과 저장 성공", $"매치 번호: {match.id}\n승자 ID: {match.winnerId}\n점수: {match.player1Score} : {match.player2Score}"),
            AppendError);
    }

    private void GetHistory()
    {
        BackendApiClient.Instance.GetMatchHistory(0, 20, history =>
        {
            StringBuilder builder = new StringBuilder();
            if (history?.content != null)
            {
                foreach (MatchResponse match in history.content)
                    builder.AppendLine($"#{match.id} | 1P:{match.player1Id} 2P:{match.player2Id} 승자:{match.winnerId} 점수 {match.player1Score}:{match.player2Score}");
            }
            AppendLog("내 매치 기록 조회 성공", builder.Length == 0 ? "아직 매치 기록이 없어." : builder.ToString());
        }, AppendError);
    }

    private bool TryReadMatch(out MatchResultRequest request)
    {
        request = null;

        if (!long.TryParse(player1Id, out long p1) ||
            !long.TryParse(player2Id, out long p2) ||
            !long.TryParse(winnerId, out long winner) ||
            !int.TryParse(player1Score, out int s1) ||
            !int.TryParse(player2Score, out int s2))
        {
            AppendError("매치 결과 입력칸은 숫자만 넣어야 해.");
            return false;
        }

        if (p1 == p2)
        {
            AppendError("1P ID와 2P ID는 서로 달라야 해.");
            return false;
        }

        if (winner != p1 && winner != p2)
        {
            AppendError("승자 ID는 1P ID 또는 2P ID 중 하나여야 해.");
            return false;
        }

        request = new MatchResultRequest
        {
            player1Id = p1,
            player2Id = p2,
            winnerId = winner,
            player1Score = Mathf.Clamp(s1, 0, 10),
            player2Score = Mathf.Clamp(s2, 0, 10)
        };
        return true;
    }

    private void FillMyIdAsPlayer1()
    {
        if (!BackendSession.IsLoggedIn)
        {
            AppendError("먼저 로그인해야 내 ID를 넣을 수 있어.");
            return;
        }

        player1Id = BackendSession.UserId.ToString();
        AppendLog("1P ID 자동 입력", $"1P ID에 내 유저 번호 {player1Id}를 넣었어.");
    }

    private void FillMyIdAsWinner()
    {
        if (!BackendSession.IsLoggedIn)
        {
            AppendError("먼저 로그인해야 내 ID를 넣을 수 있어.");
            return;
        }

        winnerId = BackendSession.UserId.ToString();
        AppendLog("승자 ID 자동 입력", $"승자 ID에 내 유저 번호 {winnerId}를 넣었어.");
    }

    private void FillWinner(bool player1Wins)
    {
        winnerId = player1Wins ? player1Id : player2Id;
        player1Score = player1Wins ? "3" : "1";
        player2Score = player1Wins ? "1" : "3";
        AppendLog("매치 예시 입력", player1Wins ? "1P 승리 예시를 넣었어." : "2P 승리 예시를 넣었어.");
    }

    private void SyncUserFields(AuthResponse auth)
    {
        if (auth == null)
            return;

        player1Id = auth.userId.ToString();
        winnerId = player1Id;
        email = auth.email;
        nickname = auth.nickname;
        newNickname = auth.nickname;
    }

    private string FormatAuth(AuthResponse auth)
    {
        return auth == null ? "응답 없음" : $"유저 번호: {auth.userId}\n이메일: {auth.email}\n닉네임: {auth.nickname}";
    }

    private void AppendError(string message)
    {
        AppendLog("오류", message);
    }

    private void AppendLog(string title, string message)
    {
        log = $"[{System.DateTime.Now:HH:mm:ss}] {title}\n{message}\n\n{log}";
    }
}
