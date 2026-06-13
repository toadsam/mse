using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// file: Assets/Scripts/JJH/UI/AudioSettingsUI.cs
// Persistent audio settings overlay that manages BGM/SFX volume and shared runtime audio helpers.
public class AudioSettingsUI : MonoBehaviour
{
    private const string BgmVolumeKey = "Settings.BgmVolume";
    private const string SfxVolumeKey = "Settings.SfxVolume";

    public static AudioSettingsUI Instance { get; private set; }

    public static float BgmVolume =>
        Instance != null ? Instance.bgmVolume : PlayerPrefs.GetFloat(BgmVolumeKey, 1f);

    public static float SfxVolume =>
        Instance != null ? Instance.sfxVolume : PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

    private readonly Dictionary<AudioSource, float> baseBgmVolumes = new();

    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private CanvasGroup panelGroup;
    private UIPanelAnimator panelAnimator;
    private Button openButton;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Text bgmValueText;
    private Text sfxValueText;
    private Button closeButton;
    private AudioSource managedBgmSource;
    private MatchPhase lastAudioPhase;
    private int lastStartSoundRound = -1;
    private bool hasAudioPhase;

    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private bool isOpen;

    // Initializes the persistent UI, audio source, saved settings, and control bindings.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureEventSystem();
        ResolveReferences();
        EnsureManagedBgmSource();
        LoadVolumes();
        BindControls();
        ApplyAllVolumes();
        SetVisible(false, true);
    }

    // Registers for scene reloads so audio/UI references stay valid across transitions.
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Handles hotkeys for opening the panel and keeps phase-driven audio in sync.
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (isOpen)
                Close();
            else
                Open();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();

        UpdatePhaseAudio();
    }

    // Opens the settings panel from any caller without needing a scene reference.
    public static void OpenSettings()
    {
        AudioSettingsUI ui = Instance;

        if (ui == null)
            ui = FindFirstObjectByType<AudioSettingsUI>(FindObjectsInactive.Include);

        if (ui != null)
            ui.Open();
        else
            Debug.LogWarning("[AudioSettingsUI] Settings UI was not found in the loaded scenes.");
    }

    // Plays a one-shot SFX while respecting the global SFX volume setting.
    public static void PlaySfx(AudioSource source, AudioClip clip)
    {
        if (clip == null)
            return;

        if (source != null)
            source.PlayOneShot(clip, SfxVolume);
        else
            GameAudio.PlayClip2D(clip);
    }

    // Falls back to a shared game audio clip when a direct AudioClip reference is missing.
    public static void PlaySfx(AudioSource source, AudioClip clip, GameAudioClipId fallbackClip)
    {
        if (clip != null)
        {
            PlaySfx(source, clip);
            return;
        }

        GameAudio.PlaySfx2D(fallbackClip);
    }

    public void Open()
    {
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
    }

    // Locates all scene references required by the settings overlay and configures animation.
    private void ResolveReferences()
    {
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        panelGroup = FindChildComponent<CanvasGroup>("SettingsOverlay");

        if (panelGroup == null)
            panelGroup = GetComponent<CanvasGroup>();

        if (panelGroup != null)
        {
            panelAnimator = UIPanelAnimator.Ensure(panelGroup.gameObject);
            panelAnimator.Configure(new Vector2(0f, -12f), 0.96f, 0.16f, 0.1f);
        }

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
        }

        if (raycaster != null)
            raycaster.enabled = true;

        openButton = FindChildComponent<Button>("SettingsOpenButton");
        bgmSlider = FindChildComponent<Slider>("BGMSlider");
        sfxSlider = FindChildComponent<Slider>("SFXSlider");
        closeButton = FindChildComponent<Button>("CloseButton");

        Transform bgmRow = FindChild("BgmVolumeRow");
        Transform sfxRow = FindChild("SfxVolumeRow");

        bgmValueText = bgmRow != null ? FindChildComponent<Text>(bgmRow, "ValueText") : null;
        sfxValueText = sfxRow != null ? FindChildComponent<Text>(sfxRow, "ValueText") : null;
    }

    // Hooks up buttons and sliders after restoring the current saved volume values.
    private void BindControls()
    {
        if (openButton != null)
        {
            UIAnimationBootstrap.InstallButton(openButton);
            openButton.onClick.AddListener(Open);
        }

        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(bgmVolume);
            bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        if (closeButton != null)
        {
            UIAnimationBootstrap.InstallButton(closeButton);
            closeButton.onClick.AddListener(Close);
        }
    }

    // Creates or reuses the dedicated looping BGM source owned by this settings UI.
    private void EnsureManagedBgmSource()
    {
        Transform sourceTransform = transform.Find("BgmAudioSource");
        if (sourceTransform == null)
        {
            GameObject sourceObject = new GameObject("BgmAudioSource");
            sourceObject.transform.SetParent(transform, false);
            sourceTransform = sourceObject.transform;
        }

        managedBgmSource = sourceTransform.GetComponent<AudioSource>();
        if (managedBgmSource == null)
            managedBgmSource = sourceTransform.gameObject.AddComponent<AudioSource>();

        managedBgmSource.loop = true;
        managedBgmSource.playOnAwake = true;
        managedBgmSource.spatialBlend = 0f;

        if (managedBgmSource.clip == null)
            managedBgmSource.clip = GameAudio.GetBgmClip(GetBgmClipIdForPhase(GetCurrentPhase()));

        if (managedBgmSource.clip != null && !managedBgmSource.isPlaying)
            managedBgmSource.Play();
    }

    // Detects phase changes and switches BGM/SFX cues accordingly.
    private void UpdatePhaseAudio()
    {
        MatchPhase currentPhase = GetCurrentPhase();
        ApplyBgmForPhase(currentPhase);

        if (!hasAudioPhase)
        {
            hasAudioPhase = true;
            lastAudioPhase = currentPhase;
            return;
        }

        if (currentPhase == lastAudioPhase)
            return;

        MatchPhase previousPhase = lastAudioPhase;
        lastAudioPhase = currentPhase;

        TryPlayStartSound(previousPhase, currentPhase);
    }

    // Resolves the active gameplay phase from the available game managers.
    private MatchPhase GetCurrentPhase()
    {
        if (MatchManager.Instance != null)
            return MatchManager.Instance.CurrentPhase;

        if (GameManager.Instance != null)
            return GameManager.Instance.CurrentPhase;

        return MatchPhase.Lobby;
    }

    // Swaps the managed BGM clip when the match phase requires a different track.
    private void ApplyBgmForPhase(MatchPhase phase)
    {
        if (managedBgmSource == null)
            return;

        AudioClip clip = GameAudio.GetBgmClip(GetBgmClipIdForPhase(phase));
        if (clip == null)
            return;

        if (managedBgmSource.clip == clip && managedBgmSource.isPlaying)
            return;

        managedBgmSource.clip = clip;
        managedBgmSource.loop = true;
        managedBgmSource.Play();

        baseBgmVolumes[managedBgmSource] = 1f;
        ApplyBgmVolumeToScene();
    }

    private GameBgmClipId GetBgmClipIdForPhase(MatchPhase phase)
    {
        if (ShouldUseGameBgm(phase))
            return GameBgmClipId.Game;

        return GameBgmClipId.Lobby;
    }

    // Uses gameplay BGM once a real networked match has started.
    private bool ShouldUseGameBgm(MatchPhase phase)
    {
        if (phase != MatchPhase.Lobby)
            return true;

        MatchManager match = GameManager.Instance != null ? GameManager.Instance.Match : null;
        if (match == null)
            match = MatchManager.Instance;

        if (match == null || !match.IsNetworkSpawned)
            return false;

        try
        {
            return match.RoundIndex > 0 || match.CurrentPhase != MatchPhase.Lobby;
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
    }

    // Plays the round-start cue only once per new round transition.
    private void TryPlayStartSound(MatchPhase previousPhase, MatchPhase currentPhase)
    {
        bool startedRound =
            currentPhase == MatchPhase.RoundIntro ||
            (previousPhase != MatchPhase.Playing && currentPhase == MatchPhase.Playing);

        if (!startedRound)
            return;

        int round = MatchManager.Instance != null ? MatchManager.Instance.RoundIndex : 0;
        if (round == lastStartSoundRound)
            return;

        lastStartSoundRound = round;
        GameAudio.PlaySfx2D(GameAudioClipId.MatchStart);
    }

    // Restores persisted volume values from PlayerPrefs.
    private void LoadVolumes()
    {
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        UpdateVolumeTexts();
    }

    // Saves the BGM slider value and reapplies it to all looping scene sources.
    private void SetBgmVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
        PlayerPrefs.Save();

        ApplyBgmVolumeToScene();
        UpdateVolumeTexts();
    }

    // Saves the SFX slider value and pushes it to all tracked SFX sources.
    private void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();

        GameAudio.ApplySfxVolume();
        UpdateVolumeTexts();
    }

    // Reapplies all volume-dependent state after initialization or scene changes.
    private void ApplyAllVolumes()
    {
        ApplyBgmVolumeToScene();
        UpdateVolumeTexts();
    }

    // Treats looping AudioSources as BGM and scales them against their captured base volume.
    private void ApplyBgmVolumeToScene()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (AudioSource source in sources)
        {
            if (source == null || !source.loop)
                continue;

            if (!baseBgmVolumes.TryGetValue(source, out float baseVolume))
            {
                baseVolume = source.volume;
                baseBgmVolumes[source] = baseVolume;
            }

            source.volume = baseVolume * bgmVolume;
        }
    }

    // Updates the percent labels shown beside the volume sliders.
    private void UpdateVolumeTexts()
    {
        if (bgmValueText != null)
            bgmValueText.text = $"{Mathf.RoundToInt(bgmVolume * 100f)}%";

        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(sfxVolume * 100f)}%";
    }

    // Shows or hides the settings panel and coordinates cursor locking with GameManager.
    private void SetVisible(bool visible, bool instant = false)
    {
        bool wasOpen = isOpen;
        isOpen = visible;

        if (GameManager.Instance != null)
        {
            if (visible && !wasOpen)
                GameManager.Instance.RequestUICursorLock();
            else if (!visible && wasOpen)
                GameManager.Instance.ReleaseUICursorLock();
        }

        if (openButton != null)
            openButton.gameObject.SetActive(!visible);

        if (panelGroup != null)
        {
            if (panelAnimator != null)
            {
                if (visible)
                    panelAnimator.Show(instant);
                else
                    panelAnimator.Hide(instant);
            }
            else
            {
                panelGroup.alpha = visible ? 1f : 0f;
                panelGroup.interactable = visible;
                panelGroup.blocksRaycasts = visible;
            }
        }
    }

    // Reinitializes transient scene references after a new scene loads.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        EnsureManagedBgmSource();
        ApplyBgmForPhase(GetCurrentPhase());
        ApplyAllVolumes();
    }

    // Ensures a usable EventSystem exists so the runtime-generated UI remains interactive.
    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    // Finds the first descendant transform by exact name, including inactive objects.
    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    // Finds a component on a named child beneath this object.
    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    // Finds a component on a named child beneath an arbitrary root transform.
    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.name == childName)
                return component;
        }

        return null;
    }
}

