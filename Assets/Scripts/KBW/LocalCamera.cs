using UnityEngine;

public class LocalCamera : MonoBehaviour
{
    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Header("Target")]
    [SerializeField] private PlayerView target;
    [SerializeField] private PlayerNetwork targetNetwork;

    [Header("Mode")]
    [SerializeField] private CameraMode mode = CameraMode.ThirdPerson;

    [Header("Third Person")]
    [SerializeField] private float thirdPersonDistance = 3.5f;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.5f, 0.2f, 0f);
    [SerializeField] private LayerMask cameraBlockMask;
    [SerializeField] private float cameraCollisionRadius = 0.2f;

    public bool IsBound => target != null && targetNetwork != null;

    public void Bind(PlayerView view, PlayerNetwork network)
    {
        target = view;
        targetNetwork = network;
        ApplyVisualMode();
    }

    public void Unbind()
    {
        if (target != null)
            target.SetFirstPersonVisual(false);

        target = null;
        targetNetwork = null;
    }

    private void Update()
    {
        if (!IsBound)
            return;

        if (Input.GetKeyDown(KeyCode.V))
        {
            mode = mode == CameraMode.FirstPerson
                ? CameraMode.ThirdPerson
                : CameraMode.FirstPerson;

            ApplyVisualMode();
        }
    }

    private void LateUpdate()
    {
        if (!IsBound)
            return;

        float yaw = targetNetwork.LookYaw;
        float pitch = targetNetwork.LookPitch;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        if (mode == CameraMode.FirstPerson)
        {
            Transform fps = target.FirstPersonAnchor;
            if (fps == null)
                return;

            transform.position = fps.position;
            transform.rotation = rot;
            return;
        }

        Transform tps = target.ThirdPersonAnchor;
        if (tps == null)
            return;

        Vector3 desired =
            tps.position +
            rot * thirdPersonOffset -
            rot * Vector3.forward * thirdPersonDistance;

        Vector3 origin = tps.position + Vector3.up * 0.1f;
        Vector3 dir = desired - origin;
        float dist = dir.magnitude;

        if (dist > 0.001f &&
            Physics.SphereCast(origin, cameraCollisionRadius, dir.normalized, out RaycastHit hit, dist, cameraBlockMask))
        {
            desired = hit.point - dir.normalized * 0.15f;
        }

        transform.position = desired;
        transform.rotation = rot;
    }

    private void ApplyVisualMode()
    {
        if (target == null)
            return;

        target.SetFirstPersonVisual(mode == CameraMode.FirstPerson);
    }
}