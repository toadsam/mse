using UnityEngine;

// Displays the crosshair only while the local player is actively playing.
public class CrosshairUI : MonoBehaviour
{
    // Root object toggled for crosshair visibility.
    [SerializeField] private GameObject root;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        root.SetActive(false);
    }

    // Synchronizes crosshair visibility with the current match phase.
    private void Update()
    {
        GameManager gm = GameManager.Instance;

        bool shouldShow =
            gm != null &&
            gm.LocalPlayer != null &&
            gm.CurrentPhase == MatchPhase.Playing;

        if (root != null && root.activeSelf != shouldShow)
            root.SetActive(shouldShow);
    }
}