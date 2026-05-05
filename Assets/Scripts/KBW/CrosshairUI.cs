using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    [SerializeField] private GameObject root;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        root.SetActive(false);
    }

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