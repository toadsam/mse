using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Scene References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private LocalCamera localCamera;

    private FusionBootstrap fusionBootstrap;
    private MatchManager matchManager;
    private PlayerNetwork localPlayer;
    private PlayerView localPlayerView;

    public MatchPhase CurrentPhase =>
        matchManager != null ? matchManager.Phase : MatchPhase.Lobby;

    public bool BlocksGameplayInput =>
        cursorController != null && cursorController.BlocksGameplayInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        CacheSceneReferences();
    }

    private void CacheSceneReferences()
    {
        if (cursorController == null)
            cursorController = FindFirstObjectByType<CursorController>();

        if (localCamera == null && Camera.main != null)
            localCamera = Camera.main.GetComponent<LocalCamera>();
    }

    public void RegisterBootstrap(FusionBootstrap bootstrap)
    {
        fusionBootstrap = bootstrap;
        SetMenuCursor();
    }

    public void RegisterMatchManager(MatchManager manager)
    {
        matchManager = manager;
        SyncCursorWithPhase();
    }

    public void RegisterLocalCamera(LocalCamera camera)
    {
        localCamera = camera;

        if (localPlayer != null && localPlayerView != null)
            localCamera.Bind(localPlayerView, localPlayer);
    }

    public void RegisterLocalPlayer(PlayerNetwork player, PlayerView view)
    {
        localPlayer = player;
        localPlayerView = view;

        CacheSceneReferences();

        if (localCamera != null && localPlayer != null && localPlayerView != null)
            localCamera.Bind(localPlayerView, localPlayer);

        SyncCursorWithPhase();
    }

    public void UnregisterLocalPlayer(PlayerNetwork player)
    {
        if (localPlayer != player)
            return;

        if (localCamera != null)
            localCamera.Unbind();

        localPlayer = null;
        localPlayerView = null;

        SetMenuCursor();
    }

    public void SetMenuCursor()
    {
        cursorController?.SetMenu();
    }

    public void SetGameplayCursor()
    {
        cursorController?.SetGameplay();
    }

    public void SetUICursor()
    {
        cursorController?.SetUI();
    }

    public void TogglePauseCursor()
    {
        if (cursorController == null)
            return;

        if (cursorController.CurrentState == CursorController.CursorState.Gameplay)
            SetUICursor();
        else
            SetGameplayCursor();
    }

    public void SyncCursorWithPhase()
    {
        if (localPlayer == null)
        {
            SetMenuCursor();
            return;
        }

        switch (CurrentPhase)
        {
            case MatchPhase.Playing:
                SetGameplayCursor();
                break;

            case MatchPhase.ChoosingAugment:
                SetUICursor();
                break;

            default:
                SetMenuCursor();
                break;
        }
    }
}