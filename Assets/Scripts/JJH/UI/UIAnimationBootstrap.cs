using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// file: Assets/Scripts/JJH/UI/UIAnimationBootstrap.cs
// Persistent installer that keeps shared button animations attached across scene transitions.
public class UIAnimationBootstrap : MonoBehaviour
{
    private static UIAnimationBootstrap instance;

    // Creates the bootstrapper before scene load so buttons can be decorated immediately.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject obj = new GameObject("UIAnimationBootstrap");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<UIAnimationBootstrap>();
    }

    // Installs the standard button animator except for augment selection UI, which manages its own state.
    public static void InstallButton(Button button)
    {
        if (button != null && button.GetComponentInParent<AugmentSelectionUI>(true) != null)
        {
            UIButtonAnimator existingAnimator = button.GetComponent<UIButtonAnimator>();
            if (existingAnimator != null)
                Destroy(existingAnimator);

            return;
        }

        UIButtonAnimator.Ensure(button);
    }

    // Installs button animators for every child button beneath a root object.
    public static void InstallButtonsIn(GameObject root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            InstallButton(button);
    }

    // Scans all loaded scenes and attaches animators to every eligible button.
    public static void InstallAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
            InstallButton(button);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(InstallLoop());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallAllButtons();
    }

    // Periodically rescans because some runtime UI is created after scene load.
    private IEnumerator InstallLoop()
    {
        while (true)
        {
            InstallAllButtons();
            yield return new WaitForSecondsRealtime(0.75f);
        }
    }
}
