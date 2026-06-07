using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIAnimationBootstrap : MonoBehaviour
{
    private static UIAnimationBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject obj = new GameObject("UIAnimationBootstrap");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<UIAnimationBootstrap>();
    }

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

    public static void InstallButtonsIn(GameObject root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            InstallButton(button);
    }

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

    private IEnumerator InstallLoop()
    {
        while (true)
        {
            InstallAllButtons();
            yield return new WaitForSecondsRealtime(0.75f);
        }
    }
}
