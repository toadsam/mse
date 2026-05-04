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

    [Header("Fire Recoil")]
    [SerializeField] private bool useFireRecoil = true;

    // 음수면 보통 위로 튀는 느낌입니다. 반대로 움직이면 +로 바꾸세요.
    [SerializeField] private float fireRecoilPitchKick = -0.45f;
    [SerializeField] private float fireRecoilYawRandom = 0.08f;
    [SerializeField] private float fireRecoilRollRandom = 0.12f;

    // 카메라 위치 흔들림입니다. 너무 크면 조준이 불편해지므로 작게 둡니다.
    [SerializeField] private Vector3 fireRecoilPositionKick = new Vector3(0f, 0.005f, -0.015f);

    [SerializeField] private float recoilSnappiness = 28f;
    [SerializeField] private float recoilReturnSpeed = 18f;

    [SerializeField] private float maxRecoilPitch = 1.2f;
    [SerializeField] private float maxRecoilYaw = 0.5f;
    [SerializeField] private float maxRecoilRoll = 0.8f;
    [SerializeField] private float maxRecoilPosition = 0.05f;

    private int lastObservedFireAnimCount;
    private Vector3 recoilRotationTarget;
    private Vector3 recoilRotationCurrent;
    private Vector3 recoilPositionTarget;
    private Vector3 recoilPositionCurrent;

    public bool IsBound => target != null && targetNetwork != null;

    public void Bind(PlayerView view, PlayerNetwork network)
    {
        target = view;
        targetNetwork = network;
        mode = CameraMode.ThirdPerson;

        lastObservedFireAnimCount = targetNetwork != null ? targetNetwork.FireAnimCount : 0;

        recoilRotationTarget = Vector3.zero;
        recoilRotationCurrent = Vector3.zero;
        recoilPositionTarget = Vector3.zero;
        recoilPositionCurrent = Vector3.zero;

        ApplyVisualMode();
    }

    public void Unbind()
    {
        if (target != null)
            target.SetFirstPersonVisual(false);

        target = null;
        targetNetwork = null;
    }

    private void Start()
    {
        GameManager.Instance?.RegisterLocalCamera(this);
    }

    private void Update()
    {
        if (!IsBound)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameManager.Instance?.TogglePauseCursor();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput) return;

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
        Quaternion baseRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 basePosition;

        if (mode == CameraMode.FirstPerson)
        {
            Transform fps = target.FirstPersonAnchor;
            if (fps == null)
                return;

            basePosition = fps.position;
        }
        else
        {
            Transform tps = target.ThirdPersonAnchor;
            if (tps == null)
                return;

            Vector3 desired =
                tps.position +
                baseRotation * thirdPersonOffset -
                baseRotation * Vector3.forward * thirdPersonDistance;

            Vector3 origin = tps.position + Vector3.up * 0.1f;
            Vector3 dir = desired - origin;
            float dist = dir.magnitude;

            if (dist > 0.001f &&
                Physics.SphereCast(origin, cameraCollisionRadius, dir.normalized, out RaycastHit hit, dist, cameraBlockMask))
            {
                desired = hit.point - dir.normalized * 0.15f;
            }

            basePosition = desired;
        }

        CheckFireRecoilEvent();
        UpdateRecoil(Time.deltaTime);

        transform.position = basePosition + baseRotation * recoilPositionCurrent;
        transform.rotation = baseRotation * Quaternion.Euler(recoilRotationCurrent);
    }

    private void ApplyVisualMode()
    {
        if (target == null)
            return;

        target.SetFirstPersonVisual(mode == CameraMode.FirstPerson);
    }

    private void CheckFireRecoilEvent()
    {
        if (!useFireRecoil || targetNetwork == null)
            return;

        int currentCount = targetNetwork.FireAnimCount;

        // 라운드 리셋 등으로 카운트가 다시 0이 된 경우
        if (currentCount < lastObservedFireAnimCount)
        {
            lastObservedFireAnimCount = currentCount;
            return;
        }

        if (currentCount == lastObservedFireAnimCount)
            return;

        int shotCount = Mathf.Min(currentCount - lastObservedFireAnimCount, 3);

        for (int i = 0; i < shotCount; i++)
            AddFireRecoil();

        lastObservedFireAnimCount = currentCount;
    }

    private void AddFireRecoil()
    {
        float yawKick = Random.Range(-fireRecoilYawRandom, fireRecoilYawRandom);
        float rollKick = Random.Range(-fireRecoilRollRandom, fireRecoilRollRandom);

        recoilRotationTarget += new Vector3(fireRecoilPitchKick, yawKick, rollKick);
        recoilRotationTarget = ClampRecoilRotation(recoilRotationTarget);

        recoilPositionTarget += fireRecoilPositionKick;
        recoilPositionTarget = Vector3.ClampMagnitude(recoilPositionTarget, maxRecoilPosition);
    }

    private void UpdateRecoil(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        float snapT = 1f - Mathf.Exp(-recoilSnappiness * deltaTime);
        float returnT = 1f - Mathf.Exp(-recoilReturnSpeed * deltaTime);

        recoilRotationTarget = Vector3.Lerp(recoilRotationTarget, Vector3.zero, returnT);
        recoilRotationCurrent = Vector3.Lerp(recoilRotationCurrent, recoilRotationTarget, snapT);

        recoilPositionTarget = Vector3.Lerp(recoilPositionTarget, Vector3.zero, returnT);
        recoilPositionCurrent = Vector3.Lerp(recoilPositionCurrent, recoilPositionTarget, snapT);
    }

    private Vector3 ClampRecoilRotation(Vector3 value)
    {
        value.x = Mathf.Clamp(value.x, -maxRecoilPitch, maxRecoilPitch);
        value.y = Mathf.Clamp(value.y, -maxRecoilYaw, maxRecoilYaw);
        value.z = Mathf.Clamp(value.z, -maxRecoilRoll, maxRecoilRoll);
        return value;
    }
}