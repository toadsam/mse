using UnityEngine;

public class PlayerView : MonoBehaviour
{
    [SerializeField] private Transform firstPersonAnchor;
    [SerializeField] private Transform thirdPersonAnchor;
    [SerializeField] private Renderer[] selfRenderers;

    public Transform FirstPersonAnchor => firstPersonAnchor;
    public Transform ThirdPersonAnchor => thirdPersonAnchor;

    public void SetFirstPersonVisual(bool isFirstPerson)
    {
        foreach (var r in selfRenderers)
        {
            if (r != null)
                r.enabled = !isFirstPerson;
        }
    }
}
