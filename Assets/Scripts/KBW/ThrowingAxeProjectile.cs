using Fusion;
using UnityEngine;

public class ThrowingAxeProjectile : NetworkBehaviour
{
    [Header("Collision")]
    [SerializeField] private float radius = 0.22f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 18f;
    [SerializeField] private float upwardSpeed = 2.5f;
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float maxLifeSeconds = 12f;

    [Header("Damage")]
    [SerializeField] private int damage = 35;

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.1f;
    [SerializeField] private float pickupDelaySeconds = 0.25f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float spinDegreesPerSecond = 900f;
    [SerializeField] private Vector3 flyingVisualEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 stuckVisualEulerOffset = new Vector3(0f, 0f, 90f);

    [Header("Drop")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundSearchUp = 1.0f;
    [SerializeField] private float groundSearchDown = 3.0f;
    [SerializeField] private float droppedGroundOffset = 0.06f;
    [SerializeField] private Vector3 droppedVisualEulerOffset = new Vector3(0f, 0f, 90f);

    [Networked] private NetworkId OwnerIdNet { get; set; }
    [Networked] private NetworkBool IsStuckNet { get; set; }
    [Networked] private TickTimer LifeTimer { get; set; }
    [Networked] private TickTimer PickupDelayTimer { get; set; }

    private Vector3 velocity;
    private Vector3 moveDirection;
    private bool hasInitialized;
    private float spinAngle;

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;
    }

    public void Init(NetworkRunner runner, PlayerNetwork owner, Vector3 direction)
    {
        if (!HasStateAuthority)
            return;

        OwnerIdNet = owner != null && owner.Object != null ? owner.Object.Id : default;

        IsStuckNet = false;
        LifeTimer = TickTimer.CreateFromSeconds(runner, maxLifeSeconds);
        PickupDelayTimer = default;

        moveDirection = direction;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude < 0.0001f)
            moveDirection = transform.forward;

        moveDirection.Normalize();

        velocity = moveDirection * forwardSpeed;
        velocity.y = upwardSpeed;

        IsStuckNet = false;
        LifeTimer = TickTimer.CreateFromSeconds(runner, maxLifeSeconds);
        PickupDelayTimer = default;