public enum GameAudioClipId
{
    Shoot,
    Footstep,
    HitMarker,
    Damage,
    Jump,
    Dash,
    MatchStart,
    ButtonClick
}

public enum GameBgmClipId
{
    Default,
    Lobby,
    Game
}

// Runtime audio utility that loads clips from Resources and centralizes SFX/BGM playback behavior.
public static class GameAudio
{
    private const string BgmResourcePath = "Audio/JJH/BGM";
    private const string SfxResourcePath = "Audio/JJH/SFX";

    private static readonly Dictionary<GameAudioClipId, AudioClip[]> sfxCache = new();
    private static readonly Dictionary<GameBgmClipId, AudioClip[]> bgmCache = new();
    private static readonly List<GameAudioSfxSource> activeSfxSources = new();
    private static AudioSource shared2DSource;
    private static AudioClip generatedButtonClickClip;

    // Returns the first available BGM clip for the requested category.
    public static AudioClip GetBgmClip(GameBgmClipId clipId = GameBgmClipId.Default)
    {
        AudioClip[] clips = GetBgmClips(clipId);
        return clips.Length > 0 ? clips[0] : null;
    }

    // Pushes the latest global SFX volume to shared and positional SFX sources.
    public static void ApplySfxVolume()
    {
        if (shared2DSource != null)
            shared2DSource.volume = AudioSettingsUI.SfxVolume;

        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            GameAudioSfxSource source = activeSfxSources[i];
            if (source == null)
            {
                activeSfxSources.RemoveAt(i);
                continue;
            }

            source.ApplyVolume();
        }
    }

    // Plays a cached 2D SFX by logical identifier.
    public static void PlaySfx2D(GameAudioClipId clipId, float volumeScale = 1f)
    {
        PlayClip2D(GetSfxClip(clipId), volumeScale);
    }

    // Plays a cached positional SFX clip in world space.
    public static void PlaySfxAt(GameAudioClipId clipId, Vector3 position, float volumeScale = 1f, float spatialBlend = 0.75f)
    {
        PlayClipAt(GetSfxClip(clipId), position, volumeScale, spatialBlend);
    }

    // Plays a raw clip through the shared 2D source.
    public static void PlayClip2D(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        AudioSource source = GetShared2DSource();
        source.volume = AudioSettingsUI.SfxVolume;
        source.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
    }

    // Spawns a short-lived AudioSource in the world to play a one-shot clip.
    public static void PlayClipAt(AudioClip clip, Vector3 position, float volumeScale = 1f, float spatialBlend = 0.75f)
    {
        if (clip == null)
            return;

        GameObject soundObject = new GameObject($"SFX_{clip.name}");
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = 35f;

        GameAudioSfxSource trackedSource = soundObject.AddComponent<GameAudioSfxSource>();
        trackedSource.Init(source, volumeScale);

        source.Play();

        Object.Destroy(soundObject, clip.length + 0.25f);
    }

    // Tracks live SFX sources so future volume changes can be applied retroactively.
    internal static void RegisterSfxSource(GameAudioSfxSource source)
    {
        if (source != null && !activeSfxSources.Contains(source))
            activeSfxSources.Add(source);
    }

    // Stops tracking destroyed or finished SFX sources.
    internal static void UnregisterSfxSource(GameAudioSfxSource source)
    {
        if (source != null)
            activeSfxSources.Remove(source);
    }

    // Loads and caches BGM clips from phase-specific folders, with a root fallback.
    private static AudioClip[] GetBgmClips(GameBgmClipId clipId)
    {
        if (bgmCache.TryGetValue(clipId, out AudioClip[] cached))
            return cached;

        List<AudioClip> clips = new List<AudioClip>();

        switch (clipId)
        {
            case GameBgmClipId.Lobby:
                AddUniqueClips(clips, Resources.LoadAll<AudioClip>($"{BgmResourcePath}/Lobby"));
                break;
            case GameBgmClipId.Game:
                AddUniqueClips(clips, Resources.LoadAll<AudioClip>($"{BgmResourcePath}/Game"));
                break;
        }

        if (clips.Count == 0)
            AddUniqueClips(clips, Resources.LoadAll<AudioClip>(BgmResourcePath));

        AudioClip[] result = clips.ToArray();
        SortByName(result);
        bgmCache[clipId] = result;
        return result;
    }

    // Returns a random matching SFX clip, or a generated click when no asset exists.
    private static AudioClip GetSfxClip(GameAudioClipId clipId)
    {
        AudioClip[] clips = GetSfxClips(clipId);
        if (clips.Length == 0)
        {
            if (clipId == GameAudioClipId.ButtonClick)
                return GetGeneratedButtonClickClip();

            return null;
        }

        return clips[Random.Range(0, clips.Length)];
    }

    // Loads and caches SFX clips by folder name plus filename aliases from the root folder.
    private static AudioClip[] GetSfxClips(GameAudioClipId clipId)
    {
        if (sfxCache.TryGetValue(clipId, out AudioClip[] cached))
            return cached;

        List<AudioClip> clips = new List<AudioClip>();

        string folderName = GetFolderName(clipId);
        AddUniqueClips(clips, Resources.LoadAll<AudioClip>($"{SfxResourcePath}/{folderName}"));

        AudioClip[] rootClips = Resources.LoadAll<AudioClip>(SfxResourcePath);
        string[] aliases = GetAliases(clipId);

        foreach (AudioClip clip in rootClips)
        {
            if (NameMatchesAnyAlias(clip.name, aliases))
                AddUniqueClip(clips, clip);
        }

        AudioClip[] result = clips.ToArray();
        SortByName(result);
        sfxCache[clipId] = result;
        return result;
    }

    // Creates a persistent shared 2D AudioSource used for UI and other non-positional SFX.
    private static AudioSource GetShared2DSource()
    {
        if (shared2DSource != null)
            return shared2DSource;

        GameObject soundObject = GameObject.Find("GameAudio2DSource");
        if (soundObject == null)
        {
            soundObject = new GameObject("GameAudio2DSource");
            Object.DontDestroyOnLoad(soundObject);
        }

        shared2DSource = soundObject.GetComponent<AudioSource>();
        if (shared2DSource == null)
            shared2DSource = soundObject.AddComponent<AudioSource>();

        shared2DSource.playOnAwake = false;
        shared2DSource.loop = false;
        shared2DSource.spatialBlend = 0f;

        return shared2DSource;
    }

    private static float GetScaledSfxVolume(float volumeScale)
    {
        return Mathf.Clamp01(AudioSettingsUI.SfxVolume * Mathf.Max(0f, volumeScale));
    }

    // Maps each logical clip identifier to its preferred Resources subfolder.
    private static string GetFolderName(GameAudioClipId clipId)
    {
        switch (clipId)
        {
            case GameAudioClipId.Shoot:
                return "Shoot";
            case GameAudioClipId.Footstep:
                return "Footstep";
            case GameAudioClipId.HitMarker:
                return "HitMarker";
            case GameAudioClipId.Damage:
                return "Damage";
            case GameAudioClipId.Jump:
                return "Jump";
            case GameAudioClipId.Dash:
                return "Dash";
            case GameAudioClipId.MatchStart:
                return "MatchStart";
            case GameAudioClipId.ButtonClick:
                return "ButtonClick";
            default:
                return clipId.ToString();
        }
    }

    // Allows a single logical SFX type to match multiple naming conventions on disk.
    private static string[] GetAliases(GameAudioClipId clipId)
    {
        switch (clipId)
        {
            case GameAudioClipId.Shoot:
                return new[] { "shoot", "shot", "fire", "gun", "rifle" };
            case GameAudioClipId.Footstep:
                return new[] { "footstep", "foot", "step", "walk", "run" };
            case GameAudioClipId.HitMarker:
                return new[] { "hitmarker", "hitconfirm", "confirm", "hit" };
            case GameAudioClipId.Damage:
                return new[] { "damage", "damaged", "hurt", "pain" };
            case GameAudioClipId.Jump:
                return new[] { "jump" };
            case GameAudioClipId.Dash:
                return new[] { "dash", "dodge" };
            case GameAudioClipId.MatchStart:
                return new[] { "matchstart", "gamestart", "roundstart", "start" };
            case GameAudioClipId.ButtonClick:
                return new[] { "buttonclick", "button", "click", "ui", "select", "confirm" };
            default:
                return new[] { clipId.ToString() };
        }
    }

    // Generates a tiny synthetic click so UI buttons still have feedback without an imported asset.
    private static AudioClip GetGeneratedButtonClickClip()
    {
        if (generatedButtonClickClip != null)
            return generatedButtonClickClip;

        const int frequency = 44100;
        int sampleCount = Mathf.CeilToInt(frequency * 0.045f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)frequency;
            float envelope = Mathf.Exp(-time * 85f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * 1800f * time) * 0.32f * envelope;
        }

        generatedButtonClickClip = AudioClip.Create("Generated_ButtonClick", sampleCount, 1, frequency, false);
        generatedButtonClickClip.SetData(samples, 0);
        return generatedButtonClickClip;
    }

    // Normalizes clip names before alias matching to tolerate different naming styles.
    private static bool NameMatchesAnyAlias(string clipName, string[] aliases)
    {
        string normalizedName = NormalizeName(clipName);

        foreach (string alias in aliases)
        {
            if (normalizedName.Contains(NormalizeName(alias)))
                return true;
        }

        return false;
    }

    private static string NormalizeName(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace(".", "");
    }

    // Deduplicates loaded clips while preserving discovery order before final sorting.
    private static void AddUniqueClips(List<AudioClip> clips, AudioClip[] candidates)
    {
        foreach (AudioClip clip in candidates)
            AddUniqueClip(clips, clip);
    }

    private static void AddUniqueClip(List<AudioClip> clips, AudioClip clip)
    {
        if (clip == null || clips.Contains(clip))
            return;

        clips.Add(clip);
    }

    // Sorts clips deterministically so random selection is stable across repeated loads.
    private static void SortByName(AudioClip[] clips)
    {
        System.Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
    }
}

// Small helper component that remembers the original SFX volume scale for a spawned AudioSource.
public sealed class GameAudioSfxSource : MonoBehaviour
{
    private AudioSource source;
    private float baseVolume = 1f;

    // Captures the created AudioSource and registers it for future global volume updates.
    public void Init(AudioSource audioSource, float volumeScale)
    {
        source = audioSource;
        baseVolume = Mathf.Max(0f, volumeScale);

        GameAudio.RegisterSfxSource(this);
        ApplyVolume();
    }

    // Reapplies the current global SFX volume using the stored per-source scale.
    public void ApplyVolume()
    {
        if (source != null)
            source.volume = Mathf.Clamp01(AudioSettingsUI.SfxVolume * baseVolume);
    }

    private void OnDestroy()
    {
        GameAudio.UnregisterSfxSource(this);
    }
}
