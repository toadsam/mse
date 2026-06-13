using UnityEngine;
using UnityEngine.SceneManagement;
// Global client-side coordinator for local player, match, camera, and cursor state.
public class GameManager : MonoBehaviour
{
    // Singleton instance used by UI and network scripts.
    public static GameManager Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Scene References")]
    // Scene references cached again after scene reloads.
    [SerializeField] private CursorController cursorController;
    [SerializeField] private LocalCamera localCamera;

    // Runtime references registered by network and player objects.
    private FusionBootstrap fusionBootstrap;
    private MatchManager matchManager;
    private PlayerNetwork localPlayer;
    private PlayerView localPlayerView;
    private int uiCursorLockCount;
    private bool pauseCursorRequested;

    public MatchManager Match => matchManager;
    public PlayerNetwork LocalPlayer => localPlayer;
    public PlayerView LocalPlayerView => localPlayerView;
    public MatchPhase CurrentPhase =>
        matchManager != null ? matchManager.CurrentPhase : MatchPhase.Lobby;

    // True when UI/menu cursor state should stop gameplay input.
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

    // Registers the active Fusion bootstrap and prepares menu cursor state.
    public void RegisterBootstrap(FusionBootstrap bootstrap)
    {
        fusionBootstrap = bootstrap;
        SetMenuCursor();
    }

    // Stores the active match manager and updates cursor mode.
    public void RegisterMatchManager(MatchManager manager)
    {
        matchManager = manager;
        SyncCursorWithPhase();
    }

    public void UnregisterMatchManager(MatchManager manager)
    {
        if (matchManager != manager)
            return;

        matchManager = null;
        SyncCursorWithPhase();
    }

    public void RegisterLocalCamera(LocalCamera camera)
    {
        localCamera = camera;

        if (localPlayer != null && localPlayerView != null)
            localCamera.Bind(localPlayerView, localPlayer);
    }

    // Binds the local player to the camera and cursor flow.
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

    public void RequestUICursorLock()
    {
        uiCursorLockCount++;
        SetUICursor();
    }

    public void ReleaseUICursorLock()
    {
        if (uiCursorLockCount > 0)
            uiCursorLockCount--;

        SyncCursorWithPhase();
    }

    public void TogglePauseCursor()
    {
        if (cursorController == null)
            return;

        if (uiCursorLockCount > 0)
            return;

        if (pauseCursorRequested)
        {
            pauseCursorRequested = false;
            SyncCursorWithPhase();
        }
        else if (CurrentPhase == MatchPhase.Playing)
        {
            pauseCursorRequested = true;
            SetUICursor();
        }
        else
        {
            SyncCursorWithPhase();
        }
    }

    // Selects the correct cursor mode from the current match phase.
    public void SyncCursorWithPhase()
    {
        if (uiCursorLockCount > 0 || pauseCursorRequested)
        {
            SetUICursor();
            return;
        }

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
    // Clears local network references when leaving or restarting a session.
    public void ClearNetworkSessionState()
    {
        if (localCamera != null)
            localCamera.Unbind();

        localPlayer = null;
        localPlayerView = null;
        matchManager = null;
        pauseCursorRequested = false;

        SyncCursorWithPhase();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cursorController = null;
        localCamera = null;

        CacheSceneReferences();
        SyncCursorWithPhase();
    }
}
