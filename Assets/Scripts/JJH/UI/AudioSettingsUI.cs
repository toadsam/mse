using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        SetVisible(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

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

    public static void PlaySfx(AudioSource source, AudioClip clip)
    {
        if (clip == null)
            return;

        if (source != null)
            source.PlayOneShot(clip, SfxVolume);
        else
            GameAudio.PlayClip2D(clip);
    }

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

        if (GameManager.Instance != null)
            GameManager.Instance.SetUICursor();
    }

    public void Close()
    {
        SetVisible(false);

        if (GameManager.Instance != null)
            GameManager.Instance.SyncCursorWithPhase();
    }

    private void ResolveReferences()
    {
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        panelGroup = FindChildComponent<CanvasGroup>("SettingsOverlay");

        if (panelGroup == null)
            panelGroup = GetComponent<CanvasGroup>();

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

    private void BindControls()
    {
        if (openButton != null)
            openButton.onClick.AddListener(Open);

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
            closeButton.onClick.AddListener(Close);
    }

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

    private void UpdatePhaseAudio()
    {
        MatchPhase currentPhase = GetCurrentPhase();

        if (!hasAudioPhase)
        {
            hasAudioPhase = true;
            lastAudioPhase = currentPhase;
            ApplyBgmForPhase(currentPhase);
            return;
        }

        if (currentPhase == lastAudioPhase)
            return;

        MatchPhase previousPhase = lastAudioPhase;
        lastAudioPhase = currentPhase;

        ApplyBgmForPhase(currentPhase);
        TryPlayStartSound(previousPhase, currentPhase);
    }

    private MatchPhase GetCurrentPhase()
    {
        if (MatchManager.Instance != null)
            return MatchManager.Instance.CurrentPhase;

        if (GameManager.Instance != null)
            return GameManager.Instance.CurrentPhase;

        return MatchPhase.Lobby;
    }

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

    private static GameBgmClipId GetBgmClipIdForPhase(MatchPhase phase)
    {
        return phase == MatchPhase.Lobby ? GameBgmClipId.Lobby : GameBgmClipId.Game;
    }

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

    private void LoadVolumes()
    {
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        UpdateVolumeTexts();
    }

    private void SetBgmVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
        PlayerPrefs.Save();

        ApplyBgmVolumeToScene();
        UpdateVolumeTexts();
    }

    private void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();

        GameAudio.ApplySfxVolume();
        UpdateVolumeTexts();
    }

    private void ApplyAllVolumes()
    {
        ApplyBgmVolumeToScene();
        UpdateVolumeTexts();
    }

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

    private void UpdateVolumeTexts()
    {
        if (bgmValueText != null)
            bgmValueText.text = $"{Mathf.RoundToInt(bgmVolume * 100f)}%";

        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(sfxVolume * 100f)}%";
    }

    private void SetVisible(bool visible)
    {
        isOpen = visible;

        if (openButton != null)
            openButton.gameObject.SetActive(!visible);

        if (panelGroup != null)
        {
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        EnsureManagedBgmSource();
        ApplyBgmForPhase(GetCurrentPhase());
        ApplyAllVolumes();
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

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

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

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
    MatchStart
}

public enum GameBgmClipId
{
    Default,
    Lobby,
    Game
}

public static class GameAudio
{
    private const string BgmResourcePath = "Audio/JJH/BGM";
    private const string SfxResourcePath = "Audio/JJH/SFX";

    private static readonly Dictionary<GameAudioClipId, AudioClip[]> sfxCache = new();
    private static readonly Dictionary<GameBgmClipId, AudioClip[]> bgmCache = new();
    private static readonly List<GameAudioSfxSource> activeSfxSources = new();
    private static AudioSource shared2DSource;

    public static AudioClip GetBgmClip(GameBgmClipId clipId = GameBgmClipId.Default)
    {
        AudioClip[] clips = GetBgmClips(clipId);
        return clips.Length > 0 ? clips[0] : null;
    }

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

    public static void PlaySfx2D(GameAudioClipId clipId, float volumeScale = 1f)
    {
        PlayClip2D(GetSfxClip(clipId), volumeScale);
    }

    public static void PlaySfxAt(GameAudioClipId clipId, Vector3 position, float volumeScale = 1f, float spatialBlend = 0.75f)
    {
        PlayClipAt(GetSfxClip(clipId), position, volumeScale, spatialBlend);
    }

    public static void PlayClip2D(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        AudioSource source = GetShared2DSource();
        source.volume = AudioSettingsUI.SfxVolume;
        source.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
    }

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

    internal static void RegisterSfxSource(GameAudioSfxSource source)
    {
        if (source != null && !activeSfxSources.Contains(source))
            activeSfxSources.Add(source);
    }

    internal static void UnregisterSfxSource(GameAudioSfxSource source)
    {
        if (source != null)
            activeSfxSources.Remove(source);
    }

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

        AddUniqueClips(clips, Resources.LoadAll<AudioClip>(BgmResourcePath));

        AudioClip[] result = clips.ToArray();
        SortByName(result);
        bgmCache[clipId] = result;
        return result;
    }

    private static AudioClip GetSfxClip(GameAudioClipId clipId)
    {
        AudioClip[] clips = GetSfxClips(clipId);
        if (clips.Length == 0)
            return null;

        return clips[Random.Range(0, clips.Length)];
    }

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
            default:
                return clipId.ToString();
        }
    }

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
            default:
                return new[] { clipId.ToString() };
        }
    }

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

    private static void SortByName(AudioClip[] clips)
    {
        System.Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
    }
}

public sealed class GameAudioSfxSource : MonoBehaviour
{
    private AudioSource source;
    private float baseVolume = 1f;

    public void Init(AudioSource audioSource, float volumeScale)
    {
        source = audioSource;
        baseVolume = Mathf.Max(0f, volumeScale);

        GameAudio.RegisterSfxSource(this);
        ApplyVolume();
    }

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
