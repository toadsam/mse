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
        if (source == null || clip == null)
            return;

        source.PlayOneShot(clip, SfxVolume);
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
