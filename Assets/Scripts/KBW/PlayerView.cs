using UnityEngine;

// Holds camera anchor references and hides self renderers in first-person mode.
public class PlayerView : MonoBehaviour
{
    // Camera anchors used by LocalCamera.
    [SerializeField] private Transform firstPersonAnchor;
    [SerializeField] private Transform thirdPersonAnchor;
    [SerializeField] private Renderer[] selfRenderers;

    public Transform FirstPersonAnchor => firstPersonAnchor;
    public Transform ThirdPersonAnchor => thirdPersonAnchor;

    // Hides the local player body while using first-person view.
    public void SetFirstPersonVisual(bool isFirstPerson)
    {
        foreach (var r in selfRenderers)
        {
            if (r != null)
                r.enabled = !isFirstPerson;
        }
    }
}