        hasInitialized = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!hasInitialized)
            return;

        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase != MatchPhase.Playing)
        {
            Runner.Despawn(Object);
            return;
        }

        if (LifeTimer.ExpiredOrNotRunning(Runner))
        {
            ReturnAxeToOwnerIfPossible();
            Runner.Despawn(Object);
            return;
        }

        if (IsStuckNet)
        {
            CheckPickup();
            return;
        }

        UpdateFlyingMovement();
    }

    public override void Render()
    {
        UpdateVisualSpin();
    }

    private void UpdateFlyingMovement()
    {
        Vector3 currentPosition = transform.position;

        velocity.y += gravity * Runner.DeltaTime;
        Vector3 nextPosition = currentPosition + velocity * Runner.DeltaTime;

        if (CheckCollision(currentPosition, nextPosition, out RaycastHit hit))
        {
            bool hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>() != null;

            ApplyHitDamage(hit);

            if (hitPlayer)
                DropToGroundNear(hit.point);
            else
                StickAt(hit.point, hit.normal);

            return;
        }

        transform.position = nextPosition;

        Vector3 lookDir = velocity;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized);
    }

    private bool CheckCollision(Vector3 from, Vector3 to, out RaycastHit hit)
    {
        Vector3 move = to - from;
        float distance = move.magnitude;

        if (distance <= 0.0001f)
        {
            hit = default;
            return false;
        }

        Vector3 dir = move / distance;

        if (!Physics.SphereCast(
                from,
                radius,
                dir,
                out hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

        // 던진 직후 자기 자신에게 맞는 것 방지
        if (hitPlayer != null &&
            hitPlayer.Object != null &&
            hitPlayer.Object.Id == OwnerIdNet)
        {
            return false;
        }

        return true;
    }

    private void ApplyHitDamage(RaycastHit hit)
    {
        PlayerNetwork ownerPlayer = FindOwnerPlayer();

        PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();
        if (hitPlayer != null && hitPlayer.Object != null)
        {
            if (hitPlayer.Object.Id == OwnerIdNet)
                return;

            PlayerHealth health = hitPlayer.Health;
            if (health != null)
            {
                bool applied = health.TakeDamage(damage, ownerPlayer);

                if (applied && ownerPlayer != null)
                    ownerPlayer.AddHitConfirm();
            }

            return;
        }

        DummyTargetHealth dummy = hit.collider.GetComponentInParent<DummyTargetHealth>();
        if (dummy != null)
        {
            dummy.TakeDamage(damage);
        }
    }

    private void StickAt(Vector3 point, Vector3 normal)
    {
        IsStuckNet = true;

        velocity = Vector3.zero;

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        transform.position = point + safeNormal * 0.05f;

        Vector3 flatForward = Vector3.ProjectOnPlane(moveDirection, safeNormal);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.ProjectOnPlane(transform.forward, safeNormal);

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;

        transform.rotation = Quaternion.LookRotation(flatForward.normalized, safeNormal);

        PickupDelayTimer = TickTimer.CreateFromSeconds(Runner, pickupDelaySeconds);
    }

    private void CheckPickup()
    {
        if (!PickupDelayTimer.ExpiredOrNotRunning(Runner))
            return;

        PlayerNetwork ownerPlayer = FindOwnerPlayer();
        if (ownerPlayer == null)
            return;

        float sqrDistance = (ownerPlayer.transform.position - transform.position).sqrMagnitude;

        if (sqrDistance > pickupRadius * pickupRadius)
            return;

        ownerPlayer.RestoreThrowingAxe();
        Runner.Despawn(Object);
    }

    private void ReturnAxeToOwnerIfPossible()
    {
        PlayerNetwork ownerPlayer = FindOwnerPlayer();
        if (ownerPlayer != null)
            ownerPlayer.RestoreThrowingAxe();
    }

    private PlayerNetwork FindOwnerPlayer()
    {
        if (OwnerIdNet == default)
            return null;

        NetworkObject ownerObject = Runner.FindObject(OwnerIdNet);
        if (ownerObject == null)
            return null;

        return ownerObject.GetComponent<PlayerNetwork>();
    }

    private void UpdateVisualSpin()
    {
        if (visualRoot == null)
            return;

        if (IsStuckNet)
        {
            visualRoot.localRotation = Quaternion.Euler(droppedVisualEulerOffset);
            return;
        }

        spinAngle += spinDegreesPerSecond * Time.deltaTime;

        visualRoot.localRotation =
            Quaternion.Euler(spinAngle, 0f, 0f) *
            Quaternion.Euler(flyingVisualEulerOffset);
    }

    private void DropToGroundNear(Vector3 nearPoint)
    {
        IsStuckNet = true;
        velocity = Vector3.zero;

        Vector3 rayOrigin = nearPoint + Vector3.up * groundSearchUp;
        float rayDistance = groundSearchUp + groundSearchDown;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            transform.position = groundHit.point + Vector3.up * droppedGroundOffset;
        }
        else
        {
            transform.position = nearPoint + Vector3.up * droppedGroundOffset;
        }

        Vector3 flatForward = moveDirection;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = transform.forward;

        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;

        transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

        PickupDelayTimer = TickTimer.CreateFromSeconds(Runner, pickupDelaySeconds);
    }

    private void OnDisable()
    {
        velocity = Vector3.zero;
        moveDirection = Vector3.zero;
        hasInitialized = false;
        spinAngle = 0f;

        if (visualRoot != null)
            visualRoot.localRotation = Quaternion.identity;
    }
}